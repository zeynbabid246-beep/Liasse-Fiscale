using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace LiasseFiscale.Api.Services;

public record XmlHeaderInfo(
    string? MatriculeFiscalDeclarant,
    int? Exercice,
    string? RaisonSociale,
    string? Activite,
    string? Adresse,
    string? DateDebutExercice,
    string? DateClotureExercice,
    string? ActeDeDepot,
    string? NatureDepot
);

public class ExtractedXmlData
{
    public XmlHeaderInfo Header { get; set; } = new(null, null, null, null, null, null, null, null, null);
    public EvaluationContext Context { get; set; } = new();
    public XDocument? RawDocument { get; set; }
}

/// <summary>
/// Extrait les valeurs numériques, attributs et entête du document XML (F6001 à F6005, etc.)
/// pour alimenter l'évaluateur XPath et le validateur de cohérence.
/// </summary>
public static class XmlFieldExtractor
{
    private static readonly XNamespace Lf = "http://www.impots.finances.gov.tn/liasse";

    /// <summary>
    /// Get secure XML reader settings that prevent XXE attacks.
    /// </summary>
    private static XmlReaderSettings GetSecureXmlSettings()
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null, // Disable external entity resolution
            MaxCharactersInDocument = 67_108_864, // 64 MB limit
            IgnoreComments = true,
            IgnoreWhitespace = true,
            ConformanceLevel = ConformanceLevel.Document
        };
        return settings;
    }

    public static ExtractedXmlData ExtraireTout(Stream xmlStream)
    {
        var result = new ExtractedXmlData();

        xmlStream.Position = 0;
        using var xmlReader = XmlReader.Create(xmlStream, GetSecureXmlSettings());
        var document = XDocument.Load(xmlReader);
        result.RawDocument = document;

        // 1) Entête
        var enteteEl = document.Descendants().FirstOrDefault(e => e.Name.LocalName == "Entete" || e.Name.LocalName == "EnteteLiasse");
        if (enteteEl is not null)
        {
            var matricule = enteteEl.Elements().FirstOrDefault(e => e.Name.LocalName == "MatriculeFiscalDeclarant")?.Value?.Trim();
            var exStr = enteteEl.Elements().FirstOrDefault(e => e.Name.LocalName == "Exercice")?.Value?.Trim();
            int? exercice = int.TryParse(exStr, out var exVal) ? exVal : null;
            var raison = enteteEl.Elements().FirstOrDefault(e => e.Name.LocalName == "NometPrenomouRaisonSociale")?.Value?.Trim();
            var activite = enteteEl.Elements().FirstOrDefault(e => e.Name.LocalName == "Activite")?.Value?.Trim();
            var adresse = enteteEl.Elements().FirstOrDefault(e => e.Name.LocalName == "Adresse")?.Value?.Trim();
            var dtDebut = enteteEl.Elements().FirstOrDefault(e => e.Name.LocalName == "DateDebutExercice")?.Value?.Trim();
            var dtFin = enteteEl.Elements().FirstOrDefault(e => e.Name.LocalName == "DateClotureExercice")?.Value?.Trim();
            var acte = enteteEl.Elements().FirstOrDefault(e => e.Name.LocalName == "ActeDeDepot")?.Value?.Trim();
            var nature = enteteEl.Elements().FirstOrDefault(e => e.Name.LocalName == "NatureDepot")?.Value?.Trim();

            result.Header = new XmlHeaderInfo(matricule, exercice, raison, activite, adresse, dtDebut, dtFin, acte, nature);
        }

        // 2) Détails & Attributs
        var detailsElement = document.Descendants().FirstOrDefault(e => e.Name.LocalName == "Details");
        if (detailsElement is not null)
        {
            foreach (var element in detailsElement.DescendantsAndSelf())
            {
                var tag = element.Name.LocalName;

                // Valeur texte de l'élément
                var texte = element.Value.Trim();
                if (!string.IsNullOrEmpty(texte) && decimal.TryParse(texte, NumberStyles.Number, CultureInfo.InvariantCulture, out var val))
                {
                    result.Context.FieldValues[tag] = val;
                }

                // Attributs de l'élément (ex: @codeformejuridique, @resultat, ou attribut numérique comme F60050002="100")
                foreach (var attr in element.Attributes())
                {
                    var attrName = attr.Name.LocalName;
                    var attrVal = attr.Value.Trim();

                    // Clé qualifiée "F60050000/@codeformejuridique" et clé simple "@codeformejuridique"
                    result.Context.AttributeValues[$"{tag}/@{attrName}"] = attrVal;
                    result.Context.AttributeValues[$"@{attrName}"] = attrVal;

                    if (decimal.TryParse(attrVal, NumberStyles.Number, CultureInfo.InvariantCulture, out var attrNum))
                    {
                        result.Context.FieldValues[$"{tag}/@{attrName}"] = attrNum;
                        // Si le nom d'attribut ressemble à un champ (ex: F60050002)
                        result.Context.FieldValues[attrName] = attrNum;
                    }
                }
            }
        }

        return result;
    }

    public static Dictionary<string, decimal> ExtraireValeursNumeriques(Stream xmlStream)
    {
        var data = ExtraireTout(xmlStream);
        return data.Context.FieldValues;
    }
}
