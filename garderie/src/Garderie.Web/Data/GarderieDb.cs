using Microsoft.EntityFrameworkCore;

namespace Garderie.Web.Data;

public class GarderieDb(DbContextOptions<GarderieDb> options) : DbContext(options)
{
    public DbSet<Enfant> Enfants => Set<Enfant>();
    public DbSet<Parent> Parents => Set<Parent>();
    public DbSet<EnfantParent> EnfantParents => Set<EnfantParent>();
    public DbSet<Contact> Contacts => Set<Contact>();
    public DbSet<Allergie> Allergies => Set<Allergie>();
    public DbSet<MedicamentAutorise> Medicaments => Set<MedicamentAutorise>();
    public DbSet<HoraireType> Horaires => Set<HoraireType>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<Presence> Presences => Set<Presence>();
    public DbSet<JourFermeture> Fermetures => Set<JourFermeture>();
    public DbSet<JournalAcces> Journal => Set<JournalAcces>();
    public DbSet<Parametres> Parametres => Set<Parametres>();
    public DbSet<FicheAssiduite> Fiches => Set<FicheAssiduite>();
    public DbSet<LienSignature> Liens => Set<LienSignature>();
    public DbSet<LienFiche> LiensFiches => Set<LienFiche>();
    public DbSet<Contrat> Contrats => Set<Contrat>();
    public DbSet<LienContrat> LiensContrats => Set<LienContrat>();
    public DbSet<Releve> Releves => Set<Releve>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<EnfantParent>().HasKey(x => new { x.EnfantId, x.ParentId });
        b.Entity<EnfantParent>().HasOne(x => x.Enfant).WithMany(e => e.Parents).HasForeignKey(x => x.EnfantId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<EnfantParent>().HasOne(x => x.Parent).WithMany(p => p.Enfants).HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<Enfant>().HasMany(e => e.Contacts).WithOne().HasForeignKey(x => x.EnfantId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Enfant>().HasMany(e => e.Allergies).WithOne().HasForeignKey(x => x.EnfantId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Enfant>().HasMany(e => e.Medicaments).WithOne().HasForeignKey(x => x.EnfantId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Enfant>().HasMany(e => e.Horaires).WithOne().HasForeignKey(x => x.EnfantId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Enfant>().HasMany(e => e.Documents).WithOne().HasForeignKey(x => x.EnfantId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Enfant>().HasIndex(e => new { e.Nom, e.Prenom });

        b.Entity<HoraireType>().HasIndex(x => new { x.EnfantId, x.DateEffective, x.Jour }).IsUnique();
        b.Entity<Presence>().HasOne(p => p.Enfant).WithMany().HasForeignKey(p => p.EnfantId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Presence>().HasIndex(x => new { x.EnfantId, x.Date, x.Demi }).IsUnique();
        b.Entity<Presence>().HasIndex(x => x.Date);
        b.Entity<JourFermeture>().HasIndex(x => x.Date);
        b.Entity<JournalAcces>().HasIndex(x => x.Horodatage);
        b.Entity<Document>().HasIndex(x => x.Fichier).IsUnique();
        b.Entity<FicheAssiduite>().HasOne(f => f.Enfant).WithMany().HasForeignKey(f => f.EnfantId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<FicheAssiduite>().HasIndex(f => new { f.EnfantId, f.Debut }).IsUnique();
        b.Entity<LienSignature>().HasOne(l => l.Fiche).WithMany().HasForeignKey(l => l.FicheId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<LienSignature>().HasIndex(l => l.JetonHash).IsUnique();
        b.Entity<LienFiche>().HasKey(x => new { x.LienId, x.FicheId });
        b.Entity<Contrat>().HasOne(c => c.Enfant).WithMany().HasForeignKey(c => c.EnfantId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Contrat>().HasIndex(c => new { c.EnfantId, c.Debut });
        b.Entity<LienContrat>().HasOne(l => l.Contrat).WithMany().HasForeignKey(l => l.ContratId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<LienContrat>().HasIndex(l => l.JetonHash).IsUnique();
        b.Entity<LienFiche>().HasOne(x => x.Lien).WithMany(l => l.Groupe).HasForeignKey(x => x.LienId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<LienFiche>().HasOne(x => x.Fiche).WithMany().HasForeignKey(x => x.FicheId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Releve>().HasOne(r => r.Parent).WithMany().HasForeignKey(r => r.ParentId).OnDelete(DeleteBehavior.SetNull);
        b.Entity<Releve>().HasIndex(r => new { r.Annee, r.ParentId });
        b.Entity<Releve>().HasIndex(r => r.Numero).IsUnique();
    }
}
