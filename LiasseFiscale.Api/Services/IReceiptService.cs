using LiasseFiscale.Api.Models;

namespace LiasseFiscale.Api.Services;

public interface IReceiptService
{
    /// <summary>
    /// Génère l'accusé de réception pour un dépôt confirmé. Version Jour 4 : un HTML simple
    /// suffisant pour la démo ; à remplacer par un vrai PDF si le "produit" l'exige explicitement.
    /// </summary>
    Task<Receipt> GenererAsync(Deposit deposit);
}
