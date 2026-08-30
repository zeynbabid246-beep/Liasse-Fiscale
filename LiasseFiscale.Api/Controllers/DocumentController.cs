using System.Text.RegularExpressions;
using LiasseFiscale.Api.Data;
using LiasseFiscale.Api.Models;
using LiasseFiscale.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LiasseFiscale.Api.Controllers;

[ApiController]
[Route("api/liasses/{liasseId}/documents")]
[Authorize]
public class DocumentController : ControllerBase
{
    private static readonly Regex NomFichierXmlRegex =
        new(@"^(?<code>F\d{4}(-MODELE-AUT)?)-(?<matricule>[0-9]{7}[A-Z]([A-Z0-9]{5})?)-(?<exercice>\d{4})\.xml$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex NomFichierPdfRegex =
        new(@"^(?<code>F6019)-(?<matricule>[0-9]{7}[A-Z]([A-Z0-9]{5})?)-(?<exercice>\d{4})\.pdf$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly AppDbContext _db;
    private readonly IXmlValidationService _xmlValidationService;
    private readonly IWebHostEnvironment _env;

    public DocumentController(
        AppDbContext db,
        IXmlValidationService xmlValidationService,
        IWebHostEnvironment env)
    {
        _db = db;
        _xmlValidationService = xmlValidationService;
        _env = env;
    }

    /// <summary>
    /// Téléversement d'un état financier XML ou d'une annexe PDF.
    /// Nom de fichier requis : [CODE]-[MATRICULE]-[EXERCICE].xml (ou .pdf).
    /// </summary>
    [HttpPost("{codeDocument}")]
    public async Task<IActionResult> Uploader(int liasseId, string codeDocument, IFormFile? file, IFormFile? fichier)
    {
        var uploadedFile = file ?? fichier;
        if (uploadedFile == null || uploadedFile.Length == 0)
        {
            return BadRequest(new { message = "Fichier vide ou manquant." });
        }

        var liasse = await _db.Liasses
            .Include(l => l.Contribuable)
            .Include(l => l.Documents)
            .FirstOrDefaultAsync(l => l.Id == liasseId);

        if (liasse is null)
        {
            return NotFound(new { message = "Liasse introuvable." });
        }

        var documentSlot = liasse.Documents.FirstOrDefault(d => d.CodeDocument.Equals(codeDocument, StringComparison.OrdinalIgnoreCase));
        if (documentSlot is null)
        {
            return BadRequest(new { message = $"'{codeDocument}' ne fait pas partie des états financiers attendus pour la catégorie {liasse.Categorie}." });
        }

        var extension = Path.GetExtension(uploadedFile.FileName).ToLowerInvariant();

        // --- CAS 1 : Upload PDF (ex: F6019 - Notes et Annexes) ---
        if (documentSlot.Format == FormatDocument.Pdf)
        {
            if (extension != ".pdf")
            {
                return BadRequest(new { message = "Pour ce document d'annexe, un fichier au format .pdf est attendu." });
            }

            var matchPdf = NomFichierPdfRegex.Match(uploadedFile.FileName);
            if (!matchPdf.Success)
            {
                return BadRequest(new { message = "Nom de fichier PDF invalide. Format attendu : F6019-[MATRICULE]-[EXERCICE].pdf (ex: F6019-1234567M-2026.pdf)" });
            }

            var matriculeDansNom = matchPdf.Groups["matricule"].Value.ToUpperInvariant();
            var matriculeCourt = matriculeDansNom.Length >= 8 ? matriculeDansNom.Substring(0, 8) : matriculeDansNom;
            var exerciceDansNom = int.Parse(matchPdf.Groups["exercice"].Value);

            if (!string.Equals(matriculeCourt, liasse.Contribuable.MatriculeCourt, StringComparison.OrdinalIgnoreCase) &&
                !liasse.Contribuable.MatriculeFiscalComplet.StartsWith(matriculeCourt, StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = $"Le matricule fiscal du nom de fichier ({matriculeDansNom}) ne correspond pas au contribuable de cette liasse ({liasse.Contribuable.MatriculeCourt})." });
            }

            if (exerciceDansNom != liasse.Exercice)
            {
                return BadRequest(new { message = $"L'exercice du nom de fichier ({exerciceDansNom}) ne correspond pas à l'exercice de cette liasse ({liasse.Exercice})." });
            }

            var dossierStockage = Path.Combine(_env.ContentRootPath, "Storage", "documents", liasseId.ToString());
            Directory.CreateDirectory(dossierStockage);
            var cheminStockage = Path.Combine(dossierStockage, uploadedFile.FileName);

            await using (var fileStream = System.IO.File.Create(cheminStockage))
            {
                await uploadedFile.CopyToAsync(fileStream);
            }

            documentSlot.NomFichier = uploadedFile.FileName;
            documentSlot.CheminStockage = cheminStockage;
            documentSlot.DateUpload = DateTime.UtcNow;
            documentSlot.Statut = StatutValidation.Valide;
            documentSlot.Erreurs.Clear();

            await _db.SaveChangesAsync();

            return Ok(new
            {
                documentSlot.CodeDocument,
                documentSlot.Libelle,
                Statut = documentSlot.Statut.ToString(),
                Erreurs = Array.Empty<object>()
            });
        }

        // --- CAS 2 : Upload XML (États financiers structurés) ---
        if (extension != ".xml")
        {
            return BadRequest(new { message = "Seuls les fichiers avec extension .xml sont acceptés pour cet état financier." });
        }

        var matchXml = NomFichierXmlRegex.Match(uploadedFile.FileName);
        if (!matchXml.Success)
        {
            return BadRequest(new { message = "Nom de fichier invalide. Format attendu : [CODE]-[MATRICULE]-[EXERCICE].xml (ex: F6001-1234567M-2026.xml)" });
        }

        var codeDansNomXml = matchXml.Groups["code"].Value.ToUpperInvariant();
        var matriculeDansNomXml = matchXml.Groups["matricule"].Value.ToUpperInvariant();
        var matriculeCourtXml = matriculeDansNomXml.Length >= 8 ? matriculeDansNomXml.Substring(0, 8) : matriculeDansNomXml;
        var exerciceDansNomXml = int.Parse(matchXml.Groups["exercice"].Value);

        if (!string.Equals(codeDansNomXml, codeDocument, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = $"Le code dans le nom de fichier ({codeDansNomXml}) ne correspond pas au document attendu ({codeDocument})." });
        }

        if (!string.Equals(matriculeCourtXml, liasse.Contribuable.MatriculeCourt, StringComparison.OrdinalIgnoreCase) &&
            !liasse.Contribuable.MatriculeFiscalComplet.StartsWith(matriculeCourtXml, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = $"Le matricule fiscal du nom de fichier ({matriculeDansNomXml}) ne correspond pas au contribuable de cette liasse ({liasse.Contribuable.MatriculeCourt})." });
        }

