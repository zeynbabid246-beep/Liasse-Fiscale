using LiasseFiscale.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace LiasseFiscale.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Contribuable> Contribuables => Set<Contribuable>();
    public DbSet<UserCompanyAuthorization> UserCompanyAuthorizations => Set<UserCompanyAuthorization>();
    public DbSet<Liasse> Liasses => Set<Liasse>();
    public DbSet<DocumentFiscal> Documents => Set<DocumentFiscal>();
    public DbSet<ValidationError> ValidationErrors => Set<ValidationError>();
    public DbSet<Deposit> Deposits => Set<Deposit>();
    public DbSet<Receipt> Receipts => Set<Receipt>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // User indexes
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        // Contribuable indexes
        modelBuilder.Entity<Contribuable>()
            .HasIndex(c => new { c.NumeroMatriculeFiscal, c.CleMatriculeFiscal })
            .IsUnique();

        // UserCompanyAuthorization relationships
        modelBuilder.Entity<UserCompanyAuthorization>()
            .HasOne(a => a.User)
            .WithMany(u => u.Authorizations)
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserCompanyAuthorization>()
            .HasOne(a => a.Contribuable)
            .WithMany(c => c.UserAuthorizations)
            .HasForeignKey(a => a.ContribuableId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserCompanyAuthorization>()
            .HasIndex(a => new { a.UserId, a.ContribuableId })
            .IsUnique()
            .HasName("IX_UserCompanyAuthorization_Unique");

        // AuditLog relationships
        modelBuilder.Entity<AuditLog>()
            .HasOne(a => a.User)
            .WithMany(u => u.AuditLogs)
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<AuditLog>()
            .HasOne(a => a.Contribuable)
            .WithMany()
            .HasForeignKey(a => a.ContribuableId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<AuditLog>()
            .HasIndex(a => new { a.UserId, a.Timestamp })
            .HasName("IX_AuditLog_UserTimestamp");

        modelBuilder.Entity<AuditLog>()
            .HasIndex(a => new { a.ContribuableId, a.Timestamp })
            .HasName("IX_AuditLog_ContribuableTimestamp");

        // Liasse relationships and indexes
        modelBuilder.Entity<Liasse>()
            .HasOne(l => l.Contribuable)
            .WithMany(c => c.Liasses)
            .HasForeignKey(l => l.ContribuableId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Liasse>()
            .HasOne(l => l.SubmittedByUser)
            .WithMany()
            .HasForeignKey(l => l.SubmittedBy)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Liasse>()
            .HasOne(l => l.ReviewedByUser)
            .WithMany()
            .HasForeignKey(l => l.ReviewedBy)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Liasse>()
            .HasIndex(l => new { l.ContribuableId, l.Exercice, l.ActeDeDepot })
            .HasName("IX_Liasse_UniqueContext");

        modelBuilder.Entity<Liasse>()
            .HasIndex(l => new { l.ContribuableId, l.Statut })
            .HasName("IX_Liasse_StatusLookup");

        // DocumentFiscal relationships
        modelBuilder.Entity<DocumentFiscal>()
            .HasOne(d => d.Liasse)
            .WithMany(l => l.Documents)
            .HasForeignKey(d => d.LiasseId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DocumentFiscal>()
            .HasOne(d => d.UploadedByUser)
            .WithMany()
            .HasForeignKey(d => d.UploadedBy)
            .OnDelete(DeleteBehavior.SetNull);

        // ValidationError relationships
        modelBuilder.Entity<ValidationError>()
            .HasOne(e => e.DocumentFiscal)
            .WithMany(d => d.Erreurs)
            .HasForeignKey(e => e.DocumentFiscalId)
            .OnDelete(DeleteBehavior.Cascade);

        // Deposit relationships
        modelBuilder.Entity<Deposit>()
            .HasOne(d => d.Liasse)
            .WithOne(l => l.Deposit)
            .HasForeignKey<Deposit>(d => d.LiasseId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Deposit>()
            .HasIndex(d => d.Reference)
            .IsUnique();

        // Receipt relationships
        modelBuilder.Entity<Receipt>()
            .HasOne(r => r.Deposit)
            .WithOne(d => d.Receipt)
            .HasForeignKey<Receipt>(r => r.DepositId)
            .OnDelete(DeleteBehavior.Cascade);

        // Enum conversions (store as string for readability)
        modelBuilder.Entity<Contribuable>().Property(c => c.Categorie).HasConversion<string>();
        modelBuilder.Entity<Liasse>().Property(l => l.Categorie).HasConversion<string>();
        modelBuilder.Entity<Liasse>().Property(l => l.ActeDeDepot).HasConversion<string>();
        modelBuilder.Entity<Liasse>().Property(l => l.Nature).HasConversion<string>();
        modelBuilder.Entity<Liasse>().Property(l => l.TypeDepot).HasConversion<string>();
        modelBuilder.Entity<Liasse>().Property(l => l.ModeleF6004Choisi).HasConversion<string>();
        modelBuilder.Entity<Liasse>().Property(l => l.Statut).HasConversion<string>();
        modelBuilder.Entity<DocumentFiscal>().Property(d => d.Format).HasConversion<string>();
        modelBuilder.Entity<DocumentFiscal>().Property(d => d.Statut).HasConversion<string>();
        modelBuilder.Entity<ValidationError>().Property(e => e.Source).HasConversion<string>();
        modelBuilder.Entity<UserCompanyAuthorization>().Property(a => a.Type).HasConversion<string>();
        modelBuilder.Entity<AuditLog>().Property(a => a.Action).HasConversion<string>();
    }
}
