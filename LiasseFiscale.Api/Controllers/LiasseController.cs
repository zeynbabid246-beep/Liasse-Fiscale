using LiasseFiscale.Api.Data;
using LiasseFiscale.Api.Dtos;
using LiasseFiscale.Api.Models;
using LiasseFiscale.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LiasseFiscale.Api.Controllers;

[ApiController]
[Route("api/liasses")]
[Authorize]
public class LiasseController : ControllerBase
{
    private readonly ILiasseService _liasseService;
    private readonly AppDbContext _db;

    public LiasseController(ILiasseService liasseService, AppDbContext db)
    {
        _liasseService = liasseService;
        _db = db;
    }

    private async Task<bool> UserCanAccessContribuableAsync(int contribuableId)
    {
        var userId = HttpContext.GetUserId();
        if (userId <= 0)
        {
            return false;
        }

        return await _db.UserCompanyAuthorizations
            .AnyAsync(a => a.UserId == userId
                && a.ContribuableId == contribuableId
                && a.IsActive
                && (a.DateExpired == null || a.DateExpired > DateTime.UtcNow));
    }

    private async Task<bool> UserCanAccessLiasseAsync(int liasseId)
    {
        var liasse = await _db.Liasses.AsNoTracking().FirstOrDefaultAsync(l => l.Id == liasseId);
        if (liasse is null)
        {
            return false;
        }

        return await UserCanAccessContribuableAsync(liasse.ContribuableId);
    }

    /// <summary>
    /// Consulte la liste des états financiers (obligatoires et optionnels, XML ou PDF)
    /// requis pour une catégorie / secteur donné.
    /// </summary>
    [HttpGet("etats-requis")]
    public IActionResult ObtenirEtatsRequis([FromQuery] CategorieLiasse categorie, [FromQuery] ModeleF6004 modeleF6004 = ModeleF6004.Reference)
    {
        if (HttpContext.GetUserId() <= 0)
        {
            return Unauthorized();
        }

        var etats = SecteurLiasseCatalog.ObtenirEtatsRequis(categorie, modeleF6004)
            .Select(e => new DefinitionEtatFinancierDto(
                e.CodeDocument,
                e.Libelle,
                e.Format.ToString(),
                e.EstObligatoire))
            .ToList();

        return Ok(etats);
    }