        if (exerciceDansNomXml != liasse.Exercice)
        {
            return BadRequest(new { message = $"L'exercice du nom de fichier ({exerciceDansNomXml}) ne correspond pas à l'exercice de cette liasse ({liasse.Exercice})." });
        }

        await using var stream = uploadedFile.OpenReadStream();
        var xmlData = XmlFieldExtractor.ExtraireTout(stream);

        var erreursCoherence = new List<ValidationIssue>();
        if (!string.IsNullOrEmpty(xmlData.Header.MatriculeFiscalDeclarant) &&
            !xmlData.Header.MatriculeFiscalDeclarant.StartsWith(matriculeCourtXml, StringComparison.OrdinalIgnoreCase))
        {
            erreursCoherence.Add(new ValidationIssue(
                "CoherenceEntete",
                "MatriculeFiscalDeclarant",
                null,
                $"Le matricule de l'entête XML ({xmlData.Header.MatriculeFiscalDeclarant}) ne correspond pas au nom de fichier ({matriculeDansNomXml})."
            ));
        }

        if (xmlData.Header.Exercice.HasValue && xmlData.Header.Exercice.Value != exerciceDansNomXml)
        {
            erreursCoherence.Add(new ValidationIssue(
                "CoherenceEntete",
                "Exercice",
                null,
                $"L'exercice de l'entête XML ({xmlData.Header.Exercice.Value}) ne correspond pas au nom de fichier ({exerciceDansNomXml})."
            ));
        }

        // Validation unifiée (Structurelle XSD 1.0 + Métier XPath)
        stream.Position = 0;
        var resultatValidation = _xmlValidationService.ValiderDocumentComplet(codeDocument, stream);

        var toutesErreurs = new List<ValidationIssue>(erreursCoherence);
        toutesErreurs.AddRange(resultatValidation.Erreurs);

