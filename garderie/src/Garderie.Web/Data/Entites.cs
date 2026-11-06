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
    [Display(Name = "Fiche d'assiduité signée")] FicheAssiduite = 6,
    [Display(Name = "Contrat signé")] Contrat = 7,
    [Display(Name = "Résiliation de contrat")] Resiliation = 8,
    [Display(Name = "Relevé 24")] Releve24 = 9,
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
    /// <summary>Numero d'inscription au portail (La Place 0-5) / Registration number on the waiting-list portal.</summary>
    [MaxLength(30)] public string? NumeroPortail { get; set; }
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
    /// <summary>Cellulaire de l'autre parent (ex. papa) non inscrit / Other parent's cell phone.</summary>
    [MaxLength(30)] public string? TelephoneAutreParent { get; set; }
    [MaxLength(300)] public string? Adresse { get; set; }
    /// <summary>NAS chiffre (contexte nas:parent), pour le releve 24 / Encrypted SIN.</summary>
    public byte[]? NasChiffre { get; set; }
    /// <summary>
    /// Consentement ecrit a recevoir le releve 24 par courriel (exige par Revenu Quebec) ; null = papier.
    /// Written consent to receive the RL-24 by email (required by Revenu Quebec); null = paper.
    /// </summary>
    public DateTime? ConsentRl24Le { get; set; }
    [MaxLength(100)] public string? ConsentRl24Source { get; set; }
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
    /// <summary>Groupe Facebook des parents (lien https) / Parents' Facebook group (https link).</summary>
    [MaxLength(300)] public string? LienFacebook { get; set; }
    public TimeOnly? HeureOuverture { get; set; }
    public TimeOnly? HeureFermeture { get; set; }
    /// <summary>Un lundi ou commence une periode de 2 semaines du BC / A Monday starting a 2-week BC period.</summary>
    public DateOnly? DebutPeriodes { get; set; }

    // --- Contrat (entente de service), textes modifiables / Service agreement, editable texts ---
    [MaxLength(100)] public string? VilleSignature { get; set; }
    [MaxLength(4000)] public string? ContratObligationsRsge { get; set; }
    [MaxLength(4000)] public string? ContratObligationsParent { get; set; }
    [MaxLength(6000)] public string? ContratMentionLpc { get; set; }
    [MaxLength(2000)] public string? ContratAttestation { get; set; }
    [MaxLength(2000)] public string? ContratNonRespect { get; set; }
    /// <summary>Note sur les vacances (ex. conge des fetes a confirmer) / Holiday note.</summary>
    [MaxLength(1000)] public string? ContratNoteVacances { get; set; }
    // Sections du modele du bureau coordonnateur (null = texte par defaut) / Coordinating office template sections
    [MaxLength(4000)] public string? ContratServices { get; set; }
    [MaxLength(4000)] public string? ContratClausesFermeture { get; set; }
    [MaxLength(4000)] public string? ContratResiliationRsge { get; set; }
    [MaxLength(2000)] public string? ContratResiliationParent { get; set; }
    /// <summary>Heures des collations et du diner (ex. « 9 h 00 ») / Snack and lunch times.</summary>
    [MaxLength(20)] public string? HeureCollationMatin { get; set; }
    [MaxLength(20)] public string? HeureCollationPm { get; set; }
    [MaxLength(20)] public string? HeureDiner { get; set; }
    /// <summary>Frais d'un cheque sans provision (ex. « 25,00 ») / NSF cheque fee.</summary>
    [MaxLength(20)] public string? ContratFraisNsf { get; set; }
    /// <summary>Frais de retard : montant par tranche de minutes / Late fee: amount per block of minutes.</summary>
    [MaxLength(20)] public string? ContratRetardMontant { get; set; }
    [MaxLength(20)] public string? ContratRetardMinutes { get; set; }

    // --- Regie interne generee : sections modifiees (JSON cle -> texte) et valeurs / Generated internal rules ---
    [MaxLength(100000)] public string? RegieSections { get; set; }
    [MaxLength(50)] public string? RegieTarif { get; set; }
    [MaxLength(30)] public string? RegieJourPaiement { get; set; }
    /// <summary>Modes de paiement (ex. « en argent comptant ou par virement Interac ») / Payment methods.</summary>
    [MaxLength(100)] public string? RegieModesPaiement { get; set; }
    [MaxLength(200)] public string? RegieFraisRetard { get; set; }
    public DateTime? RegieModifieeLe { get; set; }

    // --- Releve 24 (Revenu Quebec) ---
    /// <summary>Case H : NEQ (10 chiffres) ou NAS de la RSGE (9), chiffre / Box H: NEQ or SIN, encrypted.</summary>
    public byte[]? Rl24IdentifiantChiffre { get; set; }
    // Adresse du fournisseur, par morceaux comme sur le formulaire / Provider address, split like the form
    [MaxLength(10)] public string? Rl24Appartement { get; set; }
    [MaxLength(20)] public string? Rl24Numero { get; set; }
    [MaxLength(100)] public string? Rl24Rue { get; set; }
    [MaxLength(60)] public string? Rl24Ville { get; set; }
    [MaxLength(2)] public string? Rl24Province { get; set; }
    [MaxLength(6)] public string? Rl24CodePostal { get; set; }
    /// <summary>Nom et fonction du signataire du sommaire / Summary signatory name and title.</summary>
    [MaxLength(100)] public string? Rl24Signataire { get; set; }
    /// <summary>Dernier numero sequentiel utilise (9 chiffres) / Last sequential slip number used.</summary>
    [MaxLength(9)] public string? Rl24DernierNumero { get; set; }
}

