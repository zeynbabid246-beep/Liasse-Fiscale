using LiasseFiscale.Api.Data;
using LiasseFiscale.Api.Models;
using LiasseFiscale.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LiasseFiscale.Tests;

public class SecteurLiasseTests
{
    [Fact]
    public void CasGeneral_Contient5ObligatoiresXmlEt2Optionnels()
    {
        var etats = SecteurLiasseCatalog.ObtenirEtatsRequis(CategorieLiasse.CasGeneral, ModeleF6004.Reference);

        var obligatoires = etats.Where(e => e.EstObligatoire).ToList();
        var optionnels = etats.Where(e => !e.EstObligatoire).ToList();

        Assert.Equal(5, obligatoires.Count);
        Assert.Contains(obligatoires, e => e.CodeDocument == "F6001" && e.Format == FormatDocument.Xml);
        Assert.Contains(obligatoires, e => e.CodeDocument == "F6002" && e.Format == FormatDocument.Xml);
        Assert.Contains(obligatoires, e => e.CodeDocument == "F6003" && e.Format == FormatDocument.Xml);
        Assert.Contains(obligatoires, e => e.CodeDocument == "F6004" && e.Format == FormatDocument.Xml);
        Assert.Contains(obligatoires, e => e.CodeDocument == "F6005" && e.Format == FormatDocument.Xml);

        Assert.Equal(2, optionnels.Count);
        Assert.Contains(optionnels, e => e.CodeDocument == "F6007" && e.Format == FormatDocument.Xml);
        Assert.Contains(optionnels, e => e.CodeDocument == "F6019" && e.Format == FormatDocument.Pdf);
    }

    [Fact]
    public void CasGeneral_ModeleAutorise_UtiliseF6004ModeleAut()
    {
        var etats = SecteurLiasseCatalog.ObtenirEtatsRequis(CategorieLiasse.CasGeneral, ModeleF6004.Autorise);
        Assert.Contains(etats, e => e.CodeDocument == "F6004-MODELE-AUT");
    }

    [Fact]
    public void SecteurBancaire_Contient5ObligatoiresXmlEt2Optionnels()
    {
        var etats = SecteurLiasseCatalog.ObtenirEtatsRequis(CategorieLiasse.Bancaire);

        var obligatoires = etats.Where(e => e.EstObligatoire).ToList();
        var optionnels = etats.Where(e => !e.EstObligatoire).ToList();

        Assert.Equal(5, obligatoires.Count);
        Assert.Contains(obligatoires, e => e.CodeDocument == "F6101" && e.Format == FormatDocument.Xml);
        Assert.Contains(obligatoires, e => e.CodeDocument == "F6103" && e.Format == FormatDocument.Xml);
        Assert.Contains(obligatoires, e => e.CodeDocument == "F6104" && e.Format == FormatDocument.Xml);
        Assert.Contains(obligatoires, e => e.CodeDocument == "F6105" && e.Format == FormatDocument.Xml);
        Assert.Contains(obligatoires, e => e.CodeDocument == "F6005" && e.Format == FormatDocument.Xml);

        Assert.Equal(2, optionnels.Count);
        Assert.Contains(optionnels, e => e.CodeDocument == "F6007" && e.Format == FormatDocument.Xml);
        Assert.Contains(optionnels, e => e.CodeDocument == "F6019" && e.Format == FormatDocument.Pdf);
    }

    [Fact]
    public void SecteurAssurances_Contient8ObligatoiresXml()
    {
        var etats = SecteurLiasseCatalog.ObtenirEtatsRequis(CategorieLiasse.AssurancesReassurances);

        var obligatoires = etats.Where(e => e.EstObligatoire).ToList();
        var optionnels = etats.Where(e => !e.EstObligatoire).ToList();

        Assert.Equal(8, obligatoires.Count);
        Assert.Contains(obligatoires, e => e.CodeDocument == "F6201");
        Assert.Contains(obligatoires, e => e.CodeDocument == "F6202");
        Assert.Contains(obligatoires, e => e.CodeDocument == "F6203");
        Assert.Contains(obligatoires, e => e.CodeDocument == "F6204");
        Assert.Contains(obligatoires, e => e.CodeDocument == "F6205");
        Assert.Contains(obligatoires, e => e.CodeDocument == "F6206");
        Assert.Contains(obligatoires, e => e.CodeDocument == "F6207");
        Assert.Contains(obligatoires, e => e.CodeDocument == "F6005");

        Assert.Empty(optionnels);
    }

    [Fact]
    public void SecteurOPCVM_Contient5ObligatoiresXmlEt1Optionnel()
    {
        var etats = SecteurLiasseCatalog.ObtenirEtatsRequis(CategorieLiasse.Opcvm);

        var obligatoires = etats.Where(e => e.EstObligatoire).ToList();
        var optionnels = etats.Where(e => !e.EstObligatoire).ToList();

        Assert.Equal(5, obligatoires.Count);
        Assert.Contains(obligatoires, e => e.CodeDocument == "F6301");
        Assert.Contains(obligatoires, e => e.CodeDocument == "F6303");
        Assert.Contains(obligatoires, e => e.CodeDocument == "F6304");
        Assert.Contains(obligatoires, e => e.CodeDocument == "F6005");
        Assert.Contains(obligatoires, e => e.CodeDocument == "F6006");

        Assert.Single(optionnels);
        Assert.Contains(optionnels, e => e.CodeDocument == "F6007" && e.Format == FormatDocument.Xml);
    }

