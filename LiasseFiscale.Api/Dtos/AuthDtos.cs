using System.Text.Json.Serialization;

namespace LiasseFiscale.Api.Dtos;

public record LoginRequest(
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("password")] string Password)
{
    // Alias pour rétrocompatibilité
    [JsonPropertyName("motDePasse")]
    public string? MotDePasse { init => Password = value ?? Password; }
}

public record LoginResponse(string Token);

public record RegisterRequest(
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("password")] string Password,
    [property: JsonPropertyName("raisonSociale")] string? RaisonSociale = null,
    [property: JsonPropertyName("matriculeFiscal")] string? MatriculeFiscal = null,
    [property: JsonPropertyName("adresse")] string? Adresse = null,
    [property: JsonPropertyName("activite")] string? Activite = null,
    [property: JsonPropertyName("codeCategorie")] string? CodeCategorie = "M",
    [property: JsonPropertyName("codeTva")] string? CodeTva = "A")
{
    [JsonPropertyName("motDePasse")]
    public string? MotDePasse { init => Password = value ?? Password; }
}
