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
    }
}
