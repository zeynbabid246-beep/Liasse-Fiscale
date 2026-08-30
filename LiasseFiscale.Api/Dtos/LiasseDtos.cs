using System.Text.Json.Serialization;
using LiasseFiscale.Api.Models;

namespace LiasseFiscale.Api.Dtos;

public class CreerLiasseDto
{
    [JsonPropertyName("contribuableId")]
    public object? ContribuableId { get; set; }

    [JsonPropertyName("exercice")]
    public int Exercice { get; set; } = 2026;

    [JsonPropertyName("dateDebutExercice")]
    public string DateDebutExercice { get; set; } = "2026-01-01";

    [JsonPropertyName("dateClotureExercice")]
    public string DateClotureExercice { get; set; } = "2026-12-31";

    [JsonPropertyName("categorie")]
    public string Categorie { get; set; } = "G";

    [JsonPropertyName("nature")]
    public string Nature { get; set; } = "I";

    [JsonPropertyName("acteDeDepot")]
    public object? ActeDeDepot { get; set; } = 0;

    [JsonPropertyName("typeDepot")]
    public string TypeDepot { get; set; } = "D";

    [JsonPropertyName("modeleF6004Choisi")]
    public object? ModeleF6004Choisi { get; set; } = 0;

    public CategorieLiasse GetCategorieEnum()
    {
        var val = Categorie?.Trim().ToUpperInvariant();
        return val switch
        {
            "G" or "CASGENERAL" => CategorieLiasse.CasGeneral,
            "B" or "BANCAIRE" => CategorieLiasse.Bancaire,
            "A" or "ASSURANCES" or "ASSURANCESREASSURANCES" => CategorieLiasse.AssurancesReassurances,
            "O" or "OPCVM" => CategorieLiasse.Opcvm,
            _ => CategorieLiasse.CasGeneral
        };
    }

    public NatureLiasse GetNatureEnum()
    {
        var val = Nature?.Trim().ToUpperInvariant();
        return val switch
        {
            "I" or "INITIALE" => NatureLiasse.Initiale,
            "R" or "RECTIFICATIVE" => NatureLiasse.Rectificative,
            "C" or "CESSATION" or "CESSATIONACTIVITE" => NatureLiasse.CessationActivite,
            _ => NatureLiasse.Initiale
        };
    }

    public TypeDepot GetTypeDepotEnum()
    {
        var val = TypeDepot?.Trim().ToUpperInvariant();
        return val switch
        {
            "D" or "DEFINITIF" => Models.TypeDepot.Definitif,
            "P" or "PROVISOIRE" => Models.TypeDepot.Provisoire,
            _ => Models.TypeDepot.Definitif
        };
    }

    public ActeDeDepot GetActeDeDepotEnum()
    {
        if (ActeDeDepot is null) return Models.ActeDeDepot.Spontane;
        var str = ActeDeDepot.ToString()?.Trim().ToUpperInvariant();
        return str switch
        {
            "0" or "SPONTANE" => Models.ActeDeDepot.Spontane,
            "1" or "RECTIFICATION" => Models.ActeDeDepot.Rectification,
            "2" or "REGULARISATION" => Models.ActeDeDepot.Regularisation,
            _ => Models.ActeDeDepot.Spontane
        };
    }

    public ModeleF6004 GetModeleF6004Enum()
    {
        if (ModeleF6004Choisi is null) return ModeleF6004.Reference;
        var str = ModeleF6004Choisi.ToString()?.Trim().ToUpperInvariant();
        return str switch
        {
            "1" or "AUTORISE" => ModeleF6004.Autorise,
            _ => ModeleF6004.Reference
        };
    }

    public DateOnly GetDateDebut() =>
        DateOnly.TryParse(DateDebutExercice, out var d) ? d : new DateOnly(Exercice, 1, 1);

    public DateOnly GetDateCloture() =>
        DateOnly.TryParse(DateClotureExercice, out var d) ? d : new DateOnly(Exercice, 12, 31);
}

public record DocumentStatutDto(
    string CodeDocument,
    string Libelle,
    string Format,
    bool EstObligatoire,
    string Statut,
    string? NomFichier);

public record BilanVerificationLiasseDto(
    int LiasseId,
    string Categorie,
    bool PeutDeposer,
    int TotalObligatoires,
    int ObligatoiresValides,
    int TotalOptionnels,
    int OptionnelsDeposes,
    IReadOnlyList<string> DocumentsManquants,
    IReadOnlyList<string> DocumentsInvalides,
    IReadOnlyList<DocumentStatutDto> Documents);

public record LiasseStatutDto(
    int Id,
    int Exercice,
    string Categorie,
    string Nature,
    string ActeDeDepot,
    string TypeDepot,
    string Statut,
    bool Complete,
    IReadOnlyList<DocumentStatutDto> Documents);

public record DefinitionEtatFinancierDto(
    string CodeDocument,
    string Libelle,
    string Format,
    bool EstObligatoire);

public record DepositRequest(
    [property: JsonPropertyName("observation")] string? Observation = null,
    [property: JsonPropertyName("signatureElectronique")] string? SignatureElectronique = null);