using LiasseFiscale.Api.Data;
using LiasseFiscale.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace LiasseFiscale.Api.Services;

/// <summary>
/// Service for audit logging of all user actions.
/// Required for tax compliance and security tracking.
/// </summary>
public interface IAuditService
{
    Task LogAsync(int? userId, AuditAction action, string? entityType, int? entityId,
        int? contribuableId, string? ipAddress, string? userAgent,
        string? oldValue = null, string? newValue = null, string? notes = null,
        bool isSuccess = true, string? errorMessage = null);

    Task<List<AuditLog>> GetLogsAsync(int? userId = null, int? contribuableId = null,
        DateTime? fromDate = null, DateTime? toDate = null, int limit = 100);
}

public class AuditService : IAuditService
{
    private readonly AppDbContext _db;

    public AuditService(AppDbContext db)
    {
        _db = db;
    }

    public async Task LogAsync(int? userId, AuditAction action, string? entityType, int? entityId,
        int? contribuableId, string? ipAddress, string? userAgent,
        string? oldValue = null, string? newValue = null, string? notes = null,
        bool isSuccess = true, string? errorMessage = null)
    {
        var log = new AuditLog
        {
            UserId = userId,
            Timestamp = DateTime.UtcNow,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            ContribuableId = contribuableId,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            OldValue = oldValue,
            NewValue = newValue,
            Notes = notes,
            IsSuccess = isSuccess,
            ErrorMessage = errorMessage
        };

        _db.AuditLogs.Add(log);
        await _db.SaveChangesAsync();
    }

    public async Task<List<AuditLog>> GetLogsAsync(int? userId = null, int? contribuableId = null,
        DateTime? fromDate = null, DateTime? toDate = null, int limit = 100)
    {
        var query = _db.AuditLogs.AsQueryable();

        if (userId.HasValue)
            query = query.Where(l => l.UserId == userId);

        if (contribuableId.HasValue)
            query = query.Where(l => l.ContribuableId == contribuableId);

        if (fromDate.HasValue)
            query = query.Where(l => l.Timestamp >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(l => l.Timestamp <= toDate.Value);

        return await query
            .OrderByDescending(l => l.Timestamp)
            .Take(limit)
            .ToListAsync();
    }
}
