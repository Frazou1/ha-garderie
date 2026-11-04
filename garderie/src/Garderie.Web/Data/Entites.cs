using System.ComponentModel.DataAnnotations;

namespace Garderie.Web.Data;

public enum Sexe { F = 1, M = 2, X = 3 }
public enum DemiJournee { Matin = 0, Pm = 1 }
public enum Gravite { Legere = 1, Moderee = 2, Severe = 3 }

public enum TypeDocument
{
    [Display(Name = "Certificat de naissance")] CertificatNaissance = 1,
    [Display(Name = "Carnet de vaccination")] Vaccination = 2,
    [Display(Name = "Fiche santé")] FicheSante = 3,
    [Display(Name = "Autorisation")] Autorisation = 4,
    [Display(Name = "Photo")] Photo = 5,
    [Display(Name = "Autre")] Autre = 99,
}

/// <summary>Dossier de l'enfant / Child record.</summary>
public class Enfant
{
    public int Id { get; set; }
    [MaxLength(100)] public string Prenom { get; set; } = "";
    [MaxLength(100)] public string Nom { get; set; } = "";
    public DateOnly? DateNaissance { get; set; }
    public Sexe? Sexe { get; set; }
    [MaxLength(300)] public string? Adresse { get; set; }
    [MaxLength(30)] public string? Telephone { get; set; }
    /// <summary>NAM chiffre (contexte nam:enfant) / Encrypted health insurance number.</summary>
    public byte[]? NamChiffre { get; set; }
    public DateOnly DateDebut { get; set; }
    public DateOnly? DateFin { get; set; }
    [MaxLength(2000)] public string? Notes { get; set; }
    [MaxLength(2000)] public string? NotesSante { get; set; }
    public int? PhotoDocumentId { get; set; }
    public DateTime CreeLe { get; set; }
    public DateTime ModifieLe { get; set; }

    public List<EnfantParent> Parents { get; set; } = new();
    public List<Contact> Contacts { get; set; } = new();
    public List<Allergie> Allergies { get; set; } = new();
    public List<MedicamentAutorise> Medicaments { get; set; } = new();
    public List<HoraireType> Horaires { get; set; } = new();
    public List<Document> Documents { get; set; } = new();

    public string NomComplet => $"{Prenom} {Nom}";
    public bool EstInscritLe(DateOnly jour) => DateDebut <= jour && (DateFin is null || DateFin >= jour);
    /// <summary>Parti (date de fin passee) : dossier archive en lecture seule / Left: archived, read-only.</summary>
    public bool EstAncienLe(DateOnly jour) => EstAncienSelon(DateFin, jour);
    public static bool EstAncienSelon(DateOnly? dateFin, DateOnly jour) => dateFin is { } f && f < jour;
}

/// <summary>Parent ou responsable (partage entre freres et soeurs) / Parent or guardian (shared by siblings).</summary>
public class Parent
{
    public int Id { get; set; }
    [MaxLength(100)] public string Prenom { get; set; } = "";
    [MaxLength(100)] public string Nom { get; set; } = "";
    [MaxLength(200)] public string? Courriel { get; set; }
    [MaxLength(30)] public string? Telephone1 { get; set; }
    [MaxLength(30)] public string? Telephone2 { get; set; }
    [MaxLength(300)] public string? Adresse { get; set; }
    /// <summary>NAS chiffre (contexte nas:parent), pour le releve 24 / Encrypted SIN.</summary>
    public byte[]? NasChiffre { get; set; }
    /// <summary>Photo chiffree dans /data/docs (guid.bin) / Encrypted photo file.</summary>
    [MaxLength(64)] public string? PhotoFichier { get; set; }
    public DateTime CreeLe { get; set; }
    public DateTime ModifieLe { get; set; }

    public List<EnfantParent> Enfants { get; set; } = new();
    public string NomComplet => $"{Prenom} {Nom}";
}

public class EnfantParent
{
    public int EnfantId { get; set; }
    public Enfant Enfant { get; set; } = null!;
    public int ParentId { get; set; }
    public Parent Parent { get; set; } = null!;
    [MaxLength(50)] public string Lien { get; set; } = "";
    /// <summary>Parent payeur : nom sur la fiche FO-0310 et releve 24 / Paying parent.</summary>
    public bool EstPayeur { get; set; }
    public int Ordre { get; set; }
}

