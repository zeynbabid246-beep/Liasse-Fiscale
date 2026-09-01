namespace LiasseFiscale.Api.Models;

/// <summary>
/// Audit trail for all sensitive operations.
/// Required for tax compliance and security tracking.
/// </summary>
public enum AuditAction
{
    // Authentication events
    Login,
    Logout,
    LoginFailed,
    IdentifyTaxpayer,
    
    // Deposit operations
    CreateDeposit,
    DeleteDeposit,
    SubmitDeposit,
    ValidateDeposit,
    
    // Document operations
    UploadDocument,
    DeleteDocument,
    ValidateDocument,
    
    // Receipt operations
    GenerateReceipt,
    DownloadReceipt,
    
    // History/access
    ViewDeposit,
    DownloadDocument,
    
    // Authorization
    AuthorizationDenied,
    UnauthorizedAccess,
    
    // Admin operations
    UpdateUser,
    UpdateContribuable
}

public class AuditLog
{
    public int Id { get; set; }

    /// <summary>User who performed the action (null for system actions).</summary>
    public int? UserId { get; set; }
    public User? User { get; set; }

    /// <summary>Timestamp of the action (UTC).</summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>Type of action performed.</summary>
    public AuditAction Action { get; set; }

    /// <summary>Type of entity affected (Liasse, Document, User, etc.).</summary>
    public string? EntityType { get; set; }

    /// <summary>ID of the entity affected.</summary>
    public int? EntityId { get; set; }

    /// <summary>Company/Taxpayer ID for company-level actions.</summary>
    public int? ContribuableId { get; set; }
    public Contribuable? Contribuable { get; set; }

    /// <summary>Client IP address for security tracking.</summary>
    public string? IpAddress { get; set; }

    /// <summary>User-Agent string for device tracking.</summary>
    public string? UserAgent { get; set; }

    /// <summary>Previous value (for updates; stored as JSON if complex).</summary>
    public string? OldValue { get; set; }

    /// <summary>New value (for updates; stored as JSON if complex).</summary>
    public string? NewValue { get; set; }

    /// <summary>Additional context notes.</summary>
    public string? Notes { get; set; }

    /// <summary>Success status of the action.</summary>
    public bool IsSuccess { get; set; } = true;

    /// <summary>Error message if action failed.</summary>
    public string? ErrorMessage { get; set; }
}
