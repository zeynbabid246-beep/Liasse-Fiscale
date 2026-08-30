using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using LiasseFiscale.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LiasseFiscale.Api.Controllers;

public class ValidationErrorItemDto
{
    [JsonPropertyName("line")]
    public int? Line { get; set; }

    [JsonPropertyName("ligne")]
    public int? Ligne => Line;

    [JsonPropertyName("xmlElement")]
    public string? XmlElement { get; set; }

    [JsonPropertyName("champ")]
    public string? Champ => XmlElement;

    [JsonPropertyName("severity")]
    public string Severity { get; set; } = "Critical";

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;
}

public class ValidationResponseDto
{
    [JsonPropertyName("isValid")]
    public bool IsValid { get; set; }

    [JsonPropertyName("estValide")]
    public bool EstValide => IsValid;

    [JsonPropertyName("errors")]
    public List<ValidationErrorItemDto> Errors { get; set; } = new();

    [JsonPropertyName("erreurs")]
    public List<ValidationErrorItemDto> Erreurs => Errors;
}

/// <summary>
/// Endpoint de validation "à blanc" : vérifie la conformité structurelle, les règles métier
/// et la cohérence de nommage / entête XML sans persistance en base.
/// </summary>
[Authorize]
[ApiController]
[Route("api/validation")]
public class ValidationController : ControllerBase
{
    private static readonly Regex NomFichierRegex =
        new(@"^(?<code>F\d{4}(-MODELE-AUT)?)-(?<matricule>[0-9]{7}[A-Z]([A-Z0-9]{5})?)-(?<exercice>\d{4})\.xml$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly IXmlValidationService _xmlValidationService;

    public ValidationController(IXmlValidationService xmlValidationService)
    {
        _xmlValidationService = xmlValidationService;
    }

    /// <summary>
    /// Validation technique d'un fichier XML (XSD à la volée + règles métier).
    /// </summary>
    [HttpPost("{codeDocument}")]
    public async Task<IActionResult> Valider(string codeDocument, IFormFile? file, IFormFile? fichier)
    {
        var uploadedFile = file ?? fichier;
        if (uploadedFile == null || uploadedFile.Length == 0)
        {
            return BadRequest(new { message = "Fichier vide ou manquant." });
        }

        if (!Path.GetExtension(uploadedFile.FileName).Equals(".xml", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Seuls les fichiers avec extension .xml sont acceptés." });
        }

        await using var stream = uploadedFile.OpenReadStream();

        var match = NomFichierRegex.Match(uploadedFile.FileName);
        var issues = new List<ValidationIssue>();

        if (match.Success)
        {
            var codeDansNom = match.Groups["code"].Value;
            var matriculeDansNom = match.Groups["matricule"].Value;
            var matriculeCourt = matriculeDansNom.Length >= 8 ? matriculeDansNom.Substring(0, 8) : matriculeDansNom;
            var exerciceDansNom = int.Parse(match.Groups["exercice"].Value);

            if (!string.Equals(codeDansNom, codeDocument, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue("CoherenceFichier", null, null,
                    $"Le code formulaire dans le nom de fichier ({codeDansNom}) ne correspond pas au document ciblé ({codeDocument})."));
            }

            var xmlData = XmlFieldExtractor.ExtraireTout(stream);
            if (xmlData.Header.MatriculeFiscalDeclarant is not null &&
                !xmlData.Header.MatriculeFiscalDeclarant.StartsWith(matriculeCourt, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue("CoherenceFichier", "MatriculeFiscalDeclarant", null,
                    $"Le matricule fiscal de l'entête XML ({xmlData.Header.MatriculeFiscalDeclarant}) ne correspond pas au matricule du nom de fichier ({matriculeDansNom})."));
            }

            if (xmlData.Header.Exercice.HasValue && xmlData.Header.Exercice.Value != exerciceDansNom)
            {
                issues.Add(new ValidationIssue("CoherenceFichier", "Exercice", null,
                    $"L'exercice de l'entête XML ({xmlData.Header.Exercice.Value}) ne correspond pas à l'exercice du nom de fichier ({exerciceDansNom})."));
            }
        }

        // Validation complète (XSD 1.0 + Moteur XPath métier)
        stream.Position = 0;
        var resultatComplet = _xmlValidationService.ValiderDocumentComplet(codeDocument, stream);
        issues.AddRange(resultatComplet.Erreurs);

        var response = new ValidationResponseDto
        {
            IsValid = issues.Count == 0,
            Errors = issues.Select(i => new ValidationErrorItemDto
            {
                Line = i.Ligne,
                XmlElement = i.Champ,
                Severity = "Critical",
                Message = i.Message,
                Source = i.Source
            }).ToList()
        };

        return Ok(response);
    }
}
