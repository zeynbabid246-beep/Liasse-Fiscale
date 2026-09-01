using LiasseFiscale.Api.Data;
using LiasseFiscale.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LiasseFiscale.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAuthorizationService _authorizationService;

    public DashboardController(AppDbContext db, IAuthorizationService authorizationService)
    {
        _db = db;
        _authorizationService = authorizationService;
    }

    /// <summary>
    /// Get dashboard data for the current user: companies, pending deposits, recent activity.
    /// </summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetDashboardSummary()
    {
        var userId = HttpContext.GetUserId();
        if (userId <= 0)
            return Unauthorized();

        var companies = await _authorizationService.GetAuthorizedCompaniesAsync(userId);

        var dashboardData = new
        {
            Companies = companies.Select(c => new
            {
                c.Id,
                c.NomOuRaisonSociale,
                MatriculeFiscal = c.MatriculeFiscalComplet,
                MatriculeCourt = c.MatriculeCourt,
                c.Activite,
                c.Adresse,
                c.CodeCategorie,
                c.CodeTva
            }).ToList(),
            PendingDeposits = await GetPendingDepositsAsync(companies.Select(c => c.Id).ToList()),
            RecentActivity = await GetRecentActivityAsync(companies.Select(c => c.Id).ToList())
        };

        return Ok(dashboardData);
    }

    /// <summary>
    /// Get available fiscal years for a company.
    /// </summary>
    [HttpGet("fiscal-years/{contribuableId}")]
    public async Task<IActionResult> GetFiscalYears(int contribuableId)
    {
        // Verify user is authorized for this company
        var isAuthorized = await _authorizationService.IsAuthorizedForCompanyAsync(HttpContext.GetUserId(), contribuableId);
        if (!isAuthorized)
            return Forbid();

        var currentYear = DateTime.UtcNow.Year;
        var years = Enumerable.Range(currentYear - 5, 7)
            .OrderByDescending(y => y)
            .Select(y => new { Year = y, Label = $"{y}" })
            .ToList();

        return Ok(new { AvailableYears = years, CurrentYear = currentYear });
    }

    /// <summary>
    /// Get deposit history for a company with filtering and pagination.
    /// </summary>
    [HttpGet("company/{contribuableId}/history")]
    public async Task<IActionResult> GetCompanyHistory(
        int contribuableId,
        [FromQuery] int? exercice = null,
        [FromQuery] string? statut = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        // Verify user is authorized
        var isAuthorized = await _authorizationService.IsAuthorizedForCompanyAsync(HttpContext.GetUserId(), contribuableId);
        if (!isAuthorized)
            return Forbid();

        var query = _db.Deposits
            .Where(d => d.Liasse.ContribuableId == contribuableId)
            .Include(d => d.Liasse)
            .Include(d => d.Receipt)
            .AsQueryable();

        if (exercice.HasValue)
            query = query.Where(d => d.Liasse.Exercice == exercice.Value);

        if (!string.IsNullOrWhiteSpace(statut))
        {
            var statusUpper = statut.ToUpperInvariant();
            query = query.Where(d => d.Liasse.Statut.ToString().ToUpper() == statusUpper);
        }

        var total = await query.CountAsync();
        var deposits = await query
            .OrderByDescending(d => d.DateDepot)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new
            {
                d.Reference,
                d.DateDepot,
                Exercice = d.Liasse.Exercice,
                Statut = d.Liasse.Statut.ToString(),
                Nature = d.Liasse.Nature.ToString(),
                ActeDeDepot = d.Liasse.ActeDeDepot.ToString(),
                TypeDepot = d.Liasse.TypeDepot.ToString(),
                d.Observation,
                HasReceipt = d.Receipt != null
            })
            .ToListAsync();

        return Ok(new
        {
            Total = total,
            Page = page,
            PageSize = pageSize,
            Deposits = deposits
        });
    }

    private async Task<List<object>> GetPendingDepositsAsync(List<int> contribuableIds)
    {
        if (!contribuableIds.Any())
            return new List<object>();

        var pending = await _db.Liasses
            .Where(l => contribuableIds.Contains(l.ContribuableId) &&
                       (l.Statut == StatutLiasse.Brouillon ||
                        l.Statut == StatutLiasse.EnSaisie ||
                        l.Statut == StatutLiasse.EnErreur))
            .Include(l => l.Contribuable)
            .Select(l => new
            {
                l.Id,
                l.Exercice,
                Contribuable = l.Contribuable.NomOuRaisonSociale,
                Statut = l.Statut.ToString(),
                DateCreation = l.DateCreation,
                DocumentsUploaded = l.Documents.Count(d => d.NomFichier != null),
                TotalDocuments = l.Documents.Count
            })
            .OrderByDescending(l => l.DateCreation)
            .Take(5)
            .ToListAsync();

        return pending.Cast<object>().ToList();
    }

    private async Task<List<object>> GetRecentActivityAsync(List<int> contribuableIds)
    {
        if (!contribuableIds.Any())
            return new List<object>();

        var activities = await _db.Deposits
            .Where(d => contribuableIds.Contains(d.Liasse.ContribuableId))
            .Include(d => d.Liasse).ThenInclude(l => l.Contribuable)
            .OrderByDescending(d => d.DateDepot)
            .Take(10)
            .Select(d => new
            {
                Type = "Deposit",
                d.Reference,
                d.DateDepot,
                Contribuable = d.Liasse.Contribuable.NomOuRaisonSociale,
                Description = $"Dépôt {d.Liasse.Exercice} ({d.Liasse.Nature})",
                Statut = d.Liasse.Statut.ToString()
            })
            .ToListAsync();

        return activities.Cast<object>().ToList();
    }
}
