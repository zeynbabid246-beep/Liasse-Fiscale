using LiasseFiscale.Api.Models;

namespace LiasseFiscale.Api.Services;

public record CreerLiasseRequest(
    int ContribuableId,
    int Exercice,
    DateOnly DateDebutExercice,
    DateOnly DateClotureExercice,
    CategorieLiasse Categorie,
    NatureLiasse Nature,
    ActeDeDepot ActeDeDepot,
    TypeDepot TypeDepot,
    ModeleF6004 ModeleF6004Choisi);

public record BilanVerificationLiasse(
    int LiasseId,
    CategorieLiasse Categorie,
    bool PeutDeposer,
    int TotalObligatoires,
    int ObligatoiresValides,
    int TotalOptionnels,
    int OptionnelsDeposes,
    IReadOnlyList<string> DocumentsManquants,
    IReadOnlyList<string> DocumentsInvalides,
    IReadOnlyList<DocumentFiscal> Documents);

public interface ILiasseService
{
    Task<Liasse> CreerAsync(CreerLiasseRequest request);
    bool EstComplete(Liasse liasse);
    BilanVerificationLiasse VerifierLiasse(Liasse liasse);
    Task<bool> PeutCreerDepotProvisoireAsync(int contribuableId, int exercice);
    bool PeutSupprimer(Liasse liasse);
    bool PeutTransitionVers(Liasse liasse, StatutLiasse nouveauStatut);
    bool CombinaisonEstAutorisee(NatureLiasse nature, TypeDepot type, ActeDeDepot acte);
}
