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
        var candidatePaths = new[]
        {
            Path.IsPathRooted(structuralPath) ? structuralPath : Path.Combine(AppContext.BaseDirectory, structuralPath),
            Path.Combine(Directory.GetCurrentDirectory(), structuralPath),
            Path.Combine(Directory.GetCurrentDirectory(), "LiasseFiscale.Api", structuralPath),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LiasseFiscale.Api", structuralPath)
        };

        var fullPath = candidatePaths.FirstOrDefault(Directory.Exists);

        if (fullPath is null)
        {
            _logger.LogWarning("Dossier de schémas structurels introuvable dans les chemins testés.");
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

        MemoryStream memoryStream;
        bool disposeMemoryStream = false;
        if (xmlStream is MemoryStream ms && ms.CanSeek)
        {
            memoryStream = ms;
        }
        else
        {
            memoryStream = new MemoryStream();
            if (xmlStream.CanSeek) xmlStream.Position = 0;
            xmlStream.CopyTo(memoryStream);
            disposeMemoryStream = true;
        }

        var erreurs = new List<ValidationIssue>();

        try
        {
            // 1. Contrôle explicite de la racine XML et de l'espace de noms cible
            memoryStream.Position = 0;
            using (var rawReader = XmlReader.Create(memoryStream, new XmlReaderSettings { IgnoreComments = true, IgnoreWhitespace = true }))
            {
                while (rawReader.Read())
                {
                    if (rawReader.NodeType == XmlNodeType.Element)
                    {
                        if (!string.Equals(rawReader.LocalName, codeDocument, StringComparison.OrdinalIgnoreCase))
                        {
                            erreurs.Add(new ValidationIssue(
                                Source: "Structurelle",
                                Champ: rawReader.LocalName,
                                Ligne: 1,
                                Message: $"La racine XML '{rawReader.LocalName}' ne correspond pas au document attendu '{codeDocument}'."
                            ));
                        }
                        if (!string.IsNullOrEmpty(rawReader.NamespaceURI) && !string.Equals(rawReader.NamespaceURI, TargetNamespace, StringComparison.OrdinalIgnoreCase))
                        {
                            erreurs.Add(new ValidationIssue(
                                Source: "Structurelle",
                                Champ: rawReader.LocalName,
                                Ligne: 1,
                                Message: $"L'espace de noms de la racine XML '{rawReader.NamespaceURI}' ne correspond pas à l'espace officiel attendu '{TargetNamespace}'."
                            ));
                        }
                        break;
                    }
                }
            }

            // 2. Validation formelle par le schéma XSD 1.0
            memoryStream.Position = 0;
            var settings = new XmlReaderSettings
            {
                ValidationType = ValidationType.Schema,
                Schemas = schemaSet
            };

            settings.ValidationEventHandler += (sender, args) =>
            {
                string? champ = null;
                if (sender is XmlReader r && r.NodeType == XmlNodeType.Element)
                {
                    champ = r.LocalName;
                }
                erreurs.Add(new ValidationIssue(
                    "Structurelle",
                    Champ: champ,
                    Ligne: args.Exception?.LineNumber,
                    Message: args.Message));
            };

            using var reader = XmlReader.Create(memoryStream, settings);
            while (reader.Read()) { /* validation pendant lecture */ }
        }
        catch (XmlException ex)
        {
            erreurs.Add(new ValidationIssue("Structurelle", null, ex.LineNumber, $"XML mal formé : {ex.Message}"));
        }
        finally
        {
            if (disposeMemoryStream)
            {
                memoryStream.Dispose();
            }
            if (xmlStream.CanSeek)
            {
                xmlStream.Position = 0;
            }
        }

        return new ValidationResult(erreurs.Count == 0, erreurs);
    }

    public ValidationResult ValiderDocumentComplet(string codeDocument, Stream xmlStream)
    {
        MemoryStream memoryStream;
        bool disposeMemoryStream = false;
        if (xmlStream is MemoryStream ms && ms.CanSeek)
        {
            memoryStream = ms;
        }
        else
        {
            memoryStream = new MemoryStream();
            if (xmlStream.CanSeek) xmlStream.Position = 0;
            xmlStream.CopyTo(memoryStream);
            disposeMemoryStream = true;
        }

        try
        {
            memoryStream.Position = 0;
            // 1) Couche structurelle (XSD 1.0)
            var resultatStructurel = ValiderStructure(codeDocument, memoryStream);
            var toutesErreurs = new List<ValidationIssue>(resultatStructurel.Erreurs);

            // Si le XML est fondamentalement mal formé ou comporte des erreurs structurelles graves,
            // on ne déclenche pas les règles métier.
            bool hasXmlMalformed = resultatStructurel.Erreurs.Any(e => e.Message.StartsWith("XML mal formé", StringComparison.OrdinalIgnoreCase));
            if (!hasXmlMalformed && resultatStructurel.Erreurs.Count == 0)
            {
                // 2) Couche métier (Formules et assertions XPath)
                try
                {
                    memoryStream.Position = 0;
                    var erreursMetier = _assertRuleEngine.ValiderDocument(codeDocument, memoryStream);
                    toutesErreurs.AddRange(erreursMetier);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erreur lors de la validation métier du document {CodeDocument}", codeDocument);
                    toutesErreurs.Add(new ValidationIssue("RegleMetier", null, null, $"Erreur lors de l'évaluation des règles métier : {ex.Message}"));
                }
            }

            return new ValidationResult(toutesErreurs.Count == 0, toutesErreurs);
        }
        finally
        {
            if (disposeMemoryStream)
            {
                memoryStream.Dispose();
            }
            if (xmlStream.CanSeek)
            {
                xmlStream.Position = 0;
            }
        }
    }
}
