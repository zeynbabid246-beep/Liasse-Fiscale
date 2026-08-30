using System.Text;
using LiasseFiscale.Api.Data;
using LiasseFiscale.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace LiasseFiscale.Api.Services;

public class ReceiptService : IReceiptService
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;

    public ReceiptService(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    public async Task<Receipt> GenererAsync(Deposit deposit)
    {
        var liasse = await _db.Liasses
            .Include(l => l.Contribuable)
            .Include(l => l.Documents)
            .FirstAsync(l => l.Id == deposit.LiasseId);

        var pdfBytes = GenererPdfBinaire(deposit, liasse);

        var dossier = Path.Combine(_env.ContentRootPath, "Storage", "receipts");
        Directory.CreateDirectory(dossier);
        var chemin = Path.Combine(dossier, $"{deposit.Reference}.pdf");
        await File.WriteAllBytesAsync(chemin, pdfBytes);

        var receipt = new Receipt
        {
            DepositId = deposit.Id,
            CheminFichier = chemin,
            DateGeneration = DateTime.UtcNow
        };

        _db.Receipts.Add(receipt);
        await _db.SaveChangesAsync();

        return receipt;
    }

    private static byte[] GenererPdfBinaire(Deposit deposit, Liasse liasse)
    {
        var dateDepotStr = deposit.DateDepot.ToString("dd/MM/yyyy HH:mm");
        var docs = liasse.Documents.Where(d => d.Statut == StatutValidation.Valide).ToList();

        var streamContent = new StringBuilder();
        streamContent.AppendLine("BT");
        streamContent.AppendLine("/F1 16 Tf");
        streamContent.AppendLine("50 770 Td");
        streamContent.AppendLine("(REPUBLIQUE TUNISIENNE - MINISTERE DES FINANCES) Tj");
        streamContent.AppendLine("/F1 13 Tf");
        streamContent.AppendLine("0 -25 Td");
        streamContent.AppendLine("(DIRECTION GENERALE DES IMPOTS - ACCUSE DE RECEPTION OFFICIEL) Tj");
        streamContent.AppendLine("/F1 10 Tf");
        streamContent.AppendLine("0 -35 Td");
        streamContent.AppendLine($" (Reference de depot : {EscapePdf(deposit.Reference)}) Tj");
        streamContent.AppendLine("0 -18 Td");
        streamContent.AppendLine($" (Date et Heure : {dateDepotStr} UTC) Tj");
        streamContent.AppendLine("0 -18 Td");
        streamContent.AppendLine($" (Contribuable : {EscapePdf(liasse.Contribuable.NomOuRaisonSociale)}) Tj");
        streamContent.AppendLine("0 -18 Td");
        streamContent.AppendLine($" (Matricule Fiscal : {EscapePdf(liasse.Contribuable.MatriculeFiscalComplet)}) Tj");
        streamContent.AppendLine("0 -18 Td");
        streamContent.AppendLine($" (Exercice Fiscal : {liasse.Exercice} | Type : {liasse.TypeDepot} | Categorie : {liasse.Categorie}) Tj");
        streamContent.AppendLine("0 -28 Td");
        streamContent.AppendLine("/F1 11 Tf");
        streamContent.AppendLine("(LISTE DES ETATS FINANCIERS DEPOSES ET CONFORMES :) Tj");
        streamContent.AppendLine("/F1 10 Tf");

        foreach (var doc in docs)
        {
            streamContent.AppendLine("0 -16 Td");
            streamContent.AppendLine($"  (- {EscapePdf(doc.CodeDocument)} : {EscapePdf(doc.Libelle)} [{EscapePdf(doc.NomFichier)}]) Tj");
        }

        if (!string.IsNullOrWhiteSpace(deposit.Observation))
        {
            streamContent.AppendLine("0 -25 Td");
            streamContent.AppendLine($" (Observation : {EscapePdf(deposit.Observation)}) Tj");
        }

        streamContent.AppendLine("0 -35 Td");
        streamContent.AppendLine("/F1 9 Tf");
        streamContent.AppendLine("(Ce document certifie la recevabilite et le depot officiel de la liasse fiscale.) Tj");
        streamContent.AppendLine("ET");

        var streamBytes = Encoding.Latin1.GetBytes(streamContent.ToString());

        using var ms = new MemoryStream();
        using var writer = new StreamWriter(ms, Encoding.Latin1);

        var offsets = new List<long>();

        writer.Write("%PDF-1.4\n");
        writer.Flush();

        // 1 0 obj : Catalog
        offsets.Add(ms.Position);
        writer.Write("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");
        writer.Flush();

        // 2 0 obj : Pages
        offsets.Add(ms.Position);
        writer.Write("2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n");
        writer.Flush();

        // 3 0 obj : Page
        offsets.Add(ms.Position);
        writer.Write("3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>\nendobj\n");
        writer.Flush();

        // 4 0 obj : Contents stream
        offsets.Add(ms.Position);
        writer.Write($"4 0 obj\n<< /Length {streamBytes.Length} >>\nstream\n");
        writer.Flush();
        ms.Write(streamBytes, 0, streamBytes.Length);
        writer.Write("\nendstream\nendobj\n");
        writer.Flush();

        // 5 0 obj : Font
        offsets.Add(ms.Position);
        writer.Write("5 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\nendobj\n");
        writer.Flush();

        // xref table
        var xrefOffset = ms.Position;
        writer.Write($"xref\n0 6\n0000000000 65535 f \n");
        foreach (var off in offsets)
        {
            writer.Write($"{off:D10} 00000 n \n");
        }
        writer.Write($"trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF\n");
        writer.Flush();

        return ms.ToArray();
    }

    private static string EscapePdf(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        return input.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
    }
}
