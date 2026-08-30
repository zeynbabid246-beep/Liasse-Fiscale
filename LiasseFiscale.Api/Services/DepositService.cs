using LiasseFiscale.Api.Data;
using LiasseFiscale.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace LiasseFiscale.Api.Services;

public class DepositService : IDepositService
{
    private readonly AppDbContext _db;
    private readonly ILiasseService _liasseService;

    public DepositService(AppDbContext db, ILiasseService liasseService)
    {
        _db = db;
        _liasseService = liasseService;
    }

    public async Task<Deposit> ConfirmerDepotAsync(int liasseId, string? observation = null, string? signatureElectronique = null)
    {
        var liasse = await _db.Liasses
            .Include(l => l.Documents)
            .Include(l => l.Contribuable)
            .FirstOrDefaultAsync(l => l.Id == liasseId)
            ?? throw new KeyNotFoundException($"Liasse {liasseId} introuvable.");

        var bilan = _liasseService.VerifierLiasse(liasse);
        if (!bilan.PeutDeposer)
        {
            var problemes = bilan.DocumentsManquants.Concat(bilan.DocumentsInvalides);
            throw new InvalidOperationException(
                $"Liasse incomplète pour la catégorie {liasse.Categorie}, documents manquants ou invalides : {string.Join(", ", problemes)}");
        }

        var deposit = new Deposit
        {
            LiasseId = liasse.Id,
            Reference = GenererReference(liasse),
            DateDepot = DateTime.UtcNow,
            Observation = observation ?? string.Empty,
            SignatureElectronique = signatureElectronique ?? string.Empty
        };

        liasse.Statut = StatutLiasse.Validee;

        _db.Deposits.Add(deposit);
        await _db.SaveChangesAsync();

        return deposit;
    }

    private static string GenererReference(Liasse liasse)
    {
        var horodatage = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        return $"LF-{liasse.Contribuable.MatriculeFiscalComplet}-{liasse.Exercice}-{horodatage}";
    }
}
