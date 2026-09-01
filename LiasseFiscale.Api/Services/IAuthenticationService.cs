namespace LiasseFiscale.Api.Services;

/// <summary>
/// Result of an authentication attempt.
/// </summary>
public class AuthenticationResult
{
    public bool IsSuccess { get; set; }
    public string? Token { get; set; }
    public string? ErrorMessage { get; set; }
    public int? UserId { get; set; }
}

/// <summary>
/// Abstraction for authentication service.
/// Allows switching between local prototype and official Tunisian tax authority integration.
/// </summary>
public interface IAuthenticationService
{
    /// <summary>
    /// Authenticate a user and return a JWT token.
    /// </summary>
    Task<AuthenticationResult> AuthenticateAsync(string email, string password, string? ipAddress = null);

    /// <summary>
    /// Register a new user (local/prototype only).
    /// </summary>
    Task<(bool success, string? message)> RegisterAsync(string email, string password, string? matriculeFiscal = null);

    /// <summary>
    /// Get current authentication mode.
    /// </summary>
    string GetAuthenticationMode();

    /// <summary>
    /// Verify if running in prototype mode.
    /// </summary>
    bool IsPrototypeMode { get; }
}
