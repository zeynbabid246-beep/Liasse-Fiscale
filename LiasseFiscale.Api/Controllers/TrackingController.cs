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

    /// <summary>
    /// Téléchargement d'un état financier déposé.
    /// </summary>
    [HttpGet("{reference}/documents/{codeDocument}/download")]
    [AllowAnonymous]
    public async Task<IActionResult> TelechargerDocument(string reference, string codeDocument)
    {
        var deposit = await _db.Deposits
            .Include(d => d.Liasse).ThenInclude(l => l.Documents)
            .FirstOrDefaultAsync(d => d.Reference == reference);

        if (deposit is null) return NotFound(new { message = "Dépôt introuvable." });

        var doc = deposit.Liasse.Documents.FirstOrDefault(d => d.CodeDocument.Equals(codeDocument, StringComparison.OrdinalIgnoreCase));
        if (doc is null || string.IsNullOrEmpty(doc.CheminStockage) || !System.IO.File.Exists(doc.CheminStockage))
        {
            return NotFound(new { message = "Fichier du document introuvable." });
        }

        var contentType = doc.Format == Models.FormatDocument.Pdf ? "application/pdf" : "application/xml";
        var bytes = await System.IO.File.ReadAllBytesAsync(doc.CheminStockage);
        return File(bytes, contentType, doc.NomFichier ?? $"{codeDocument}.xml");
    }

    /// <summary>
    /// Affichage HTML convivial de l'état financier déposé.
    /// </summary>
    [HttpGet("{reference}/documents/{codeDocument}/view")]
    [AllowAnonymous]
    public async Task<IActionResult> VoirDocument(string reference, string codeDocument)
    {
        var deposit = await _db.Deposits
            .Include(d => d.Liasse).ThenInclude(l => l.Contribuable)
            .Include(d => d.Liasse).ThenInclude(l => l.Documents)
            .FirstOrDefaultAsync(d => d.Reference == reference);

        if (deposit is null) return NotFound("Dépôt introuvable.");

        var doc = deposit.Liasse.Documents.FirstOrDefault(d => d.CodeDocument.Equals(codeDocument, StringComparison.OrdinalIgnoreCase));
        if (doc is null) return NotFound("Document introuvable.");

        string xmlContent = string.Empty;
        if (!string.IsNullOrEmpty(doc.CheminStockage) && System.IO.File.Exists(doc.CheminStockage))
        {
            xmlContent = await System.IO.File.ReadAllTextAsync(doc.CheminStockage);
        }

        var html = $@"<!DOCTYPE html>
<html lang=""fr"">
<head>
  <meta charset=""utf-8"">
  <title>{doc.CodeDocument} - Dépôt {deposit.Reference}</title>
  <style>
    body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; padding: 30px; color: #2b3a55; background: #f8fafc; line-height: 1.5; }}
    .container {{ max-width: 900px; margin: auto; background: #fff; border: 1px solid #dcdfe6; border-radius: 6px; box-shadow: 0 4px 12px rgba(0,0,0,0.06); padding: 30px; }}
    .header {{ border-bottom: 2px solid #2e7d32; padding-bottom: 15px; margin-bottom: 20px; display: flex; justify-content: space-between; align-items: center; }}
    .title {{ font-size: 18px; font-weight: 700; color: #2b3a55; }}
    .meta-box {{ background: #f4fbf5; border: 1px solid #c8e6c9; border-radius: 4px; padding: 14px 18px; display: grid; grid-template-columns: repeat(2, 1fr); gap: 10px; margin-bottom: 24px; font-size: 13px; }}
    .meta-label {{ color: #555; font-size: 11px; text-transform: uppercase; }}
    .meta-val {{ font-weight: 600; color: #2b3a55; }}
    .xml-raw {{ margin-top: 24px; background: #1e293b; color: #e2e8f0; padding: 16px; border-radius: 4px; font-family: monospace; font-size: 12px; overflow-x: auto; max-height: 500px; white-space: pre-wrap; }}
    .badge {{ display: inline-block; padding: 4px 10px; border-radius: 3px; font-size: 12px; font-weight: 600; background: #e8f5e9; color: #2e7d32; }}
    @media print {{ .no-print {{ display: none; }} }}
  </style>
</head>
<body>
  <div class=""container"">
    <div class=""no-print"" style=""text-align: right; margin-bottom: 15px;"">
      <button onclick=""window.print()"" style=""background:#2e7d32;color:#fff;border:none;padding:8px 16px;border-radius:4px;cursor:pointer;font-weight:600;"">🖨 Imprimer l'état déposé</button>
    </div>
    <div class=""header"">
      <div>
        <div style=""font-size:12px;font-weight:bold;color:#2e7d32;"">RÉPUBLIQUE TUNISIENNE • MINISTÈRE DES FINANCES</div>
        <div class=""title"">{doc.CodeDocument} : {doc.Libelle}</div>
      </div>
      <div style=""text-align: right;"">
        <span class=""badge"">✔ Dépôt Officiel : {deposit.Reference}</span>
      </div>
    </div>
    <div class=""meta-box"">
      <div><div class=""meta-label"">Raison Sociale</div><div class=""meta-val"">{deposit.Liasse.Contribuable.NomOuRaisonSociale}</div></div>
      <div><div class=""meta-label"">Matricule Fiscal</div><div class=""meta-val"">{deposit.Liasse.Contribuable.MatriculeFiscalComplet}</div></div>
      <div><div class=""meta-label"">Exercice Déposé</div><div class=""meta-val"">{deposit.Liasse.Exercice}</div></div>
      <div><div class=""meta-label"">Date de Dépôt</div><div class=""meta-val"">{deposit.DateDepot:dd/MM/yyyy HH:mm} UTC</div></div>
    </div>
    <h4 style=""font-size:13px; text-transform:uppercase; color:#2b3a55; margin-bottom:8px;"">Contenu Archivé de l'État Financier :</h4>
    <pre class=""xml-raw"">{(string.IsNullOrEmpty(xmlContent) ? ""Aucun contenu disponible."" : System.Net.WebUtility.HtmlEncode(xmlContent))}</pre>
  </div>
</body>
</html>";

        return Content(html, "text/html", System.Text.Encoding.UTF8);
    }
}
