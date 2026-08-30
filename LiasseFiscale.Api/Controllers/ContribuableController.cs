using LiasseFiscale.Api.Data;
using LiasseFiscale.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LiasseFiscale.Api.Controllers;

[ApiController]
[Route("api/contribuables")]
[Authorize]
public class ContribuableController : ControllerBase
{
    private readonly AppDbContext _db;

    public ContribuableController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Recherche un contribuable par son matricule fiscal et sa clé.
    /// </summary>
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string matricule, [FromQuery] string cle)
    {
        if (string.IsNullOrWhiteSpace(matricule) || string.IsNullOrWhiteSpace(cle))
        {
            return BadRequest(new { message = "Le matricule fiscal et la clé sont obligatoires." });
        }

        var num = matricule.Trim();
        var key = cle.Trim().ToUpperInvariant();

        var contribuable = await _db.Contribuables
            .FirstOrDefaultAsync(c => c.NumeroMatriculeFiscal == num && c.CleMatriculeFiscal == key);

        if (contribuable is null)
        {
            return NotFound(new { message = $"Aucun contribuable trouvé avec le matricule {num} {key}." });
        }

        return Ok(new
        {
            contribuable.Id,
            contribuable.NumeroMatriculeFiscal,
            contribuable.CleMatriculeFiscal,
            contribuable.MatriculeFiscalComplet,
            contribuable.NomOuRaisonSociale,
            contribuable.Adresse,
            contribuable.Activite,
            Categorie = contribuable.Categorie.ToString()
        });
    }

    /// <summary>
    /// Récupère les informations d'un contribuable par son identifiant.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var contribuable = await _db.Contribuables.FindAsync(id);
        if (contribuable is null) return NotFound();
        return Ok(contribuable);
    }
}
