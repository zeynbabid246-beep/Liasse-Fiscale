using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using LiasseFiscale.Api.Data;
using LiasseFiscale.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace LiasseFiscale.Api.Services;

/// <summary>
/// Local/Prototype authentication service.
/// This implements local email/password authentication for development/testing only.
/// Production should integrate with official Tunisian tax authority authentication.
/// </summary>
public class LocalAuthenticationService : IAuthenticationService
{
    private static readonly Regex MatriculeRegex = new(@"^[0-9]{7}[A-Z]([A-Z0-9]{5})?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly IAuditService _auditService;

    public bool IsPrototypeMode => true;

    public LocalAuthenticationService(AppDbContext db, IConfiguration configuration, IAuditService auditService)
    {
        _db = db;
        _configuration = configuration;
        _auditService = auditService;
    }

    public async Task<AuthenticationResult> AuthenticateAsync(string email, string password, string? ipAddress = null)
    {
        try
        {
            var user = await _db.Users
                .Include(u => u.Authorizations)
                .FirstOrDefaultAsync(u => u.Email == email);

            if (user is null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            {
                await _auditService.LogAsync(null, AuditAction.LoginFailed, "User", null, null,
                    ipAddress, null, notes: $"Failed login for {email}", isSuccess: false);
                return new AuthenticationResult { IsSuccess = false, ErrorMessage = "Email or password incorrect." };
            }

            // Update last login info
            user.LastLoginAt = DateTime.UtcNow;
            user.LastLoginIp = ipAddress;
            await _db.SaveChangesAsync();

            // Log successful login
            await _auditService.LogAsync(user.Id, AuditAction.Login, "User", user.Id, null,
                ipAddress, null);

            var token = GenerateToken(user);
            return new AuthenticationResult { IsSuccess = true, Token = token, UserId = user.Id };
        }
        catch (Exception ex)
        {
            await _auditService.LogAsync(null, AuditAction.LoginFailed, "User", null, null,
                ipAddress, null, notes: ex.Message, isSuccess: false, errorMessage: ex.Message);
            return new AuthenticationResult { IsSuccess = false, ErrorMessage = "Authentication failed." };
        }
    }

    public async Task<(bool success, string? message)> RegisterAsync(string email, string password, string? matriculeFiscal = null)
    {
        try
        {
            if (await _db.Users.AnyAsync(u => u.Email == email))
                return (false, "A user already exists with this email address.");

            var user = new User
            {
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password)
            };

            // If matricule provided, link or create contribuable
            if (!string.IsNullOrWhiteSpace(matriculeFiscal))
            {
                var matricule = matriculeFiscal.Trim().ToUpperInvariant();
                if (!MatriculeRegex.IsMatch(matricule))
                    return (false, "Invalid fiscal identification format.");

                var num = matricule.Substring(0, 7);
                var cle = matricule.Substring(7, 1);

                var contribuable = await _db.Contribuables
                    .FirstOrDefaultAsync(c => c.NumeroMatriculeFiscal == num && c.CleMatriculeFiscal == cle);

                if (contribuable is null)
                    return (false, "Taxpayer not found in the system.");

                // Create authorization
                var authorization = new UserCompanyAuthorization
                {
                    User = user,
                    Contribuable = contribuable,
                    Type = AuthorizationType.Direct,
                    DateAuthorized = DateTime.UtcNow,
                    Permissions = "all",
                    IsActive = true
                };

                user.Authorizations.Add(authorization);
            }

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            return (true, "Registration successful.");
        }
        catch (Exception ex)
        {
            return (false, $"Registration failed: {ex.Message}");
        }
    }

    private string GenerateToken(User user)
    {
        var key = _configuration["Jwt:Key"]!;
        var issuer = _configuration["Jwt:Issuer"];
        var audience = _configuration["Jwt:Audience"];
        var expiryMinutes = int.Parse(_configuration["Jwt:ExpiryMinutes"] ?? "120");

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GetAuthenticationMode() => "Local (Prototype)";
}

/// <summary>
/// Placeholder for official Tunisian tax authority authentication.
/// To be implemented when official API/SSO becomes available.
/// </summary>
public class OfficialAuthenticationService : IAuthenticationService
{
    public bool IsPrototypeMode => false;

    public Task<AuthenticationResult> AuthenticateAsync(string email, string password, string? ipAddress = null)
    {
        throw new NotImplementedException("Official authentication not yet available.");
    }

    public Task<(bool success, string? message)> RegisterAsync(string email, string password, string? matriculeFiscal = null)
    {
        throw new NotImplementedException("Official authentication does not support registration.");
    }

    public string GetAuthenticationMode() => "Official";
}
