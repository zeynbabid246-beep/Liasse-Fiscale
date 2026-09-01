namespace LiasseFiscale.Api.Models;

public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Authorizations for this user to access/modify companies.</summary>
    public List<UserCompanyAuthorization> Authorizations { get; set; } = new();

    /// <summary>Audit logs of user's actions.</summary>
    public List<AuditLog> AuditLogs { get; set; } = new();

    /// <summary>Account creation date (UTC).</summary>
    public DateTime DateCreation { get; set; } = DateTime.UtcNow;

    /// <summary>Last successful login date/time.</summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>Last IP address used for login.</summary>
    public string? LastLoginIp { get; set; }

    /// <summary>Convenience property: get all companies this user is authorized for.</summary>
    public List<Contribuable> Contribuables => Authorizations
        .Where(a => a.IsValid)
        .Select(a => a.Contribuable)
        .Distinct()
        .ToList();
}
