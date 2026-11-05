using Garderie.Web.Infrastructure;
using System.ComponentModel.DataAnnotations;
using Garderie.Web.Data;
using Garderie.Web.Securite;
using Garderie.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Garderie.Web.Pages.Enfants;

/// <summary>
/// Dossier complet de l'enfant : identite, parents, contacts, sante, horaire, documents.
/// Full child record. Each sub-form posts to its own handler.
/// </summary>
[LectureSeuleSiAncien("id", "Reactiver", "SupprimerEnfant")]
public class DossierModel(GarderieDb db, Chiffreur chiffreur, Journal journal, DocumentsService documents) : PageModel
{
    public sealed class NouveauContact
    {
        [Required(ErrorMessage = "Nom requis"), MaxLength(200)] public string Nom { get; set; } = "";
        [MaxLength(50)] public string? Lien { get; set; }
        [MaxLength(30)] public string? Telephone { get; set; }
        public bool PeutVenirChercher { get; set; }
        public bool ContactUrgence { get; set; }
    }

    public sealed class NouvelleAllergie
    {
        [Required(ErrorMessage = "Description requise"), MaxLength(200)] public string Description { get; set; } = "";
        public Gravite Gravite { get; set; } = Gravite.Moderee;
        [MaxLength(1000)] public string? Mesures { get; set; }
    }

    public sealed class NouveauMedicament
    {
        [Required(ErrorMessage = "Nom requis"), MaxLength(200)] public string Nom { get; set; } = "";
        [MaxLength(200)] public string? Posologie { get; set; }
        [MaxLength(300)] public string? Raison { get; set; }
        public DateOnly? DateDebut { get; set; }
        public DateOnly? DateFin { get; set; }
    }

