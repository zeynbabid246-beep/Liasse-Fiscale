namespace LiasseFiscale.Api.Models;

/// <summary>
/// Type of authorization a user has for a company.
/// </summary>
public enum AuthorizationType
{
    /// <summary>User is the taxpayer themselves.</summary>
    Direct,

    /// <summary>User is a tax professional acting on behalf of taxpayer.</summary>
    Professional,

    /// <summary>User is a company representative/authorized agent.</summary>
    Representative
}

/// <summary>
/// Represents authorization of a user to act for a company/taxpayer.
/// Implements the mandate concept from the Tunisian tax authority:
/// taxpayers can authorize professionals to file on their behalf.
/// </summary>
public class UserCompanyAuthorization
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public int ContribuableId { get; set; }
    public Contribuable Contribuable { get; set; } = null!;

    /// <summary>Type of authorization (Direct, Professional, Representative).</summary>
    public AuthorizationType Type { get; set; } = AuthorizationType.Direct;

    /// <summary>Optional reference to mandate documentation (e.g., attestation number).</summary>
    public string? MandateReference { get; set; }

    /// <summary>When authorization was granted.</summary>
    public DateTime DateAuthorized { get; set; } = DateTime.UtcNow;

    /// <summary>When authorization expires (null = indefinite).</summary>
    public DateTime? DateExpired { get; set; }

    /// <summary>Comma-separated list of permissions (all, deposit, view, download) or "all".</summary>
    public string Permissions { get; set; } = "all";

    /// <summary>Is this authorization currently active.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Check if authorization is still valid (not expired, not deactivated).</summary>
    public bool IsValid => IsActive && (DateExpired is null || DateExpired > DateTime.UtcNow);

    /// <summary>Check if user has specific permission.</summary>
    public bool HasPermission(string permission)
    {
        if (!IsValid) return false;
        if (Permissions == "all") return true;
        return Permissions.Split(',', System.StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Contains(permission);
    }
}