    [Fact]
    public void VerifierLiasse_DetecteDocumentsManquantsEtBloqueDepot()
    {
        var liasse = new Liasse
        {
            Id = 1,
            Categorie = CategorieLiasse.CasGeneral,
            Documents = new List<DocumentFiscal>
            {
                new() { CodeDocument = "F6001", Libelle = "Bilan Actif", EstObligatoire = true, Statut = StatutValidation.Valide },
                new() { CodeDocument = "F6002", Libelle = "Bilan Passif", EstObligatoire = true, Statut = StatutValidation.NonDepose },
                new() { CodeDocument = "F6003", Libelle = "État de résultat", EstObligatoire = true, Statut = StatutValidation.Valide },
                new() { CodeDocument = "F6004", Libelle = "Flux trésorerie", EstObligatoire = true, Statut = StatutValidation.Valide },
                new() { CodeDocument = "F6005", Libelle = "Résultat fiscal", EstObligatoire = true, Statut = StatutValidation.Valide },
                new() { CodeDocument = "F6007", Libelle = "Faits marquants", EstObligatoire = false, Statut = StatutValidation.NonDepose },
                new() { CodeDocument = "F6019", Libelle = "Annexes PDF", Format = FormatDocument.Pdf, EstObligatoire = false, Statut = StatutValidation.NonDepose }
            }
        };

        var service = new LiasseService(null!);
        var bilan = service.VerifierLiasse(liasse);

        Assert.False(bilan.PeutDeposer);
        Assert.Equal(5, bilan.TotalObligatoires);
        Assert.Equal(4, bilan.ObligatoiresValides);
        Assert.Single(bilan.DocumentsManquants);
        Assert.Contains("F6002", bilan.DocumentsManquants[0]);
    }

    [Fact]
    public void VerifierLiasse_TousObligatoiresValides_AutoriseDepotMemeSansOptionnels()
    {
        var liasse = new Liasse
        {
            Id = 1,
            Categorie = CategorieLiasse.CasGeneral,
            Documents = new List<DocumentFiscal>
            {
                new() { CodeDocument = "F6001", Libelle = "Bilan Actif", EstObligatoire = true, Statut = StatutValidation.Valide },
                new() { CodeDocument = "F6002", Libelle = "Bilan Passif", EstObligatoire = true, Statut = StatutValidation.Valide },
                new() { CodeDocument = "F6003", Libelle = "État de résultat", EstObligatoire = true, Statut = StatutValidation.Valide },
                new() { CodeDocument = "F6004", Libelle = "Flux trésorerie", EstObligatoire = true, Statut = StatutValidation.Valide },
                new() { CodeDocument = "F6005", Libelle = "Résultat fiscal", EstObligatoire = true, Statut = StatutValidation.Valide },
                new() { CodeDocument = "F6007", Libelle = "Faits marquants", EstObligatoire = false, Statut = StatutValidation.NonDepose },
                new() { CodeDocument = "F6019", Libelle = "Annexes PDF", Format = FormatDocument.Pdf, EstObligatoire = false, Statut = StatutValidation.NonDepose }
            }
        };

        var service = new LiasseService(null!);
        var bilan = service.VerifierLiasse(liasse);

        Assert.True(bilan.PeutDeposer);
        Assert.Empty(bilan.DocumentsManquants);
        Assert.Empty(bilan.DocumentsInvalides);
    }

    [Fact]
    public async Task PeutCreerDepotProvisoireAsync_QuandDefinitiveExiste_RetourneFalse()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new AppDbContext(options);
        db.Contribuables.Add(new Contribuable
        {
            NumeroMatriculeFiscal = "1234567",
            CleMatriculeFiscal = "M",
            NomOuRaisonSociale = "Societe Test",
            CodeCategorie = "M",
            CodeTva = "A"
        });
        await db.SaveChangesAsync();

        var contribuable = await db.Contribuables.FirstAsync();
        db.Liasses.Add(new Liasse
        {
            ContribuableId = contribuable.Id,
            Exercice = 2026,
            DateDebutExercice = new DateOnly(2026, 1, 1),
            DateClotureExercice = new DateOnly(2026, 12, 31),
            Categorie = CategorieLiasse.CasGeneral,
            Nature = NatureLiasse.Initiale,
            ActeDeDepot = ActeDeDepot.Spontane,
            TypeDepot = TypeDepot.Definitif,
            Statut = StatutLiasse.Validee
        });
        await db.SaveChangesAsync();

        var service = new LiasseService(db);
        var peutCreer = await service.PeutCreerDepotProvisoireAsync(contribuable.Id, 2026);

        Assert.False(peutCreer);
    }

    [Fact]
    public void PeutSupprimer_LiasseValidee_RetourneFalse()
    {
        var service = new LiasseService(null!);
        var liasse = new Liasse { Statut = StatutLiasse.Validee };

        Assert.False(service.PeutSupprimer(liasse));
    }

    [Fact]
    public void PeutTransitionVers_TransitionsValideesAutorisees()
    {
        var service = new LiasseService(null!);
        var liasse = new Liasse { Statut = StatutLiasse.EnSaisie };

        Assert.True(service.PeutTransitionVers(liasse, StatutLiasse.EnAttenteDeValidation));
        Assert.True(service.PeutTransitionVers(liasse, StatutLiasse.EnErreur));
        Assert.False(service.PeutTransitionVers(liasse, StatutLiasse.Validee));
    }
}
