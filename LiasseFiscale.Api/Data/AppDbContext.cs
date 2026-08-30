using LiasseFiscale.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace LiasseFiscale.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Contribuable> Contribuables => Set<Contribuable>();
    public DbSet<Liasse> Liasses => Set<Liasse>();
    public DbSet<DocumentFiscal> Documents => Set<DocumentFiscal>();
    public DbSet<ValidationError> ValidationErrors => Set<ValidationError>();
    public DbSet<Deposit> Deposits => Set<Deposit>();
    public DbSet<Receipt> Receipts => Set<Receipt>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<Contribuable>()
            .HasIndex(c => new { c.NumeroMatriculeFiscal, c.CleMatriculeFiscal })
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasMany(u => u.Contribuables)
            .WithMany(c => c.Utilisateurs)
            .UsingEntity(j => j.ToTable("UserContribuables"));

        modelBuilder.Entity<Liasse>()
            .HasOne(l => l.Contribuable)
            .WithMany(c => c.Liasses)
            .HasForeignKey(l => l.ContribuableId);

        // Une liasse ne peut avoir qu'un seul dépôt définitif par (contribuable, exercice) —
        // appliqué en code dans LiasseService, pas seulement en base, car la règle dépend
        // aussi de l'état "Supprimée".
        modelBuilder.Entity<Liasse>()
            .HasIndex(l => new { l.ContribuableId, l.Exercice });

        modelBuilder.Entity<DocumentFiscal>()
            .HasOne(d => d.Liasse)
            .WithMany(l => l.Documents)
            .HasForeignKey(d => d.LiasseId);

        modelBuilder.Entity<ValidationError>()
            .HasOne(e => e.DocumentFiscal)
            .WithMany(d => d.Erreurs)
            .HasForeignKey(e => e.DocumentFiscalId);

        modelBuilder.Entity<Deposit>()
            .HasOne(d => d.Liasse)
            .WithOne(l => l.Deposit)
            .HasForeignKey<Deposit>(d => d.LiasseId);

        modelBuilder.Entity<Deposit>()
            .HasIndex(d => d.Reference)
            .IsUnique();

        modelBuilder.Entity<Receipt>()
            .HasOne(r => r.Deposit)
            .WithOne(d => d.Receipt)
            .HasForeignKey<Receipt>(r => r.DepositId);

        // Enums stockés en texte pour rester lisibles directement en base (plus simple à déboguer
        // qu'un entier opaque quand on inspecte les données à la main).
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
    }
}