/// <summary>Personne autorisee et/ou contact d'urgence / Authorized pickup and/or emergency contact.</summary>
public class Contact
{
    public int Id { get; set; }
    public int EnfantId { get; set; }
    [MaxLength(200)] public string Nom { get; set; } = "";
    [MaxLength(50)] public string? Lien { get; set; }
    [MaxLength(30)] public string? Telephone { get; set; }
    public bool PeutVenirChercher { get; set; }
    public bool ContactUrgence { get; set; }
    public int Ordre { get; set; }
    /// <summary>Photo chiffree dans /data/docs (guid.bin) / Encrypted photo file.</summary>
    [MaxLength(64)] public string? PhotoFichier { get; set; }
}

public class Allergie
{
    public int Id { get; set; }
    public int EnfantId { get; set; }
    [MaxLength(200)] public string Description { get; set; } = "";
    public Gravite Gravite { get; set; } = Gravite.Moderee;
    [MaxLength(1000)] public string? Mesures { get; set; }
}

public class MedicamentAutorise
{
    public int Id { get; set; }
    public int EnfantId { get; set; }
    [MaxLength(200)] public string Nom { get; set; } = "";
    [MaxLength(200)] public string? Posologie { get; set; }
    [MaxLength(300)] public string? Raison { get; set; }
    public DateOnly? DateDebut { get; set; }
    public DateOnly? DateFin { get; set; }
    public int? DocumentId { get; set; }
}

/// <summary>
/// Horaire prevu, par jour (0 = dimanche ... 6 = samedi), valide a partir de DateEffective.
/// Planned schedule per weekday, effective from DateEffective (history kept).
/// </summary>
public class HoraireType
{
    public int Id { get; set; }
    public int EnfantId { get; set; }
    public DateOnly DateEffective { get; set; }
    public DayOfWeek Jour { get; set; }
    public bool Matin { get; set; }
    public bool Pm { get; set; }
}

/// <summary>Document televerse, contenu chiffre dans /data/docs / Uploaded document, encrypted on disk.</summary>
public class Document
{
    public int Id { get; set; }
    public int EnfantId { get; set; }
    public TypeDocument Type { get; set; }
    [MaxLength(255)] public string NomOriginal { get; set; } = "";
    [MaxLength(100)] public string TypeContenu { get; set; } = "";
    public long Taille { get; set; }
    [MaxLength(64)] public string Sha256 { get; set; } = "";
    /// <summary>Nom du fichier chiffre (guid.bin) / Encrypted file name.</summary>
    [MaxLength(64)] public string Fichier { get; set; } = "";
    [MaxLength(500)] public string? Note { get; set; }
    public DateTime TeleverseLe { get; set; }
}

/// <summary>
/// Code d'une demi-case du FO-0310 (2 cases par jour : matin, apres-midi). Journee complete = P dans les 2.
/// Code of one FO-0310 half cell (2 cells per day). Full day = P in both.
/// </summary>
public class Presence
{
    public int Id { get; set; }
    public int EnfantId { get; set; }
    public Enfant Enfant { get; set; } = null!;
    public DateOnly Date { get; set; }
    public DemiJournee Demi { get; set; }
    [MaxLength(5)] public string Code { get; set; } = "";
    public DateTime ModifieLe { get; set; }
    [MaxLength(100)] public string? ModifiePar { get; set; }
}

public class JourFermeture
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    /// <summary>null = journee complete / null = full day.</summary>
    public DemiJournee? Demi { get; set; }
    [MaxLength(200)] public string? Motif { get; set; }
}

/// <summary>Journal des acces et modifications / Access and change log.</summary>
public class JournalAcces
{
    public long Id { get; set; }
    public DateTime Horodatage { get; set; }
    [MaxLength(100)] public string Utilisateur { get; set; } = "";
    [MaxLength(64)] public string Ip { get; set; } = "";
    [MaxLength(50)] public string Action { get; set; } = "";
    [MaxLength(50)] public string? Entite { get; set; }
    public int? EntiteId { get; set; }
    [MaxLength(500)] public string? Details { get; set; }
}

/// <summary>Parametres de la garderie (une seule ligne, Id = 1) / Daycare settings (single row).</summary>
public class Parametres
{
    public int Id { get; set; } = 1;
    [MaxLength(200)] public string? NomGarderie { get; set; }
    [MaxLength(200)] public string? NomRsge { get; set; }
    [MaxLength(300)] public string? Adresse { get; set; }
    [MaxLength(30)] public string? Telephone { get; set; }
    [MaxLength(200)] public string? CourrielRsge { get; set; }
    [MaxLength(200)] public string? NomBureauCoordonnateur { get; set; }
    public TimeOnly? HeureOuverture { get; set; }
    public TimeOnly? HeureFermeture { get; set; }
}
