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
}
