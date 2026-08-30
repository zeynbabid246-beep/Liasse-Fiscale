using System.Text.Json;
using System.Text.RegularExpressions;

namespace LiasseFiscale.Api.Services;

internal record SimpleSumRule(string Target, string Operation, List<string> Operands, string RawTest);

internal record RulesDocument(string Document, int TotalAssertions, List<SimpleSumRule> SimpleSumRules, List<RawRule> ComplexRules);

internal record RawRule(string RawTest);

public class AssertRuleEngine : IAssertRuleEngine
{
    private static readonly Regex TargetRegex = new(@"^lf:([A-Za-z0-9_]+)", RegexOptions.Compiled);

    private readonly Dictionary<string, RulesDocument> _rulesByDocument = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<string>> _allAssertsByDocument = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<AssertRuleEngine> _logger;

    public AssertRuleEngine(IConfiguration configuration, ILogger<AssertRuleEngine> logger)
    {
        _logger = logger;

        var rulesPath = configuration["SchemaAssets:RulesPath"] ?? "SchemaAssets/rules";
        var fullPath = Path.Combine(AppContext.BaseDirectory, rulesPath);

        if (!Directory.Exists(fullPath))
        {
            _logger.LogWarning("Dossier de règles introuvable : {Path}", fullPath);
            return;
        }

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        foreach (var file in Directory.GetFiles(fullPath, "*.rules.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var doc = JsonSerializer.Deserialize<RulesDocument>(json, options);
                if (doc is not null)
                {
                    _rulesByDocument[doc.Document] = doc;

                    var allAsserts = new List<string>();
                    if (doc.SimpleSumRules is not null)
                    {
                        allAsserts.AddRange(doc.SimpleSumRules.Select(r => r.RawTest));
                    }
                    if (doc.ComplexRules is not null)
                    {
                        allAsserts.AddRange(doc.ComplexRules.Select(r => r.RawTest));
                    }
                    _allAssertsByDocument[doc.Document] = allAsserts;

                    _logger.LogInformation(
                        "Règles chargées pour {Document} : {Total} assertions total ({Simple} simples, {Complex} complexes)",
                        doc.Document, allAsserts.Count, doc.SimpleSumRules?.Count ?? 0, doc.ComplexRules?.Count ?? 0);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors du chargement des règles depuis {File}", file);
            }
        }
    }

    public IReadOnlyList<ValidationIssue> ValiderDocument(string codeDocument, ExtractedXmlData xmlData)
    {
        var erreurs = new List<ValidationIssue>();

        if (!_allAssertsByDocument.TryGetValue(codeDocument, out var asserts) || asserts.Count == 0)
        {
            return erreurs;
        }

        foreach (var rawTest in asserts)
        {
            var targetField = ExtractTargetField(rawTest);

            bool isValid = XPathAssertEvaluator.Evaluate(rawTest, xmlData.Context, out var message);
            if (!isValid)
            {
                erreurs.Add(new ValidationIssue(
                    Source: "RegleMetier",
                    Champ: targetField,
                    Ligne: null,
                    Message: string.IsNullOrEmpty(targetField)
                        ? $"{message} (Formule : {rawTest})"
                        : $"{targetField} : {message} (Formule : {rawTest})"
                ));
            }
        }

        return erreurs;
    }

    public IReadOnlyList<ValidationIssue> ValiderDocument(string codeDocument, Stream xmlStream)
    {
        var xmlData = XmlFieldExtractor.ExtraireTout(xmlStream);
        return ValiderDocument(codeDocument, xmlData);
    }

    public IReadOnlyList<ValidationIssue> ValiderFormulesSimples(string codeDocument, IReadOnlyDictionary<string, decimal> valeursChamps)
    {
        var context = new EvaluationContext();
        foreach (var (k, v) in valeursChamps)
        {
            context.FieldValues[k] = v;
        }
        var xmlData = new ExtractedXmlData { Context = context };
        return ValiderDocument(codeDocument, xmlData);
    }

    public IReadOnlyDictionary<string, List<string>> ObtenirTableReglesSimples(string codeDocument)
    {
        var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        if (_rulesByDocument.TryGetValue(codeDocument, out var doc) && doc.SimpleSumRules is not null)
        {
            foreach (var rule in doc.SimpleSumRules)
            {
                result[rule.Target] = rule.Operands;
            }
        }

        return result;
    }

    public IReadOnlyList<string> ObtenirReglesComplexesNonImplementees(string codeDocument)
    {
        // Avec notre interpréteur XPath unifié, toutes les règles complexes connues (if/then/else, -1, eq)
        // sont désormais prises en charge.
        return Array.Empty<string>();
    }

    private static string? ExtractTargetField(string rawTest)
    {
        var match = TargetRegex.Match(rawTest.Trim());
        return match.Success ? match.Groups[1].Value : null;
    }
}