        // Stockage physique
        var dossierStockageXml = Path.Combine(_env.ContentRootPath, "Storage", "documents", liasseId.ToString());
        Directory.CreateDirectory(dossierStockageXml);
        var cheminStockageXml = Path.Combine(dossierStockageXml, uploadedFile.FileName);

        await using (var fileStream = System.IO.File.Create(cheminStockageXml))
        {
            stream.Position = 0;
            await stream.CopyToAsync(fileStream);
        }

        documentSlot.NomFichier = uploadedFile.FileName;
        documentSlot.CheminStockage = cheminStockageXml;
        documentSlot.DateUpload = DateTime.UtcNow;
        documentSlot.Statut = toutesErreurs.Count == 0 ? StatutValidation.Valide : StatutValidation.Invalide;

        documentSlot.Erreurs = toutesErreurs
            .Select(e => new ValidationError
            {
                Source = e.Source == "Structurelle" ? SourceErreur.Structurelle : SourceErreur.RegleMetier,
                Champ = e.Champ,
                Ligne = e.Ligne,
                Message = e.Message
            })
            .ToList();

        await _db.SaveChangesAsync();

        return Ok(new
        {
            documentSlot.CodeDocument,
            documentSlot.Libelle,
            Statut = documentSlot.Statut.ToString(),
            Erreurs = toutesErreurs
        });
    }

    /// <summary>
    /// Détachement / suppression d'un fichier associé à un document de la liasse.
    /// </summary>
    [HttpDelete("{codeDocument}")]
    public async Task<IActionResult> Detacher(int liasseId, string codeDocument)
    {
        var liasse = await _db.Liasses
            .Include(l => l.Documents)
            .FirstOrDefaultAsync(l => l.Id == liasseId);

        if (liasse is null)
        {
            return NotFound(new { message = "Liasse introuvable." });
        }

        var documentSlot = liasse.Documents.FirstOrDefault(d => d.CodeDocument.Equals(codeDocument, StringComparison.OrdinalIgnoreCase));
        if (documentSlot is null)
        {
            return NotFound(new { message = "Document introuvable dans cette liasse." });
        }

        if (!string.IsNullOrEmpty(documentSlot.CheminStockage) && System.IO.File.Exists(documentSlot.CheminStockage))
        {
            try { System.IO.File.Delete(documentSlot.CheminStockage); } catch { }
        }

        documentSlot.NomFichier = null;
        documentSlot.CheminStockage = null;
        documentSlot.DateUpload = null;
        documentSlot.Statut = StatutValidation.NonSoumis;
        documentSlot.Erreurs.Clear();

        await _db.SaveChangesAsync();

        return Ok(new { message = $"Document {codeDocument} détaché avec succès." });
    }

    /// <summary>
    /// Téléchargement du document physique téléversé.
    /// </summary>
    [HttpGet("{codeDocument}/download")]
    [AllowAnonymous]
    public async Task<IActionResult> Telecharger(int liasseId, string codeDocument)
    {
        var liasse = await _db.Liasses
            .Include(l => l.Documents)
            .FirstOrDefaultAsync(l => l.Id == liasseId);

        if (liasse is null) return NotFound(new { message = "Liasse introuvable." });

        var doc = liasse.Documents.FirstOrDefault(d => d.CodeDocument.Equals(codeDocument, StringComparison.OrdinalIgnoreCase));
        if (doc is null || string.IsNullOrEmpty(doc.CheminStockage) || !System.IO.File.Exists(doc.CheminStockage))
        {
            return NotFound(new { message = "Fichier introuvable pour ce document." });
        }

        var contentType = doc.Format == FormatDocument.Pdf ? "application/pdf" : "application/xml";
        var fileBytes = await System.IO.File.ReadAllBytesAsync(doc.CheminStockage);
        return File(fileBytes, contentType, doc.NomFichier ?? $"{codeDocument}.xml");
    }

    /// <summary>
    /// Rendu HTML lisible et tabulaire de l'état financier.
    /// </summary>
    [HttpGet("{codeDocument}/html")]
    [AllowAnonymous]
    public async Task<IActionResult> MapperHtml(int liasseId, string codeDocument)
    {
        var liasse = await _db.Liasses
            .Include(l => l.Contribuable)
            .Include(l => l.Documents)
            .FirstOrDefaultAsync(l => l.Id == liasseId);

        if (liasse is null) return NotFound("Liasse introuvable.");

        var doc = liasse.Documents.FirstOrDefault(d => d.CodeDocument.Equals(codeDocument, StringComparison.OrdinalIgnoreCase));
        if (doc is null) return NotFound("Document introuvable.");

        string xmlContent = string.Empty;
        if (!string.IsNullOrEmpty(doc.CheminStockage) && System.IO.File.Exists(doc.CheminStockage))
        {
            xmlContent = await System.IO.File.ReadAllTextAsync(doc.CheminStockage);
        }

        string displayFileName = doc.NomFichier ?? "Non téléversé";
        string displayContent = string.IsNullOrEmpty(xmlContent) ? "Aucun contenu disponible." : System.Net.WebUtility.HtmlEncode(xmlContent);

        var html = $@"<!DOCTYPE html>
<html lang=""fr"">
<head>
  <meta charset=""utf-8"">
  <title>{doc.CodeDocument} - {doc.Libelle}</title>
  <style>
    body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; padding: 30px; color: #2b3a55; background: #f8fafc; line-height: 1.5; }}
    .container {{ max-width: 900px; margin: auto; background: #fff; border: 1px solid #dcdfe6; border-radius: 6px; box-shadow: 0 4px 12px rgba(0,0,0,0.06); padding: 30px; }}
    .header {{ border-bottom: 2px solid #d9531e; padding-bottom: 15px; margin-bottom: 20px; display: flex; justify-content: space-between; align-items: center; }}
    .logo-area {{ font-size: 13px; font-weight: bold; color: #d9531e; }}
    .title {{ font-size: 18px; font-weight: 700; color: #2b3a55; margin-top: 4px; }}
    .meta-box {{ background: #fdfaf8; border: 1px solid #f1ded4; border-radius: 4px; padding: 14px 18px; display: grid; grid-template-columns: repeat(2, 1fr); gap: 10px; margin-bottom: 24px; font-size: 13px; }}
    .meta-label {{ color: #666; font-size: 11.5px; text-transform: uppercase; }}
    .meta-val {{ font-weight: 600; color: #2b3a55; }}
    .xml-raw {{ margin-top: 24px; background: #1e293b; color: #e2e8f0; padding: 16px; border-radius: 4px; font-family: monospace; font-size: 12px; overflow-x: auto; max-height: 400px; white-space: pre-wrap; }}
    .badge {{ display: inline-block; padding: 3px 8px; border-radius: 3px; font-size: 11.5px; font-weight: 600; background: #e8f5e9; color: #2e7d32; }}
    @media print {{ .no-print {{ display: none; }} body {{ padding: 0; background: #fff; }} .container {{ box-shadow: none; border: none; padding: 0; }} }}
  </style>
</head>
<body>
  <div class=""container"">
    <div class=""no-print"" style=""text-align: right; margin-bottom: 15px;"">
      <button onclick=""window.print()"" style=""background:#d9531e;color:#fff;border:none;padding:8px 16px;border-radius:4px;cursor:pointer;font-weight:600;"">🖨 Imprimer l'état financier</button>
    </div>
    <div class=""header"">
      <div>
        <div class=""logo-area"">RÉPUBLIQUE TUNISIENNE • MINISTÈRE DES FINANCES</div>
        <div class=""title"">{doc.CodeDocument} : {doc.Libelle}</div>
      </div>
      <div style=""text-align: right;"">
        <span class=""badge"">✔ Statut : {doc.Statut}</span>
      </div>
    </div>
    <div class=""meta-box"">
      <div><div class=""meta-label"">Contribuable / Raison Sociale</div><div class=""meta-val"">{liasse.Contribuable.NomOuRaisonSociale}</div></div>
      <div><div class=""meta-label"">Matricule Fiscal</div><div class=""meta-val"">{liasse.Contribuable.MatriculeFiscalComplet}</div></div>
      <div><div class=""meta-label"">Exercice Comptable</div><div class=""meta-val"">{liasse.Exercice} ({liasse.DateDebut:dd/MM/yyyy} au {liasse.DateCloture:dd/MM/yyyy})</div></div>
      <div><div class=""meta-label"">Nom de Fichier</div><div class=""meta-val"">{displayFileName}</div></div>
    </div>
    <h4 style=""font-size:13px; text-transform:uppercase; color:#2b3a55; margin-bottom:8px;"">Contenu du Fichier :</h4>
    <pre class=""xml-raw"">{displayContent}</pre>
  </div>
</body>
</html>";

        return Content(html, "text/html", System.Text.Encoding.UTF8);
    }
}
