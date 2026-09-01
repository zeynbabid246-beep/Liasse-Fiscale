namespace LiasseFiscale.Api.Services;

public record ValidationIssue(string Source, string? Champ, int? Ligne, string Message);

public record ValidationResult(bool EstValide, IReadOnlyList<ValidationIssue> Erreurs)
{
    public ValidationResult() : this(true, new List<ValidationIssue>())
    {
    }
}


/// <summary>
/// Service unifié de validation XML des liasses fiscales :
/// Combine la validation structurelle XSD 1.0 (Couche 1) et les règles métier XPath (Couche 2).
/// </summary>
public interface IXmlValidationService
{
    /// <summary>
    /// Valide un flux XML contre le schéma structurel du document donné (ex: "F6001").
    /// </summary>
    ValidationResult ValiderStructure(string codeDocument, Stream xmlStream);

    /// <summary>
    /// Validation complète unifiée : Couche 1 (structurelle) + Couche 2 (règles métier / assertions XPath).
    /// </summary>
    ValidationResult ValiderDocumentComplet(string codeDocument, Stream xmlStream);

    /// <summary>Liste des codes documents pour lesquels un schéma structurel est disponible.</summary>
    IReadOnlyList<string> DocumentsSupportes { get; }
}