    /// <summary>
    /// Crée et configure une nouvelle liasse fiscale pour un exercice donné.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Creer([FromBody] CreerLiasseDto dto)
    {
        try
        {
            int contribuableId = 0;
            var rawId = dto.ContribuableId?.ToString()?.Trim();

            if (int.TryParse(rawId, out int parsedId) && parsedId > 0 && await _db.Contribuables.AnyAsync(c => c.Id == parsedId))
            {
                contribuableId = parsedId;
            }
            else if (!string.IsNullOrEmpty(rawId) && rawId.Length >= 8)
            {
                var num = rawId.Substring(0, 7);
                var cle = rawId.Substring(7, 1).ToUpperInvariant();
                var contribuable = await _db.Contribuables
                    .FirstOrDefaultAsync(c => c.NumeroMatriculeFiscal == num && c.CleMatriculeFiscal == cle);

                if (contribuable is null)
                {
                    contribuable = new Contribuable
                    {
                        NumeroMatriculeFiscal = num,
                        CleMatriculeFiscal = cle,
                        NomOuRaisonSociale = $"Contribuable {num}{cle}",
                        CodeCategorie = rawId.Length >= 9 ? rawId.Substring(8, 1) : "M",
                        CodeTva = rawId.Length >= 10 ? rawId.Substring(9, 1) : "A"
                    };
                    _db.Contribuables.Add(contribuable);
                    await _db.SaveChangesAsync();
                }
                contribuableId = contribuable.Id;
            }
            else
            {
                var first = await _db.Contribuables.FirstOrDefaultAsync();
                if (first is null)
                {
                    first = new Contribuable
                    {
                        NumeroMatriculeFiscal = "1234567",
                        CleMatriculeFiscal = "M",
                        CodeCategorie = "M",
                        CodeTva = "A",
                        NomOuRaisonSociale = "SOCIETE COMMERCIALE TUNISIENNE SA"
                    };
                    _db.Contribuables.Add(first);
                    await _db.SaveChangesAsync();
                }
                contribuableId = first.Id;
            }

            var liasse = await _liasseService.CreerAsync(new CreerLiasseRequest(
                contribuableId,
                dto.Exercice,
                dto.GetDateDebut(),
                dto.GetDateCloture(),
                dto.GetCategorieEnum(),
                dto.GetNatureEnum(),
                dto.GetActeDeDepotEnum(),
                dto.GetTypeDepotEnum(),
                dto.GetModeleF6004Enum()));

            return CreatedAtAction(nameof(ObtenirStatut), new { id = liasse.Id }, new { liasse.Id });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Récupère la configuration et l'état des documents d'une liasse fiscale.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> ObtenirStatut(int id)
    {
        var liasse = await _db.Liasses
            .Include(l => l.Documents)
            .FirstOrDefaultAsync(l => l.Id == id);

        if (liasse is null)
        {
            return NotFound();
        }

        if (!await UserCanAccessContribuableAsync(liasse.ContribuableId))
        {
            return Forbid();
        }

        var dto = new LiasseStatutDto(
            liasse.Id,
            liasse.Exercice,
            liasse.Categorie.ToString(),
            liasse.Nature.ToString(),
            liasse.ActeDeDepot.ToString(),
            liasse.TypeDepot.ToString(),
            liasse.Statut.ToString(),
            _liasseService.EstComplete(liasse),
            liasse.Documents
                .Select(d => new DocumentStatutDto(d.CodeDocument, d.Libelle, d.Format.ToString(), d.EstObligatoire, d.Statut.ToString(), d.NomFichier))
                .ToList());

        return Ok(dto);
    }

    /// <summary>
    /// Bouton « Vérifier Liasse » : Contrôle la complétude de tous les états financiers obligatoires et optionnels.
    /// </summary>
    [HttpPost("{id}/verifier")]
    public async Task<IActionResult> VerifierLiasse(int id)
    {
        var liasse = await _db.Liasses
            .Include(l => l.Documents)
            .FirstOrDefaultAsync(l => l.Id == id);

        if (liasse is null)
        {
            return NotFound(new { message = "Liasse introuvable." });
        }

        if (!await UserCanAccessContribuableAsync(liasse.ContribuableId))
        {
            return Forbid();
        }

        var bilan = _liasseService.VerifierLiasse(liasse);

        var dto = new BilanVerificationLiasseDto(
            bilan.LiasseId,
            bilan.Categorie.ToString(),
            bilan.PeutDeposer,
            bilan.TotalObligatoires,
            bilan.ObligatoiresValides,
            bilan.TotalOptionnels,
            bilan.OptionnelsDeposes,
            bilan.DocumentsManquants,
            bilan.DocumentsInvalides,
            bilan.Documents
                .Select(d => new DocumentStatutDto(d.CodeDocument, d.Libelle, d.Format.ToString(), d.EstObligatoire, d.Statut.ToString(), d.NomFichier))
                .ToList()
        );

        return Ok(dto);
    }

    /// <summary>
    /// Liste les liasses en cours de saisie pour un contribuable.
    /// </summary>
    [HttpGet("en-cours")]
    public async Task<IActionResult> ObtenirEnCours([FromQuery] int contribuableId)
    {
        if (!await UserCanAccessContribuableAsync(contribuableId))
        {
            return Forbid();
        }

        var liasses = await _db.Liasses
            .Include(l => l.Documents)
            .Include(l => l.Contribuable)
            .Where(l => l.ContribuableId == contribuableId && 
                       (l.Statut == StatutLiasse.Brouillon || l.Statut == StatutLiasse.EnSaisie || l.Statut == StatutLiasse.EnErreur))
            .OrderByDescending(l => l.DateCreation)
            .Select(l => new
            {
                l.Id,
                l.Exercice,
                Categorie = l.Categorie.ToString(),
                Nature = l.Nature.ToString(),
                TypeDepot = l.TypeDepot.ToString(),
                Statut = l.Statut.ToString(),
                l.DateCreation,
                TotalDocuments = l.Documents.Count,
                DocumentsUploade = l.Documents.Count(d => d.NomFichier != null),
                EstPretPourDepot = l.Documents.Where(d => d.EstObligatoire).All(d => d.Statut == StatutValidation.Valide),
                Documents = l.Documents.Select(d => new
                {
                    d.CodeDocument,
                    d.Libelle,
                    Format = d.Format.ToString(),
                    d.EstObligatoire,
                    Statut = d.Statut.ToString(),
                    d.NomFichier
                }).ToList()
            })
            .ToListAsync();

        return Ok(liasses);
    }

    /// <summary>
    /// Suppression d'une liasse en cours de saisie.
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> SupprimerLiasse(int id)
    {
        var liasse = await _db.Liasses.FirstOrDefaultAsync(l => l.Id == id);
        if (liasse is null)
        {
            return NotFound(new { message = "Liasse introuvable." });
        }

        if (!await UserCanAccessContribuableAsync(liasse.ContribuableId))
        {
            return Forbid();
        }

        if (!_liasseService.PeutSupprimer(liasse))
        {
            return Conflict(new
            {
                message = "Une liasse déjà validée ou déposée ne peut pas être supprimée. " +
                          "Vous devez d'abord créer une nouvelle liasse correcte ou annuler le dépôt dans le workflow applicatif."
            });
        }

        liasse.Statut = StatutLiasse.Supprimee;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Liasse en cours supprimée avec succès." });
    }
}
