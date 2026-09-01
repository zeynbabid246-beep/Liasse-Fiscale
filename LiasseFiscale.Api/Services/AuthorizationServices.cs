using LiasseFiscale.Api.Data;
using LiasseFiscale.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace LiasseFiscale.Api.Services;

/// <summary>
/// Service for authorization checks and company access control.
/// </summary>
public interface IAuthorizationService
{
    /// <summary>
    /// Check if a user is authorized to access a specific company.
    /// </summary>
    Task<bool> IsAuthorizedForCompanyAsync(int userId, int contribuableId);

    /// <summary>
    /// Get all companies a user is authorized for.
    /// </summary>
    Task<List<Contribuable>> GetAuthorizedCompaniesAsync(int userId);

    /// <summary>
    /// Get specific authorization details.
    /// </summary>
    Task<UserCompanyAuthorization?> GetAuthorizationAsync(int userId, int contribuableId);

    /// <summary>
    /// Check if user has specific permission for a company.
    /// </summary>
    Task<bool> HasPermissionAsync(int userId, int contribuableId, string permission);
}

public class AuthorizationService : IAuthorizationService
{
    private readonly AppDbContext _db;

    public AuthorizationService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<bool> IsAuthorizedForCompanyAsync(int userId, int contribuableId)
    {
        return await _db.UserCompanyAuthorizations
            .Where(a => a.UserId == userId && a.ContribuableId == contribuableId && a.IsActive &&
                   (a.DateExpired == null || a.DateExpired > DateTime.UtcNow))
            .AnyAsync();
    }

    public async Task<List<Contribuable>> GetAuthorizedCompaniesAsync(int userId)
    {
        return await _db.UserCompanyAuthorizations
            .Where(a => a.UserId == userId && a.IsActive &&
                   (a.DateExpired == null || a.DateExpired > DateTime.UtcNow))
            .Select(a => a.Contribuable)
            .Distinct()
            .ToListAsync();
    }

    public async Task<UserCompanyAuthorization?> GetAuthorizationAsync(int userId, int contribuableId)
    {
        return await _db.UserCompanyAuthorizations
            .FirstOrDefaultAsync(a => a.UserId == userId && a.ContribuableId == contribuableId &&
                                   a.IsActive &&
                                   (a.DateExpired == null || a.DateExpired > DateTime.UtcNow));
    }

    public async Task<bool> HasPermissionAsync(int userId, int contribuableId, string permission)
    {
        var auth = await GetAuthorizationAsync(userId, contribuableId);
        return auth?.HasPermission(permission) ?? false;
    }
}

/// <summary>
/// Service for managing Liasse deposits with business rule enforcement.
/// Handles all deposit creation, state management, and validation.
/// </summary>
public interface ILiasseManagementService
{
    /// <summary>
    /// Create a new liasse with business rule validation.
    /// Enforces: no Provisional after Definitive, Rectification requires prior Spontané, etc.
    /// </summary>
    Task<Liasse> CreateLiasseAsync(int contribuableId, int exercice, DateOnly dateDebut, DateOnly dateCloture,
        CategorieLiasse categorie, NatureLiasse nature, ActeDeDepot acte, TypeDepot typeDepot, int submittedBy);

    /// <summary>
    /// Check if creating a deposit violates business rules.
    /// </summary>
    Task<(bool isValid, string? errorMessage)> ValidateNewDepositAsync(int contribuableId, int exercice,
        ActeDeDepot acte, TypeDepot typeDepot);

    /// <summary>
    /// Get existing deposits for a taxpayer's fiscal year.
    /// </summary>
    Task<List<Liasse>> GetExistingDepositsAsync(int contribuableId, int exercice, ActeDeDepot acte);
}

public class LiasseManagementService : ILiasseManagementService
{
    private readonly AppDbContext _db;
    private readonly IAuditService _auditService;

    public LiasseManagementService(AppDbContext db, IAuditService auditService)
    {
        _db = db;
        _auditService = auditService;
    }

