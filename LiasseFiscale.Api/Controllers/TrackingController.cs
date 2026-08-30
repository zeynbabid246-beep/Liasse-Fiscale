using LiasseFiscale.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LiasseFiscale.Api.Controllers;

[ApiController]
[Route("api/deposits")]
[Authorize]
public class TrackingController : ControllerBase
{
    private readonly AppDbContext _db;

    public TrackingController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Recherche historique globale : Liste les dépôts de liasses transmis avec filtres par exercice et statut.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Lister([FromQuery] int? exercice, [FromQuery] string? statut)
    {
        var query = _db.Deposits
            .Include(d => d.Liasse).ThenInclude(l => l.Contribuable)
            .AsQueryable();

        if (exercice is not null)
        {
            query = query.Where(d => d.Liasse.Exercice == exercice);
        }

        if (!string.IsNullOrWhiteSpace(statut))
        {
            var statutNormalized = statut.Trim().ToUpperInvariant();
            query = query.Where(d => d.Liasse.Statut.ToString().ToUpper() == statutNormalized);
        }

        var resultats = await query
            .Select(d => new
            {
                d.Reference,
                d.DateDepot,
                Exercice = d.Liasse.Exercice,
                Contribuable = $"{d.Liasse.Contribuable.NomOuRaisonSociale} ({d.Liasse.Contribuable.MatriculeFiscalComplet})",
                Statut = d.Liasse.Statut.ToString(),
                Nature = d.Liasse.Nature.ToString(),
                ActeDeDepot = d.Liasse.ActeDeDepot.ToString(),
                TypeDepot = d.Liasse.TypeDepot.ToString(),
                Observation = d.Observation
            })
            .ToListAsync();

        return Ok(resultats);
    }

    /// <summary>
    /// Consultation détaillée d'un dépôt par sa référence unique et liste des états financiers associés.
    /// </summary>
    [HttpGet("{reference}")]
    public async Task<IActionResult> Details(string reference)
    {
        var deposit = await _db.Deposits
            .Include(d => d.Liasse).ThenInclude(l => l.Documents)
            .Include(d => d.Liasse).ThenInclude(l => l.Contribuable)
            .Include(d => d.Receipt)
            .FirstOrDefaultAsync(d => d.Reference == reference);

        if (deposit is null)
        {
            return NotFound(new { message = $"Dépôt avec référence '{reference}' introuvable." });
        }

        return Ok(new
        {
            deposit.Reference,
            deposit.DateDepot,
            Exercice = deposit.Liasse.Exercice,
            Contribuable = new
            {
                deposit.Liasse.Contribuable.NomOuRaisonSociale,
                MatriculeFiscal = deposit.Liasse.Contribuable.MatriculeFiscalComplet,
                deposit.Liasse.Contribuable.Activite,
                deposit.Liasse.Contribuable.Adresse
            },
            Statut = deposit.Liasse.Statut.ToString(),
            Categorie = deposit.Liasse.Categorie.ToString(),
            Nature = deposit.Liasse.Nature.ToString(),
            ActeDeDepot = deposit.Liasse.ActeDeDepot.ToString(),
            TypeDepot = deposit.Liasse.TypeDepot.ToString(),
            deposit.Observation,
            Documents = deposit.Liasse.Documents.Select(doc => new
            {
                doc.CodeDocument,
                doc.Libelle,
                Format = doc.Format.ToString(),
                doc.NomFichier,
                Statut = doc.Statut.ToString()
            }),
            AccuseDisponible = deposit.Receipt is not null
        });
    }
}