    [BindProperty(SupportsGet = true)] public int Id { get; set; }
    public Enfant Enfant { get; private set; } = null!;
    public string? NamMasque { get; private set; }
    public DateOnly? HoraireDepuis { get; private set; }
    public List<HoraireType> HoraireActuel { get; private set; } = new();
    public DateOnly Aujourdhui { get; } = Temps.Aujourdhui();
    /// <summary>Dernier contrat de l'enfant / Child's latest contract.</summary>
    public Contrat? Contrat { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await ChargerAsync()) return NotFound();
        await journal.NoterMaintenantAsync("enfant.consulter", "Enfant", Id);
        return Page();
    }

    private async Task<bool> ChargerAsync()
    {
        var e = await db.Enfants.AsNoTracking().AsSplitQuery()
            .Include(x => x.Parents).ThenInclude(p => p.Parent)
            .Include(x => x.Contacts)
            .Include(x => x.Allergies)
            .Include(x => x.Medicaments)
            .Include(x => x.Horaires)
            .Include(x => x.Documents)
            .FirstOrDefaultAsync(x => x.Id == Id);
        if (e is null) return false;
        Enfant = e;
        Contrat = await db.Contrats.AsNoTracking().Where(c => c.EnfantId == Id).OrderByDescending(c => c.Debut).ThenByDescending(c => c.Id).FirstOrDefaultAsync();
        var nam = chiffreur.DechiffrerTexte(e.NamChiffre, Contextes.NamEnfant);
        NamMasque = nam is null ? null : Identifiants.MasquerNam(nam);
        var versions = e.Horaires.Where(h => h.DateEffective <= Aujourdhui).Select(h => h.DateEffective).ToList();
        if (versions.Count == 0) versions = e.Horaires.Select(h => h.DateEffective).ToList(); // horaire futur seulement
        if (versions.Count > 0)
        {
            HoraireDepuis = versions.Max();
            HoraireActuel = e.Horaires.Where(h => h.DateEffective == HoraireDepuis).ToList();
        }
        return true;
    }

    private async Task<IActionResult> RetourAsync(string? message = null)
    {
        if (!ModelState.IsValid)
        {
            TempData["Erreur"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(x => x.ErrorMessage));
        }
        else if (message is not null) TempData["Message"] = message;
        await Task.CompletedTask;
        return RedirectToPage(new { id = Id });
    }

    private Task<bool> EnfantExisteAsync() => db.Enfants.AnyAsync(e => e.Id == Id);

    // --- Contacts ---
    public async Task<IActionResult> OnPostContactAsync(NouveauContact contact)
    {
        if (!await EnfantExisteAsync()) return NotFound();
        if (!ModelState.IsValid) return await RetourAsync();
        var ordre = await db.Contacts.Where(c => c.EnfantId == Id).CountAsync();
        var c = new Contact
        {
            EnfantId = Id, Nom = contact.Nom.Trim(), Lien = contact.Lien?.Trim(), Telephone = contact.Telephone?.Trim(),
            PeutVenirChercher = contact.PeutVenirChercher, ContactUrgence = contact.ContactUrgence, Ordre = ordre,
        };
        db.Contacts.Add(c);
        journal.Noter("contact.ajouter", "Enfant", Id, c.Nom);
        await db.SaveChangesAsync();
        return await RetourAsync("Contact ajouté.");
    }

    public async Task<IActionResult> OnPostSupprimerContactAsync(int contactId)
    {
        var c = await db.Contacts.FirstOrDefaultAsync(x => x.Id == contactId && x.EnfantId == Id);
        if (c is null) return NotFound();
        db.Contacts.Remove(c);
        journal.Noter("contact.supprimer", "Enfant", Id, c.Nom);
        await db.SaveChangesAsync();
        documents.SupprimerFichier(c.PhotoFichier);
        return await RetourAsync("Contact retiré.");
    }

    // --- Photos (enfant, parents, contacts) / Photos ---
    /// <summary>Photo de l'enfant : remplace la precedente / Child photo: replaces the previous one.</summary>
    public async Task<IActionResult> OnPostPhotoAsync(IFormFile? photo)
    {
        var e = await db.Enfants.FindAsync(Id);
        if (e is null) return NotFound();
        if (photo is null) { TempData["Erreur"] = "Choisissez une photo."; return RedirectToPage(new { id = Id }); }
        var r = await documents.EnregistrerAsync(Id, TypeDocument.Photo, photo, null);
        if (r.Document is null) { TempData["Erreur"] = r.Erreur; return RedirectToPage(new { id = Id }); }
        var ancienne = e.PhotoDocumentId is { } aid ? await db.Documents.FindAsync(aid) : null;
        db.Documents.Add(r.Document);
        await db.SaveChangesAsync();
        e.PhotoDocumentId = r.Document.Id;
        if (ancienne is not null) db.Documents.Remove(ancienne);
        journal.Noter("photo.changer", "Enfant", Id, e.NomComplet);
        await db.SaveChangesAsync();
        if (ancienne is not null) documents.Supprimer(ancienne);
        return await RetourAsync("Photo mise à jour.");
    }

    /// <summary>Photo d'un parent de cet enfant / Photo of a parent of this child.</summary>
    public async Task<IActionResult> OnPostPhotoParentAsync(int parentId, IFormFile? photo)
    {
        var lien = await db.EnfantParents.Include(x => x.Parent).FirstOrDefaultAsync(x => x.EnfantId == Id && x.ParentId == parentId);
        if (lien is null) return NotFound();
        return await RemplacerPhotoAsync(photo, lien.Parent.PhotoFichier, f => lien.Parent.PhotoFichier = f, "photo.parent", lien.Parent.NomComplet);
    }

    /// <summary>Photo d'une personne autorisee / contact d'urgence / Contact photo.</summary>
    public async Task<IActionResult> OnPostPhotoContactAsync(int contactId, IFormFile? photo)
    {
        var c = await db.Contacts.FirstOrDefaultAsync(x => x.Id == contactId && x.EnfantId == Id);
        if (c is null) return NotFound();
        return await RemplacerPhotoAsync(photo, c.PhotoFichier, f => c.PhotoFichier = f, "photo.contact", c.Nom);
    }

    private async Task<IActionResult> RemplacerPhotoAsync(IFormFile? photo, string? ancienne, Action<string> affecter, string action, string qui)
    {
        if (photo is null) { TempData["Erreur"] = "Choisissez une photo."; return RedirectToPage(new { id = Id }); }
        var (fichier, erreur) = await documents.EnregistrerPhotoAsync(photo);
        if (fichier is null) { TempData["Erreur"] = erreur; return RedirectToPage(new { id = Id }); }
        affecter(fichier);
        journal.Noter(action, "Enfant", Id, qui);
        await db.SaveChangesAsync();
        documents.SupprimerFichier(ancienne);
        return await RetourAsync("Photo mise à jour.");
    }

    public async Task<IActionResult> OnGetPhotoParentAsync(int parentId)
    {
        var p = await db.EnfantParents.AsNoTracking().Where(x => x.EnfantId == Id && x.ParentId == parentId)
            .Select(x => x.Parent.PhotoFichier).FirstOrDefaultAsync();
        return await PhotoAsync(p);
    }

    public async Task<IActionResult> OnGetPhotoContactAsync(int contactId)
    {
        var p = await db.Contacts.AsNoTracking().Where(x => x.Id == contactId && x.EnfantId == Id)
            .Select(x => x.PhotoFichier).FirstOrDefaultAsync();
        return await PhotoAsync(p);
    }

    private async Task<IActionResult> PhotoAsync(string? fichier)
    {
        var photo = await documents.LirePhotoAsync(fichier);
        return photo is { } p ? File(p.Contenu, p.Type) : NotFound();
    }

    // --- Sante / Health ---
    public async Task<IActionResult> OnPostAllergieAsync(NouvelleAllergie allergie)
    {
        if (!await EnfantExisteAsync()) return NotFound();
        if (!ModelState.IsValid) return await RetourAsync();
        db.Allergies.Add(new Allergie { EnfantId = Id, Description = allergie.Description.Trim(), Gravite = allergie.Gravite, Mesures = allergie.Mesures?.Trim() });
        journal.Noter("allergie.ajouter", "Enfant", Id, allergie.Description);
        await db.SaveChangesAsync();
        return await RetourAsync("Allergie ajoutée.");
    }

    public async Task<IActionResult> OnPostSupprimerAllergieAsync(int allergieId)
    {
        var a = await db.Allergies.FirstOrDefaultAsync(x => x.Id == allergieId && x.EnfantId == Id);
        if (a is null) return NotFound();
        db.Allergies.Remove(a);
        journal.Noter("allergie.supprimer", "Enfant", Id, a.Description);
        await db.SaveChangesAsync();
        return await RetourAsync("Allergie retirée.");
    }

    public async Task<IActionResult> OnPostMedicamentAsync(NouveauMedicament medicament)
    {
        if (!await EnfantExisteAsync()) return NotFound();
        if (medicament.DateDebut is not null && medicament.DateFin is not null && medicament.DateFin < medicament.DateDebut)
            ModelState.AddModelError("", "La date de fin du médicament précède la date de début.");
        if (!ModelState.IsValid) return await RetourAsync();
        db.Medicaments.Add(new MedicamentAutorise
        {
            EnfantId = Id, Nom = medicament.Nom.Trim(), Posologie = medicament.Posologie?.Trim(), Raison = medicament.Raison?.Trim(),
            DateDebut = medicament.DateDebut, DateFin = medicament.DateFin,
        });
        journal.Noter("medicament.ajouter", "Enfant", Id, medicament.Nom);
        await db.SaveChangesAsync();
        return await RetourAsync("Médicament ajouté.");
    }

    public async Task<IActionResult> OnPostSupprimerMedicamentAsync(int medicamentId)
    {
        var m = await db.Medicaments.FirstOrDefaultAsync(x => x.Id == medicamentId && x.EnfantId == Id);
        if (m is null) return NotFound();
        db.Medicaments.Remove(m);
        journal.Noter("medicament.supprimer", "Enfant", Id, m.Nom);
        await db.SaveChangesAsync();
        return await RetourAsync("Médicament retiré.");
    }

    // --- Parents ---
    public async Task<IActionResult> OnPostRetirerParentAsync(int parentId)
    {
        var lien = await db.EnfantParents.Include(x => x.Parent).FirstOrDefaultAsync(x => x.EnfantId == Id && x.ParentId == parentId);
        if (lien is null) return NotFound();
        db.EnfantParents.Remove(lien);
        journal.Noter("parent.retirer", "Enfant", Id, lien.Parent.NomComplet);
        await db.SaveChangesAsync();
        // Parent sans enfant : supprime (donnees personnelles inutiles) / Orphan parent: deleted
        if (!await db.EnfantParents.AnyAsync(x => x.ParentId == parentId))
        {
            db.Parents.Remove(lien.Parent);
            journal.Noter("parent.supprimer", "Parent", parentId, lien.Parent.NomComplet);
            await db.SaveChangesAsync();
            documents.SupprimerFichier(lien.Parent.PhotoFichier);
        }
        return await RetourAsync("Parent retiré du dossier.");
    }

    // --- Archive / Archived record ---
    public bool Archive => Enfant.EstAncienLe(Aujourdhui);

    /// <summary>Reactive un dossier archive (retire la date de fin) / Reactivates an archived record.</summary>
    public async Task<IActionResult> OnPostReactiverAsync()
    {
        var e = await db.Enfants.FindAsync(Id);
        if (e is null) return NotFound();
        var ancienneFin = e.DateFin;
        e.DateFin = null;
        e.ModifieLe = DateTime.UtcNow;
        journal.Noter("enfant.reactiver", "Enfant", Id, $"Date de fin retirée ({ancienneFin:yyyy-MM-dd})");
        await db.SaveChangesAsync();
        return await RetourAsync("Dossier réactivé : la date de fin est retirée.");
    }

    // --- Suppression du dossier / Record deletion ---
    /// <summary>
    /// Supprime l'enfant et tout son dossier (contacts, sante, horaires, presences, documents chiffres),
    /// puis les parents qui n'ont plus d'enfant. Il faut retaper le prenom. Le journal est conserve.
    /// Deletes the child and the whole record, then orphan parents. First name must be retyped. Log kept.
    /// </summary>
    public async Task<IActionResult> OnPostSupprimerEnfantAsync(string? confirmation)
    {
        var e = await db.Enfants.Include(x => x.Documents).Include(x => x.Contacts).Include(x => x.Parents).ThenInclude(p => p.Parent)
            .FirstOrDefaultAsync(x => x.Id == Id);
        if (e is null) return NotFound();
        if (!string.Equals(confirmation?.Trim(), e.Prenom.Trim(), StringComparison.CurrentCultureIgnoreCase))
        {
            TempData["Erreur"] = "Suppression annulée : le prénom tapé ne correspond pas.";
            return RedirectToPage(new { id = Id });
        }

        var nom = e.NomComplet;
        var fichiers = e.Documents.ToList();
        var parents = e.Parents.Select(p => p.Parent).ToList();
        var photos = e.Contacts.Select(c => c.PhotoFichier).ToList();
        // Signatures des fiches (les fiches et liens partent en cascade) / Sheet signatures (sheets cascade)
        foreach (var f in await db.Fiches.Where(f => f.EnfantId == Id).Select(f => new { f.SignatureRsgeFichier, f.SignatureParentFichier }).ToListAsync())
            photos.AddRange([f.SignatureRsgeFichier, f.SignatureParentFichier]);
        foreach (var c in await db.Contrats.Where(c => c.EnfantId == Id).Select(c => new { c.SignatureParentFichier, c.SignatureRsgeFichier, c.SignatureResiliationFichier }).ToListAsync())
            photos.AddRange([c.SignatureParentFichier, c.SignatureRsgeFichier, c.SignatureResiliationFichier]);
        await using var tx = await db.Database.BeginTransactionAsync();
        await db.Presences.Where(p => p.EnfantId == Id).ExecuteDeleteAsync();
        db.Enfants.Remove(e); // cascade : liens parents, contacts, allergies, medicaments, horaires, documents
        journal.Noter("enfant.supprimer", "Enfant", Id, $"{nom} ({fichiers.Count} document(s))");
        await db.SaveChangesAsync();
        foreach (var p in parents)
        {
            if (await db.EnfantParents.AnyAsync(x => x.ParentId == p.Id)) continue;
            db.Parents.Remove(p);
            journal.Noter("parent.supprimer", "Parent", p.Id, p.NomComplet);
            photos.Add(p.PhotoFichier);
        }
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        // Fichiers chiffres effaces apres la validation / Encrypted files removed after commit
        foreach (var d in fichiers) documents.Supprimer(d);
        foreach (var f in photos) documents.SupprimerFichier(f);

        TempData["Message"] = $"Dossier de {nom} supprimé.";
        return RedirectToPage("Index");
    }

    // --- Documents ---
    public async Task<IActionResult> OnPostDocumentAsync(IFormFile? fichier, TypeDocument type, string? note)
    {
        var e = await db.Enfants.FindAsync(Id);
        if (e is null) return NotFound();
        if (fichier is null) { TempData["Erreur"] = "Choisissez un fichier."; return RedirectToPage(new { id = Id }); }
        if (!Enum.IsDefined(type)) type = TypeDocument.Autre;

        var r = await documents.EnregistrerAsync(Id, type, fichier, note);
        if (r.Document is null) { TempData["Erreur"] = r.Erreur; return RedirectToPage(new { id = Id }); }
        db.Documents.Add(r.Document);
        await db.SaveChangesAsync();
        if (type == TypeDocument.Photo) e.PhotoDocumentId = r.Document.Id;
        journal.Noter("document.televerser", "Enfant", Id, $"{type} : {r.Document.NomOriginal}");
        await db.SaveChangesAsync();
        TempData["Message"] = "Document ajouté (chiffré).";
        return RedirectToPage(new { id = Id });
    }

    public async Task<IActionResult> OnPostSupprimerDocumentAsync(int documentId)
    {
        var d = await db.Documents.FirstOrDefaultAsync(x => x.Id == documentId && x.EnfantId == Id);
        if (d is null) return NotFound();
        var e = await db.Enfants.FindAsync(Id);
        if (e?.PhotoDocumentId == d.Id) e.PhotoDocumentId = null;
        await db.Medicaments.Where(m => m.DocumentId == d.Id).ExecuteUpdateAsync(s => s.SetProperty(m => m.DocumentId, (int?)null));
        db.Documents.Remove(d);
        journal.Noter("document.supprimer", "Enfant", Id, d.NomOriginal);
        await db.SaveChangesAsync();
        documents.Supprimer(d);
        return await RetourAsync("Document supprimé.");
    }

    /// <summary>
    /// Telechargement (pieces jointes) ou apercu de la photo (image seulement).
    /// Download (attachment) or photo preview (images only).
    /// </summary>
    public async Task<IActionResult> OnGetDocumentAsync(int documentId, bool apercu = false, bool afficher = false)
    {
        var d = await db.Documents.AsNoTracking().FirstOrDefaultAsync(x => x.Id == documentId && x.EnfantId == Id);
        if (d is null) return NotFound();
        var contenu = await documents.LireAsync(d);
        // Apercu de la photo (avatar) : non journalise, il s'affiche partout / Avatar preview: not logged
        if (apercu && d.Type == TypeDocument.Photo && d.TypeContenu is "image/jpeg" or "image/png")
            return File(contenu, d.TypeContenu);
        var lisible = d.TypeContenu is "application/pdf" or "image/jpeg" or "image/png";
        await journal.NoterMaintenantAsync(afficher && lisible ? "document.afficher" : "document.telecharger", "Enfant", Id, d.NomOriginal);
        return this.Fichier(contenu, d.TypeContenu, d.NomOriginal, afficher && lisible);
    }
}
