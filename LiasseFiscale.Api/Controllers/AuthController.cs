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
    private readonly IAuthService _authService;
    private readonly AppDbContext _db;

    public AuthController(IAuthService authService, AppDbContext db)
    {
        _authService = authService;
        _db = db;
    }

    /// <summary>
    /// Inscription d'un déclarant / contribuable avec matricule fiscal (13 caractères).
    /// </summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        try
        {
            var success = await _authService.InscrireAsync(request);
            if (!success)
            {
                return Conflict(new { message = "Un compte existe déjà avec cet email." });
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
        var token = await _authService.ConnecterAsync(request.Email, request.Password);
        if (token is null)
        {
            return Unauthorized(new { message = "Email ou mot de passe incorrect." });
        }
        return Ok(new LoginResponse(token));
    }

    /// <summary>
    /// Profil du déclarant connecté et contribuable associé.
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
            .Include(u => u.Contribuables)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user is null)
        {
            return NotFound(new { message = "Utilisateur introuvable." });
        }

        var contribuable = user.Contribuables.FirstOrDefault();

        return Ok(new
        {
            user.Email,
            RaisonSociale = contribuable?.NomOuRaisonSociale ?? user.Email,
            MatriculeFiscal = contribuable?.MatriculeFiscalComplet ?? string.Empty,
            ContribuableId = contribuable?.Id,
            MatriculeCourt = contribuable?.MatriculeCourt ?? string.Empty
        });
    }
}
