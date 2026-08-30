namespace LiasseFiscale.Api.Models;

public class Deposit
{
    public int Id { get; set; }

    public int LiasseId { get; set; }
    public Liasse Liasse { get; set; } = null!;

    /// <summary>Référence unique générée à la confirmation du dépôt.</summary>
    public string Reference { get; set; } = string.Empty;

    public DateTime DateDepot { get; set; }

    public string Observation { get; set; } = string.Empty;
    public string SignatureElectronique { get; set; } = string.Empty;

    public Receipt? Receipt { get; set; }
}

public class Receipt
{
    public int Id { get; set; }

    public int DepositId { get; set; }
    public Deposit Deposit { get; set; } = null!;

    /// <summary>Chemin du PDF généré pour l'accusé de réception.</summary>
    public string CheminFichier { get; set; } = string.Empty;

    public DateTime DateGeneration { get; set; } = DateTime.UtcNow;
}
