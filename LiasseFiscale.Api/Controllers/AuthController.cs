using System.IdentityModel.Tokens.Jwt;
using LiasseFiscale.Api.Data;
using LiasseFiscale.Api.Dtos;
using LiasseFiscale.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LiasseFiscale.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthenticationService _authenticationService;
    private readonly AppDbContext _db;
    private readonly LiasseFiscale.Api.Services.IAuthorizationService _authorizationService;

    public AuthController(IAuthenticationService authenticationService, AppDbContext db, LiasseFiscale.Api.Services.IAuthorizationService authorizationService)
    {
        _authenticationService = authenticationService;
        _db = db;
        _authorizationService = authorizationService;
    }

    /// <summary>
    /// Inscription d'un déclarant / contribuable avec matricule fiscal (13 caractères).
    /// </summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        try
        {
            var (success, message) = await _authenticationService.RegisterAsync(request.Email, request.Password, request.MatriculeFiscal);
            if (!success)
            {
                return Conflict(new { message });
            }
            return Ok(new { message = "Inscription réussie." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Authentification du déclarant pour obtenir le jeton JWT.
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _authenticationService.AuthenticateAsync(request.Email, request.Password, HttpContext.Connection.RemoteIpAddress?.ToString());
        if (!result.IsSuccess || string.IsNullOrWhiteSpace(result.Token))
        {
            return Unauthorized(new { message = result.ErrorMessage ?? "Email ou mot de passe incorrect." });
        }

        return Ok(new LoginResponse(result.Token));
    }

    /// <summary>
    /// Identifie un contribuable à partir de son matricule fiscal.
    /// </summary>
    [Authorize]
    [HttpPost("identify-taxpayer")]
    public async Task<IActionResult> IdentifyTaxpayer([FromBody] IdentifyTaxpayerRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.MatriculeFiscal))
        {
            return BadRequest(new { message = "Le matricule fiscal est obligatoire." });
        }

        var matricule = request.MatriculeFiscal.Trim().ToUpperInvariant();
        var num = matricule.Length >= 7 ? matricule.Substring(0, 7) : matricule;
        var cle = matricule.Length >= 8 ? matricule.Substring(7, 1) : "";

        var contribuable = await _db.Contribuables
            .FirstOrDefaultAsync(c => c.NumeroMatriculeFiscal == num &&
                (string.IsNullOrEmpty(cle) || c.CleMatriculeFiscal == cle));

        if (contribuable is null)
        {
            return NotFound(new { message = "Contribuable introuvable pour ce matricule fiscal." });
        }

        var userId = HttpContext.GetUserId();
        var isAuthorized = await _authorizationService.IsAuthorizedForCompanyAsync(userId, contribuable.Id);

        return Ok(new IdentifyTaxpayerResponse(
            contribuable.Id,
            contribuable.MatriculeCourt,
            contribuable.MatriculeFiscalComplet,
            contribuable.NomOuRaisonSociale,
            contribuable.CodeCategorie,
            contribuable.CodeTva,
            isAuthorized));
    }

    /// <summary>
    /// Profil du déclarant connecté et contribuables auxquels il a accès.
    /// </summary>
    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var userIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (!int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new { message = "Token invalide." });
        }

        var user = await _db.Users
            .Include(u => u.Authorizations)
            .ThenInclude(a => a.Contribuable)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user is null)
        {
            return NotFound(new { message = "Utilisateur introuvable." });
        }

        var authorizations = user.Authorizations
            .Where(a => a.IsValid)
            .Select(a => new
            {
                a.ContribuableId,
                a.Type,
                a.Permissions,
                Contribuable = new
                {
                    a.Contribuable.Id,
                    a.Contribuable.MatriculeFiscalComplet,
                    a.Contribuable.MatriculeCourt,
                    a.Contribuable.NomOuRaisonSociale,
                    a.Contribuable.CodeCategorie,
                    a.Contribuable.CodeTva
                }
            })
            .ToList();

        return Ok(new
        {
            user.Email,
            user.DateCreation,
            user.LastLoginAt,
            user.LastLoginIp,
            Contribuables = authorizations,
            ContribuableCount = authorizations.Count,
            PrimaryContribuable = authorizations.FirstOrDefault()?.Contribuable
        });
    }
}
