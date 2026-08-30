using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using LiasseFiscale.Api.Data;
using LiasseFiscale.Api.Dtos;
using LiasseFiscale.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace LiasseFiscale.Api.Services;

public class AuthService : IAuthService
{
    private static readonly Regex MatriculeRegex = new(@"^[0-9]{7}[A-Z]([A-Z0-9]{5})?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;

    public AuthService(AppDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    public async Task<bool> InscrireAsync(RegisterRequest request)
    {
        if (await _db.Users.AnyAsync(u => u.Email == request.Email))
        {
            return false;
        }

        var user = new User
        {
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password)
        };

        // Si des informations de contribuable sont fournies, on crée ou lie le contribuable
        if (!string.IsNullOrWhiteSpace(request.MatriculeFiscal))
        {
            var matricule = request.MatriculeFiscal.Trim().ToUpperInvariant();
            if (!MatriculeRegex.IsMatch(matricule))
            {
                throw new ArgumentException("Le matricule fiscal doit être composé de 7 chiffres suivis d'une lettre clé (ex: 1234567M ou 1234567MAM000).");
            }

            var num = matricule.Substring(0, 7);
            var cle = matricule.Substring(7, 1);
            var catCode = request.CodeCategorie ?? (matricule.Length >= 9 ? matricule.Substring(8, 1) : "M");
            var tvaCode = request.CodeTva ?? (matricule.Length >= 10 ? matricule.Substring(9, 1) : "A");

            var contribuable = await _db.Contribuables
                .FirstOrDefaultAsync(c => c.NumeroMatriculeFiscal == num && c.CleMatriculeFiscal == cle);

            if (contribuable is null)
            {
                contribuable = new Contribuable
                {
                    NumeroMatriculeFiscal = num,
                    CleMatriculeFiscal = cle,
                    CodeCategorie = catCode,
                    CodeTva = tvaCode,
                    NomOuRaisonSociale = request.RaisonSociale ?? request.Email,
                    Adresse = request.Adresse ?? string.Empty,
                    Activite = request.Activite ?? string.Empty,
                    Categorie = catCode switch
                    {
                        "C" => CategorieContribuable.PersonneMoraleCommercialeIndustrielle,
                        "M" => CategorieContribuable.PersonneMorale,
                        "N" => CategorieContribuable.EmployeurNonSoumisImpotDirect,
                        "P" => CategorieContribuable.PersonnePhysiqueProfessionLiberale,
                        _ => CategorieContribuable.PersonneMorale
                    }
                };
                _db.Contribuables.Add(contribuable);
            }

            user.Contribuables.Add(contribuable);
        }

        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<string?> ConnecterAsync(string email, string motDePasse)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user is null || !BCrypt.Net.BCrypt.Verify(motDePasse, user.PasswordHash))
        {
            return null;
        }

        return GenererToken(user);
    }

    private string GenererToken(User user)
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
}
