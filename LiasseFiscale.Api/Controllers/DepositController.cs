using LiasseFiscale.Api.Dtos;
using LiasseFiscale.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LiasseFiscale.Api.Controllers;

[ApiController]
[Route("api/liasses/{liasseId}/deposit")]
[Authorize]
public class DepositController : ControllerBase
{
    private readonly IDepositService _depositService;
    private readonly IReceiptService _receiptService;

    public DepositController(IDepositService depositService, IReceiptService receiptService)
    {
        _depositService = depositService;
        _receiptService = receiptService;
    }

    /// <summary>
    /// Verrouillage et dépôt officiel de la liasse fiscale (avec observation et signature électronique).
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Confirmer(int liasseId, [FromBody] DepositRequest? request = null)
    {
        try
        {
            var deposit = await _depositService.ConfirmerDepotAsync(
                liasseId,
                request?.Observation,
                request?.SignatureElectronique);

            await _receiptService.GenererAsync(deposit);
            return Ok(new
            {
                deposit.Reference,
                deposit.DateDepot,
                Message = "Liasse fiscale déposée et validée avec succès."
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = $"Liasse {liasseId} introuvable." });
        }
    }
}
