using LiasseFiscale.Api.Data;
using LiasseFiscale.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace LiasseFiscale.Api.Services;

public class LiasseService : ILiasseService
{
    private readonly AppDbContext _db;

    public LiasseService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Liasse> CreerAsync(CreerLiasseRequest request)
    {
        if (!CombinaisonEstAutorisee(request.Nature, request.TypeDepot, request.ActeDeDepot))
        {
            throw new InvalidOperationException(
                $"Combinaison non autorisée : Nature={request.Nature}, Type={request.TypeDepot}, Acte={request.ActeDeDepot}. " +
                "Voir la matrice des combinaisons de dépôt du cahier des charges.");
        }

        if (request.TypeDepot == TypeDepot.Provisoire &&
            !await PeutCreerDepotProvisoireAsync(request.ContribuableId, request.Exercice))
        {
            throw new InvalidOperationException(
                "Une liasse définitive existe déjà pour cet exercice : un dépôt provisoire n'est plus possible.");
        }

        var liasse = new Liasse
        {
            ContribuableId = request.ContribuableId,
            Exercice = request.Exercice,
            DateDebutExercice = request.DateDebutExercice,
            DateClotureExercice = request.DateClotureExercice,
            Categorie = request.Categorie,
            Nature = request.Nature,
            ActeDeDepot = request.ActeDeDepot,
            TypeDepot = request.TypeDepot,
            ModeleF6004Choisi = request.ModeleF6004Choisi,
            Statut = StatutLiasse.EnCoursDeSaisie
        };

        // Génère la liste des états financiers selon la catégorie / secteur
        var etatsRequis = SecteurLiasseCatalog.ObtenirEtatsRequis(request.Categorie, request.ModeleF6004Choisi);
        foreach (var etat in etatsRequis)
        {
            liasse.Documents.Add(new DocumentFiscal
            {
                CodeDocument = etat.CodeDocument,
                Libelle = etat.Libelle,
                Format = etat.Format,
                EstObligatoire = etat.EstObligatoire,
                Statut = StatutValidation.NonDepose
            });
        }

        _db.Liasses.Add(liasse);
        await _db.SaveChangesAsync();
        return liasse;
    }

    public bool EstComplete(Liasse liasse)
    {
        var obligatoires = liasse.Documents.Where(d => d.EstObligatoire).ToList();
        if (obligatoires.Count == 0) return false;
        return obligatoires.All(d => d.Statut == StatutValidation.Valide);
    }

    public BilanVerificationLiasse VerifierLiasse(Liasse liasse)
    {
        var obligatoires = liasse.Documents.Where(d => d.EstObligatoire).ToList();
        var optionnels = liasse.Documents.Where(d => !d.EstObligatoire).ToList();

        var manquants = obligatoires
            .Where(d => d.Statut == StatutValidation.NonDepose || d.Statut == StatutValidation.EnAttenteDeValidation)
            .Select(d => $"{d.CodeDocument} ({d.Libelle})")
            .ToList();

        var invalides = obligatoires
            .Where(d => d.Statut == StatutValidation.Invalide)
            .Select(d => $"{d.CodeDocument} ({d.Libelle})")
            .ToList();

        int obligatoiresValides = obligatoires.Count(d => d.Statut == StatutValidation.Valide);
        int optionnelsDeposes = optionnels.Count(d => d.Statut == StatutValidation.Valide);

        bool peutDeposer = obligatoires.Count > 0
            && obligatoiresValides == obligatoires.Count
            && invalides.Count == 0;

        return new BilanVerificationLiasse(
            LiasseId: liasse.Id,
            Categorie: liasse.Categorie,
            PeutDeposer: peutDeposer,
            TotalObligatoires: obligatoires.Count,
            ObligatoiresValides: obligatoiresValides,
            TotalOptionnels: optionnels.Count,
            OptionnelsDeposes: optionnelsDeposes,
            DocumentsManquants: manquants,
            DocumentsInvalides: invalides,
            Documents: liasse.Documents
        );
    }

    public async Task<bool> PeutCreerDepotProvisoireAsync(int contribuableId, int exercice)
    {
        var existeDefinitive = await _db.Liasses.AnyAsync(l =>
            l.ContribuableId == contribuableId &&
            l.Exercice == exercice &&
            l.TypeDepot == TypeDepot.Definitif &&
            l.Statut != StatutLiasse.Supprimee);

        return !existeDefinitive;
    }

    public bool CombinaisonEstAutorisee(NatureLiasse nature, TypeDepot type, ActeDeDepot acte)
    {
        return (nature, type, acte) switch
        {
            // 1. Initiale + Provisoire + Spontané
            (NatureLiasse.Initiale, TypeDepot.Provisoire, ActeDeDepot.Spontane) => true,
            // 2. Initiale + Définitif + Spontané
            (NatureLiasse.Initiale, TypeDepot.Definitif, ActeDeDepot.Spontane) => true,
            // 3. Rectificative + Provisoire
            (NatureLiasse.Rectificative, TypeDepot.Provisoire, ActeDeDepot.Rectification) => true,
            (NatureLiasse.Rectificative, TypeDepot.Provisoire, ActeDeDepot.Regularisation) => true,
            // 4. Rectificative + Définitif
            (NatureLiasse.Rectificative, TypeDepot.Definitif, ActeDeDepot.Rectification) => true,
            (NatureLiasse.Rectificative, TypeDepot.Definitif, ActeDeDepot.Regularisation) => true,
            // 5. Cessation d'activité + Définitif
            (NatureLiasse.CessationActivite, TypeDepot.Definitif, ActeDeDepot.Spontane) => true,
            (NatureLiasse.CessationActivite, TypeDepot.Definitif, ActeDeDepot.Regularisation) => true,
            _ => false
        };
    }
}