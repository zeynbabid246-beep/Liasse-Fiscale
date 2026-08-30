namespace LiasseFiscale.Api.Models;

public enum SourceErreur
{
    Structurelle,   // couche 1 : XSD 1.0 (type, format, champ manquant)
    RegleMetier     // couche 2 : moteur d'assertions (formules d'agrégation, logique F6005)
}

public class ValidationError
{
    public int Id { get; set; }

    public int DocumentFiscalId { get; set; }
    public DocumentFiscal DocumentFiscal { get; set; } = null!;

    public SourceErreur Source { get; set; }

    /// <summary>Champ concerné, ex: "F60010001", ou vide si l'erreur est globale au document.</summary>
    public string? Champ { get; set; }

    public int? Ligne { get; set; }

    public string Message { get; set; } = string.Empty;
}
