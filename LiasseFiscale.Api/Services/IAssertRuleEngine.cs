namespace LiasseFiscale.Api.Services;

public interface IAssertRuleEngine
{
    /// <summary>
    /// Valide toutes les assertions métier d'un document XML (F6001 à F6005, etc.)
    /// via l'interpréteur XPath unifié.
    /// </summary>
    IReadOnlyList<ValidationIssue> ValiderDocument(string codeDocument, ExtractedXmlData xmlData);

    /// <summary>
    /// Valide un flux XML complet en extrayant son contexte.
    /// </summary>
    IReadOnlyList<ValidationIssue> ValiderDocument(string codeDocument, Stream xmlStream);

    /// <summary>
    /// Rétrocompatibilité : validation à partir d'un dictionnaire de valeurs numériques.
    /// </summary>
    IReadOnlyList<ValidationIssue> ValiderFormulesSimples(string codeDocument, IReadOnlyDictionary<string, decimal> valeursChamps);

    /// <summary>
    /// Retourne la table des règles simples extraites { champ_cible: [champs opérandes] } pour F6001-F6004.
    /// </summary>
    IReadOnlyDictionary<string, List<string>> ObtenirTableReglesSimples(string codeDocument);

    /// <summary>
    /// Règles complexes non prises en charge le cas échéant.
    /// </summary>
    IReadOnlyList<string> ObtenirReglesComplexesNonImplementees(string codeDocument);
}
