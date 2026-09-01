namespace LiasseFiscale.Api.Models;

/// <summary>
/// Catégorie de liasse fiscale — correspond exactement aux 6 options du menu déroulant
/// "Type de la liasse" de l'écran de dépôt (guide d'utilisation). Détermine la liste des
/// états financiers obligatoires/optionnels via SecteurLiasseCatalog.
/// </summary>
public enum CategorieLiasse
{
    CasGeneral,
    CasGeneralAvecFluxTresorerieModeleAutorise,
    Bancaire,
    AssurancesReassurances,
    Opcvm,

    /// <summary>
    /// Listée dans le menu déroulant du guide d'utilisation, mais sa liste d'états financiers
    /// n'est documentée nulle part dans les documents fournis jusqu'ici. Volontairement non
    /// supportée : SecteurLiasseCatalog lève une exception explicite plutôt que d'inventer une
    /// liste de documents non vérifiée.
    /// </summary>
    MicroCredits
}

/// <summary>T_NatureDepot : D = Définitif, P = Provisoire (nommé "TypeDepot" ici pour éviter la confusion
/// avec ActeDeDepot ci-dessous, qui correspond à une notion différente dans le cahier des charges).</summary>
public enum TypeDepot
{
    Definitif, // D
    Provisoire // P
}

/// <summary>
/// Nature de la liasse — 3ème dimension distincte de TypeDepot et ActeDeDepot, correspondant au
/// champ "Nature" de l'écran de dépôt (Initiale / Rectificative / Cessation d'activité).
/// Ne pas confondre avec T_NatureDepot (D/P) de l'entête XML, qui est TypeDepot ci-dessus.
/// </summary>
public enum NatureLiasse
{
    Initiale,
    Rectificative,
    CessationActivite
}

/// <summary>T_ActeDeDepot : 0 = Spontané, 1 = Rectification, 2 = Régularisation.</summary>
public enum ActeDeDepot
{
    Spontane = 0,
    Rectification = 1,
    Regularisation = 2
}

/// <summary>Modèle utilisé pour F6004 (cas général uniquement) : Référence ou Autorisé.</summary>
public enum ModeleF6004
{
    Reference,
    Autorise
}

public enum StatutLiasse
{
    /// <summary>Newly created, not yet populated with documents.</summary>
    Brouillon,

    /// <summary>In progress; documents being uploaded.</summary>
    EnSaisie,

    /// <summary>All documents uploaded; awaiting validation.</summary>
    EnAttenteDeValidation,

    /// <summary>Validation errors detected in one or more documents.</summary>
    EnErreur,

    /// <summary>Deposited/submitted locally; system-accepted.</summary>
    Deposee,

    /// <summary>Officially validated and accepted.</summary>
    Validee,

    /// <summary>Deleted by user while non-valid.</summary>
    Supprimee,

    /// <summary>Rejected by validation engine.</summary>
    Rejetee
}

public class Liasse
{
    public int Id { get; set; }

    public int ContribuableId { get; set; }
    public Contribuable Contribuable { get; set; } = null!;

    public CategorieLiasse Categorie { get; set; } = CategorieLiasse.CasGeneral;

    /// <summary>Exercice fiscal (T_Annee, format AAAA).</summary>
    public int Exercice { get; set; }

    public DateOnly DateDebutExercice { get; set; }
    public DateOnly DateClotureExercice { get; set; }

    public NatureLiasse Nature { get; set; } = NatureLiasse.Initiale;
    public ActeDeDepot ActeDeDepot { get; set; } = ActeDeDepot.Spontane;
    public TypeDepot TypeDepot { get; set; }

    /// <summary>
    /// Modèle F6004 effectivement utilisé. Ignoré si Categorie == CasGeneralAvecFluxTresorerieModeleAutorise
    /// (la catégorie impose alors Autorise) — voir LiasseService.CreerAsync.
    /// </summary>
    public ModeleF6004 ModeleF6004Choisi { get; set; } = ModeleF6004.Reference;

    public StatutLiasse Statut { get; set; } = StatutLiasse.Brouillon;

    public List<DocumentFiscal> Documents { get; set; } = new();

    /// <summary>User who created/submitted this liasse.</summary>
    public int? SubmittedBy { get; set; }
    public User? SubmittedByUser { get; set; }

    /// <summary>When the liasse was submitted for validation.</summary>
    public DateTime? DateSubmission { get; set; }

    /// <summary>User who validated/reviewed this liasse.</summary>
    public int? ReviewedBy { get; set; }
    public User? ReviewedByUser { get; set; }

    /// <summary>When the liasse was validated.</summary>
    public DateTime? DateReview { get; set; }

    /// <summary>Validation notes/comments from reviewer.</summary>
    public string? ReviewNotes { get; set; }

    public Deposit? Deposit { get; set; }

    public DateTime DateCreation { get; set; } = DateTime.UtcNow;
}