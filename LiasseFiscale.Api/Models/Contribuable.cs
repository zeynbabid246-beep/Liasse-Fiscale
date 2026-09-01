namespace LiasseFiscale.Api.Models;

/// <summary>
/// Catégorie du contribuable, telle qu'utilisée dans les assertions conditionnelles de F6005
/// (T_CodeCategorie du cahier des charges : C, M, N, P).
/// </summary>
public enum CategorieContribuable
{
    PersonneMoraleCommercialeIndustrielle, // C
    PersonneMorale,                        // M
    EmployeurNonSoumisImpotDirect,          // N
    PersonnePhysiqueProfessionLiberale      // P
}

public class Contribuable
{
    public int Id { get; set; }

    /// <summary>Numéro à 7 chiffres (T_NumMatriculeFiscal).</summary>
    public string NumeroMatriculeFiscal { get; set; } = string.Empty;

    /// <summary>Clé du matricule (1 lettre, T_CleMatriculeFiscal).</summary>
    public string CleMatriculeFiscal { get; set; } = string.Empty;

    public string CodeCategorie { get; set; } = "M"; // M, C, P, N
    public string CodeTva { get; set; } = "A";        // A, B, P, D, N

    public string NomOuRaisonSociale { get; set; } = string.Empty;
    public string Activite { get; set; } = string.Empty;
    public string Adresse { get; set; } = string.Empty;

    public CategorieContribuable Categorie { get; set; } = CategorieContribuable.PersonneMorale;

    /// <summary>Users authorized to act for this company.</summary>
    public List<UserCompanyAuthorization> UserAuthorizations { get; set; } = new();

    /// <summary>Liasses filed by/for this taxpayer.</summary>
    public List<Liasse> Liasses { get; set; } = new();

    /// <summary>Matricule fiscal complet à 13 caractères (T_MatriculeFiscal_13c), ex: 1234567MAM000.</summary>
    public string MatriculeFiscalComplet => $"{NumeroMatriculeFiscal}{CleMatriculeFiscal}{CodeCategorie}{CodeTva}000";

    /// <summary>Matricule fiscal court (8 caractères : 7 chiffres + 1 clé).</summary>
    public string MatriculeCourt => $"{NumeroMatriculeFiscal}{CleMatriculeFiscal}";
}
