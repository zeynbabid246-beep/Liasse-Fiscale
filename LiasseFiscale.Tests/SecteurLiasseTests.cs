using LiasseFiscale.Api.Models;
using LiasseFiscale.Api.Services;
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
}
