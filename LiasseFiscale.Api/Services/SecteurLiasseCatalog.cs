using LiasseFiscale.Api.Models;

namespace LiasseFiscale.Api.Services;

public record DefinitionEtatFinancier(
    string CodeDocument,
    string Libelle,
    FormatDocument Format,
    bool EstObligatoire
);

/// <summary>
/// Référentiel des états financiers requis par catégorie de liasse
/// (CasGeneral, Bancaire, AssurancesReassurances, Opcvm).
/// </summary>
public static class SecteurLiasseCatalog
{
    public static IReadOnlyList<DefinitionEtatFinancier> ObtenirEtatsRequis(CategorieLiasse categorie, ModeleF6004 modeleF6004 = ModeleF6004.Reference)
    {
        return categorie switch
        {
            // 1. Cas Général (Secteur Commercial/Industriel classique)
            CategorieLiasse.CasGeneral => new List<DefinitionEtatFinancier>
            {
                new("F6001", "Bilan Actif", FormatDocument.Xml, EstObligatoire: true),
                new("F6002", "Bilan Passif", FormatDocument.Xml, EstObligatoire: true),
                new("F6003", "État de résultat", FormatDocument.Xml, EstObligatoire: true),
                new(modeleF6004 == ModeleF6004.Autorise ? "F6004-MODELE-AUT" : "F6004",
                    modeleF6004 == ModeleF6004.Autorise ? "État de flux de trésorerie (Modèle autorisé)" : "État de flux de trésorerie (Modèle de référence)",
                    FormatDocument.Xml, EstObligatoire: true),
                new("F6005", "Tableau de détermination du résultat fiscal", FormatDocument.Xml, EstObligatoire: true),
                new("F6007", "Faits marquants de l'exercice", FormatDocument.Xml, EstObligatoire: false),
                new("F6019", "Notes et autres feuillets de l'annexe", FormatDocument.Pdf, EstObligatoire: false)
            },

            // 2. Secteur Bancaire et Établissements Financiers
            CategorieLiasse.Bancaire => new List<DefinitionEtatFinancier>
            {
                new("F6101", "Bilan Actifs-Passifs", FormatDocument.Xml, EstObligatoire: true),
                new("F6103", "État de résultat", FormatDocument.Xml, EstObligatoire: true),
                new("F6104", "État de flux de trésorerie", FormatDocument.Xml, EstObligatoire: true),
                new("F6105", "État des engagements hors bilan", FormatDocument.Xml, EstObligatoire: true),
                new("F6005", "Tableau de détermination du résultat fiscal", FormatDocument.Xml, EstObligatoire: true),
                new("F6007", "Faits marquants de l'exercice", FormatDocument.Xml, EstObligatoire: false),
                new("F6019", "Annexes", FormatDocument.Pdf, EstObligatoire: false)
            },

            // 3. Secteur des Assurances et Réassurances
            CategorieLiasse.AssurancesReassurances => new List<DefinitionEtatFinancier>
            {
                new("F6201", "Bilan Actif", FormatDocument.Xml, EstObligatoire: true),
                new("F6202", "Bilan Passif", FormatDocument.Xml, EstObligatoire: true),
                new("F6203", "État de résultat", FormatDocument.Xml, EstObligatoire: true),
                new("F6204", "État de flux de trésorerie (Méthode directe)", FormatDocument.Xml, EstObligatoire: true),
                new("F6205", "Résultat technique non-vie", FormatDocument.Xml, EstObligatoire: true),
                new("F6206", "Résultat technique vie", FormatDocument.Xml, EstObligatoire: true),
                new("F6207", "Tableau des engagements reçus et donnés", FormatDocument.Xml, EstObligatoire: true),
                new("F6005", "Tableau de détermination du résultat fiscal", FormatDocument.Xml, EstObligatoire: true)
            },

            // 4. Secteur des OPCVM (Organismes de Placement Collectif)
            CategorieLiasse.Opcvm => new List<DefinitionEtatFinancier>
            {
                new("F6301", "Bilan Actif-Passif", FormatDocument.Xml, EstObligatoire: true),
                new("F6303", "État de résultat", FormatDocument.Xml, EstObligatoire: true),
                new("F6304", "État de variation de l'actif net", FormatDocument.Xml, EstObligatoire: true),
                new("F6005", "Tableau de détermination du résultat fiscal", FormatDocument.Xml, EstObligatoire: true),
                new("F6006", "Notes et principes comptables appliqués", FormatDocument.Xml, EstObligatoire: true),
                new("F6007", "Faits marquants de l'exercice", FormatDocument.Xml, EstObligatoire: false)
            },

            _ => throw new NotSupportedException(
                $"La catégorie {categorie} n'est pas encore supportée (liste d'états financiers non documentée).")
        };
    }
}
