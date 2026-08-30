using LiasseFiscale.Api.Models;

namespace LiasseFiscale.Api.Services;

public interface IDepositService
{
    /// <summary>
    /// Confirme le dépôt officiel d'une liasse : vérifie la complétude, verrouille la liasse,
    /// génère une référence unique et horodate.
    /// </summary>
    Task<Deposit> ConfirmerDepotAsync(int liasseId, string? observation = null, string? signatureElectronique = null);
}
