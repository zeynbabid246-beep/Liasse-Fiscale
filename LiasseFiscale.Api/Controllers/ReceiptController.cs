using LiasseFiscale.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LiasseFiscale.Api.Controllers;

[ApiController]
[Route("api/deposits/{reference}/receipt")]
[Authorize]
public class ReceiptController : ControllerBase
{
    private readonly AppDbContext _db;

    public ReceiptController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Téléchargement direct de l'accusé de réception officiel au format PDF.
    /// </summary>
    [HttpGet]
    [Produces("application/pdf")]
    public async Task<IActionResult> Telecharger(string reference)
    {
        var deposit = await _db.Deposits
            .Include(d => d.Receipt)
            .FirstOrDefaultAsync(d => d.Reference == reference);

        if (deposit?.Receipt is null || !System.IO.File.Exists(deposit.Receipt.CheminFichier))
        {
            return NotFound(new { message = $"Accusé de réception introuvable pour la référence '{reference}'." });
        }

        var bytes = await System.IO.File.ReadAllBytesAsync(deposit.Receipt.CheminFichier);
        return File(bytes, "application/pdf", $"accuse-{reference}.pdf");
    }
}
