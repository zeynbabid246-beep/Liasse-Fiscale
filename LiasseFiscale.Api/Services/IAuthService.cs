using LiasseFiscale.Api.Dtos;

namespace LiasseFiscale.Api.Services;

public interface IAuthService
{
    Task<string?> ConnecterAsync(string email, string motDePasse);
    Task<bool> InscrireAsync(RegisterRequest request);
}