public enum EtatContrat
{
    [Display(Name = "Brouillon")] Brouillon = 0,
    [Display(Name = "Signé par le parent")] SigneParent = 1,
    [Display(Name = "Signé")] Signe = 2,
    [Display(Name = "Résilié")] Resilie = 3,
}

/// <summary>
/// Entente de service (contrat) d'un enfant : le parent complete ses renseignements et signe, puis la RSGE
/// contresigne. Les renseignements et les textes sont figes a la signature du parent (Fige). Resiliation possible.
/// Child service agreement: parent completes their info and signs, then the RSGE countersigns.
/// </summary>
public class Contrat
{
    public int Id { get; set; }
    public int EnfantId { get; set; }
    public Enfant Enfant { get; set; } = null!;
    public int? ParentId { get; set; }
    public DateOnly Debut { get; set; }
    public DateOnly Fin { get; set; }
    public EtatContrat Etat { get; set; }

    [MaxLength(1000)] public string? VacancesParent { get; set; }
    /// <summary>Renseignements et textes figes (JSON) a la signature du parent / Frozen info and texts (JSON).</summary>
    public string? Fige { get; set; }
    /// <summary>NAS du parent fige a la signature, chiffre (contexte nas:contrat) / Frozen SIN, encrypted.</summary>
    public byte[]? NasChiffre { get; set; }

    [MaxLength(64)] public string? SignatureParentFichier { get; set; }
    public DateTime? SigneParentLe { get; set; }
    [MaxLength(200)] public string? SignataireParent { get; set; }
    [MaxLength(64)] public string? IpParent { get; set; }
    public bool SigneSurPlace { get; set; }

    [MaxLength(64)] public string? SignatureRsgeFichier { get; set; }
    public DateTime? SigneRsgeLe { get; set; }
    [MaxLength(100)] public string? SigneRsgePar { get; set; }
    public int? DocumentId { get; set; }

    // Resiliation (formule signee par le parent) / Termination (form signed by the parent)
    public DateOnly? ResilieLe { get; set; }
    [MaxLength(64)] public string? SignatureResiliationFichier { get; set; }
    public DateTime? ResiliationSigneeLe { get; set; }
    [MaxLength(200)] public string? SignataireResiliation { get; set; }
    [MaxLength(64)] public string? IpResiliation { get; set; }
    public int? DocumentResiliationId { get; set; }

    public DateTime CreeLe { get; set; }
    public DateTime ModifieLe { get; set; }
}

public enum ButLien { Signature = 0, Resiliation = 1 }

/// <summary>Lien unique pour un contrat (signature ou resiliation) ; seul le SHA-256 est garde / One-time contract link.</summary>
public class LienContrat
{
    public int Id { get; set; }
    public int ContratId { get; set; }
    public Contrat Contrat { get; set; } = null!;
    public ButLien But { get; set; }
    [MaxLength(64)] public string JetonHash { get; set; } = "";
    public DateTime CreeLe { get; set; }
    public DateTime ExpireLe { get; set; }
    public DateTime? UtiliseLe { get; set; }
    public DateTime? AnnuleLe { get; set; }
    [MaxLength(200)] public string? EnvoyeA { get; set; }
    public bool SurPlace { get; set; }
}

public enum EtatFiche
{
    [Display(Name = "À signer")] Brouillon = 0,
    [Display(Name = "Signée par la RSGE")] SigneeRsge = 1,
    [Display(Name = "Signée")] SigneeParent = 2,
}

