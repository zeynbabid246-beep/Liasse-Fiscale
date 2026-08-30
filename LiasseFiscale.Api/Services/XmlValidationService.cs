using System.Xml;
using System.Xml.Schema;

namespace LiasseFiscale.Api.Services;

public class XmlValidationService : IXmlValidationService
{
    private const string TargetNamespace = "http://www.impots.finances.gov.tn/liasse";

    private readonly Dictionary<string, XmlSchemaSet> _schemaSets = new(StringComparer.OrdinalIgnoreCase);
    private readonly IAssertRuleEngine _assertRuleEngine;
    private readonly ILogger<XmlValidationService> _logger;

    public IReadOnlyList<string> DocumentsSupportes => _schemaSets.Keys.ToList();

    public XmlValidationService(
        IConfiguration configuration,
        IAssertRuleEngine assertRuleEngine,
        ILogger<XmlValidationService> logger)
    {
        _assertRuleEngine = assertRuleEngine;
        _logger = logger;

        var structuralPath = configuration["SchemaAssets:StructuralPath"] ?? "SchemaAssets/structural";
        var fullPath = Path.Combine(AppContext.BaseDirectory, structuralPath);

        if (!Directory.Exists(fullPath))
        {
            _logger.LogWarning("Dossier de schémas structurels introuvable : {Path}", fullPath);
            return;
        }

        var documentFiles = Directory.GetFiles(fullPath, "F*.xsd");

        foreach (var file in documentFiles)
        {
            var codeDocument = Path.GetFileNameWithoutExtension(file);
            try
            {
                var schemaSet = new XmlSchemaSet();
                schemaSet.Add(TargetNamespace, file);
                schemaSet.Compile();
                _schemaSets[codeDocument] = schemaSet;
                _logger.LogInformation("Schéma structurel chargé : {Code}", codeDocument);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Échec du chargement du schéma structurel {File}", file);
            }
        }
    }

    public ValidationResult ValiderStructure(string codeDocument, Stream xmlStream)
    {
        if (!_schemaSets.TryGetValue(codeDocument, out var schemaSet))
        {
            return new ValidationResult(false, new[]
            {
                new ValidationIssue("Structurelle", null, null,
                    $"Aucun schéma structurel disponible pour le document '{codeDocument}'.")
            });
        }

        var erreurs = new List<ValidationIssue>();

        var settings = new XmlReaderSettings
        {
            ValidationType = ValidationType.Schema,
            Schemas = schemaSet
        };

        settings.ValidationEventHandler += (sender, args) =>
        {
            erreurs.Add(new ValidationIssue(
                "Structurelle",
                Champ: null,
                Ligne: args.Exception?.LineNumber,
                Message: args.Message));
        };

        try
        {
            xmlStream.Position = 0;
            using var reader = XmlReader.Create(xmlStream, settings);
            while (reader.Read()) { /* validation pendant lecture */ }
        }
        catch (XmlException ex)
        {
            erreurs.Add(new ValidationIssue("Structurelle", null, ex.LineNumber, $"XML mal formé : {ex.Message}"));
        }

        return new ValidationResult(erreurs.Count == 0, erreurs);
    }

    public ValidationResult ValiderDocumentComplet(string codeDocument, Stream xmlStream)
    {
        // 1) Couche structurelle (XSD 1.0)
        var resultatStructurel = ValiderStructure(codeDocument, xmlStream);
        var toutesErreurs = new List<ValidationIssue>(resultatStructurel.Erreurs);

        // Si le XML est fondamentalement mal formé ou comporte des erreurs structurelles graves,
        // on retourne directement pour éviter des exceptions de parsing DOM.
        bool hasXmlMalformed = resultatStructurel.Erreurs.Any(e => e.Message.StartsWith("XML mal formé", StringComparison.OrdinalIgnoreCase));
        if (hasXmlMalformed)
        {
            return new ValidationResult(false, toutesErreurs);
        }

        // 2) Couche métier (Formules et assertions XPath)
        try
        {
            xmlStream.Position = 0;
            var erreursMetier = _assertRuleEngine.ValiderDocument(codeDocument, xmlStream);
            toutesErreurs.AddRange(erreursMetier);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la validation métier du document {CodeDocument}", codeDocument);
            toutesErreurs.Add(new ValidationIssue("RegleMetier", null, null, $"Erreur lors de l'évaluation des règles métier : {ex.Message}"));
        }

        return new ValidationResult(toutesErreurs.Count == 0, toutesErreurs);
    }
}