    public async Task<(bool isValid, string? errorMessage)> ValidateNewDepositAsync(int contribuableId, int exercice,
        ActeDeDepot acte, TypeDepot typeDepot)
    {
        // Rule 1: Provisional after Definitive
        var existingDefinitive = await _db.Liasses
            .Where(l => l.ContribuableId == contribuableId
                    && l.Exercice == exercice
                    && l.ActeDeDepot == acte
                    && l.TypeDepot == TypeDepot.Definitif
                    && l.Statut != StatutLiasse.Supprimee)
            .FirstOrDefaultAsync();

        if (existingDefinitive != null && typeDepot == TypeDepot.Provisoire)
        {
            return (false, "A definitive liasse has already been deposited for this fiscal year and act. " +
                "A provisional liasse cannot be transferred after a definitive deposit.");
        }

        // Rule 2: Multiple Provisionals
        var existingProvisional = await _db.Liasses
            .Where(l => l.ContribuableId == contribuableId
                    && l.Exercice == exercice
                    && l.ActeDeDepot == acte
                    && l.TypeDepot == TypeDepot.Provisoire
                    && l.Statut != StatutLiasse.Supprimee
                    && l.Statut != StatutLiasse.Rejetee)
            .FirstOrDefaultAsync();

        if (existingProvisional != null && typeDepot == TypeDepot.Provisoire)
        {
            return (false, "A provisional liasse for this fiscal year and act already exists. " +
                "Please complete or delete the existing one before creating a new deposit.");
        }

        // Rule 3: Rectification requires prior Spontané
        if (acte == ActeDeDepot.Rectification)
        {
            var hasSpontane = await _db.Liasses
                .Where(l => l.ContribuableId == contribuableId
                        && l.Exercice == exercice
                        && l.ActeDeDepot == ActeDeDepot.Spontane
                        && l.Statut != StatutLiasse.Supprimee)
                .AnyAsync();

            if (!hasSpontane)
            {
                return (false, "A rectification requires a prior spontaneous liasse for the same fiscal year.");
            }
        }

        return (true, null);
    }

    public async Task<Liasse> CreateLiasseAsync(int contribuableId, int exercice, DateOnly dateDebut, DateOnly dateCloture,
        CategorieLiasse categorie, NatureLiasse nature, ActeDeDepot acte, TypeDepot typeDepot, int submittedBy)
    {
        // Validate business rules
        var (isValid, errorMessage) = await ValidateNewDepositAsync(contribuableId, exercice, acte, typeDepot);
        if (!isValid)
            throw new InvalidOperationException(errorMessage);

        // Verify contribuable exists
        var contribuable = await _db.Contribuables.FindAsync(contribuableId);
        if (contribuable is null)
            throw new ArgumentException("Taxpayer not found.");

        // Create new liasse
        var liasse = new Liasse
        {
            ContribuableId = contribuableId,
            Exercice = exercice,
            DateDebutExercice = dateDebut,
            DateClotureExercice = dateCloture,
            Categorie = categorie,
            Nature = nature,
            ActeDeDepot = acte,
            TypeDepot = typeDepot,
            Statut = StatutLiasse.Brouillon,
            SubmittedBy = submittedBy,
            DateCreation = DateTime.UtcNow
        };

        _db.Liasses.Add(liasse);
        await _db.SaveChangesAsync();

        // Log creation
        await _auditService.LogAsync(submittedBy, AuditAction.CreateDeposit, "Liasse", liasse.Id,
            contribuableId, null, null, notes: $"{acte} {typeDepot} for {exercice}");

        return liasse;
    }

    public async Task<List<Liasse>> GetExistingDepositsAsync(int contribuableId, int exercice, ActeDeDepot acte)
    {
        return await _db.Liasses
            .Where(l => l.ContribuableId == contribuableId
                    && l.Exercice == exercice
                    && l.ActeDeDepot == acte
                    && l.Statut != StatutLiasse.Supprimee)
            .ToListAsync();
    }
}
