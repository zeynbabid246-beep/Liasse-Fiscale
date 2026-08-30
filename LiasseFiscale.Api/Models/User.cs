namespace LiasseFiscale.Api.Models;

public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Contribuable(s) pour le(s)quel(s) cet utilisateur est autorisé à déposer (lui-même ou en tant que mandataire).</summary>
    public List<Contribuable> Contribuables { get; set; } = new();

    public DateTime DateCreation { get; set; } = DateTime.UtcNow;
}