/// <summary>
/// Fiche d'assiduite FO-0310 d'un enfant pour 2 semaines : paiements, signatures, PDF final.
/// Les codes sont figes (CodesSignes) a la signature de la RSGE : le parent signe exactement ce qu'elle a signe.
/// FO-0310 attendance sheet for one child and 2 weeks; codes frozen when the RSGE signs.
/// </summary>
public class FicheAssiduite
{
    public int Id { get; set; }
    public int EnfantId { get; set; }
    public Enfant Enfant { get; set; } = null!;
    /// <summary>Lundi de la premiere semaine / Monday of the first week.</summary>
    public DateOnly Debut { get; set; }
    public EtatFiche Etat { get; set; }

    public DateOnly? DatePaiement1 { get; set; }
    public decimal? MontantPaye1 { get; set; }
    public decimal? Solde1 { get; set; }
    public DateOnly? DatePaiement2 { get; set; }
    public decimal? MontantPaye2 { get; set; }
    public decimal? Solde2 { get; set; }

    /// <summary>Codes figes : « 2026-11-02M=P;2026-11-02P=P;... » / Frozen codes.</summary>
    public string? CodesSignes { get; set; }
    [MaxLength(200)] public string? NomParent { get; set; }
    public int? ParentId { get; set; }

    [MaxLength(64)] public string? SignatureRsgeFichier { get; set; }
    public DateTime? SigneRsgeLe { get; set; }
    [MaxLength(100)] public string? SigneRsgePar { get; set; }

    [MaxLength(64)] public string? SignatureParentFichier { get; set; }
    public DateTime? SigneParentLe { get; set; }
    [MaxLength(200)] public string? SignataireParent { get; set; }
    [MaxLength(64)] public string? IpParent { get; set; }
    public bool SigneSurPlace { get; set; }

    /// <summary>Document du dossier qui contient le PDF final / Child document holding the final PDF.</summary>
    public int? DocumentId { get; set; }
    [MaxLength(64)] public string? PdfSha256 { get; set; }

    [MaxLength(1000)] public string? ErreurSignalee { get; set; }
    public DateTime? ErreurSignaleeLe { get; set; }

    public DateTime CreeLe { get; set; }
    public DateTime ModifieLe { get; set; }

    public DateOnly Fin => Debut.AddDays(13);
}

/// <summary>
/// Lien unique envoye au parent : seul le hachage SHA-256 du jeton est garde. Usage unique, expire.
/// One-time parent link: only the SHA-256 hash of the token is stored.
/// </summary>
public class LienSignature
{
    public int Id { get; set; }
    public int FicheId { get; set; }
    public FicheAssiduite Fiche { get; set; } = null!;
    [MaxLength(64)] public string JetonHash { get; set; } = "";
    public DateTime CreeLe { get; set; }
    public DateTime ExpireLe { get; set; }
    public DateTime? UtiliseLe { get; set; }
    public DateTime? AnnuleLe { get; set; }
    [MaxLength(200)] public string? EnvoyeA { get; set; }
    public bool SurPlace { get; set; }
    /// <summary>Toutes les fiches couvertes par ce lien (fratrie) / All sheets covered by this link (siblings).</summary>
    public List<LienFiche> Groupe { get; set; } = new();
}

/// <summary>Fiche couverte par un lien de signature / Sheet covered by a signature link.</summary>
public class LienFiche
{
    public int LienId { get; set; }
    public LienSignature Lien { get; set; } = null!;
    public int FicheId { get; set; }
    public FicheAssiduite Fiche { get; set; } = null!;
}

/// <summary>
/// Releve 24 produit pour un parent payeur et une annee. Le contenu est fige en JSON (sans NAS) ; le PDF rempli est
/// dans le dossier du premier enfant. Un releve modifie (code A) remplace le precedent.
/// RL-24 slip issued to a paying parent for a year: frozen JSON content (no SIN), filled PDF in the first child's file.
/// </summary>
public class Releve
{
    public int Id { get; set; }
    public int Annee { get; set; }
    /// <summary>null si le parent a ete supprime (le releve reste, pour l'impot) / null if the parent was deleted.</summary>
    public int? ParentId { get; set; }
    public Parent? Parent { get; set; }
    [MaxLength(9)] public string Numero { get; set; } = "";
    /// <summary>R original, A modifie / R original, A amended.</summary>
    [MaxLength(1)] public string Code { get; set; } = "R";
    [MaxLength(9)] public string? NumeroOriginal { get; set; }
    public string Contenu { get; set; } = "";
    public int EnfantId { get; set; }
    public int? DocumentId { get; set; }
    /// <summary>Remplace par un releve modifie / Replaced by an amended slip.</summary>
    public int? RemplaceParId { get; set; }
    public DateTime CreeLe { get; set; }
    [MaxLength(100)] public string? CreePar { get; set; }
    public DateTime? EnvoyeLe { get; set; }
    [MaxLength(200)] public string? EnvoyeA { get; set; }
}
