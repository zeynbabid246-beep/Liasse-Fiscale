namespace LiasseFiscale.Api.Models;

public enum StatutValidation
{
    NonDepose,
    EnAttenteDeValidation,
    Valide,
    Invalide
}

/// <summary>Format attendu du document — détermine si DocumentController applique la
/// validation XSD (Xml) ou se contente d'un simple dépôt de fichier sans validation
/// structurelle possible (Pdf, ex: F6019).</summary>
public enum FormatDocument
{
    Xml,
    Pdf
}

public class DocumentFiscal
{
    public int Id { get; set; }

    public int LiasseId { get; set; }
    public Liasse Liasse { get; set; } = null!;

    public string CodeDocument { get; set; } = string.Empty;
    public string Libelle { get; set; } = string.Empty;
    public FormatDocument Format { get; set; } = FormatDocument.Xml;
    public bool EstObligatoire { get; set; }

    public string NomFichier { get; set; } = string.Empty;
    public string CheminStockage { get; set; } = string.Empty;

    public StatutValidation Statut { get; set; } = StatutValidation.NonDepose;

    public List<ValidationError> Erreurs { get; set; } = new();

    public DateTime? DateUpload { get; set; }

    /// <summary>User who uploaded this document.</summary>
    public int? UploadedBy { get; set; }
    public User? UploadedByUser { get; set; }

    /// <summary>SHA256 checksum of uploaded file for integrity verification.</summary>
    public string? ChecksumSha256 { get; set; }
}