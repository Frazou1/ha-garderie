using System.ComponentModel.DataAnnotations;
using Garderie.Web.Data;
using Garderie.Web.Securite;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Garderie.Web.Pages.Enfants;

/// <summary>
/// Ajout / modification d'un parent dans le dossier d'un enfant, ou lien vers un parent existant (fratrie).
/// Add or edit a parent in a child's record, or link an existing parent (siblings).
/// </summary>
public class ParentModel(GarderieDb db, Chiffreur chiffreur, Journal journal) : PageModel
{
    public sealed class Saisie
    {
        [Required(ErrorMessage = "Prénom requis"), MaxLength(100)] public string Prenom { get; set; } = "";
        [Required(ErrorMessage = "Nom requis"), MaxLength(100)] public string Nom { get; set; } = "";
        [Required(ErrorMessage = "Lien requis"), MaxLength(50)] public string Lien { get; set; } = "Mère";
        public bool EstPayeur { get; set; }
        [EmailAddress(ErrorMessage = "Courriel invalide"), MaxLength(200)] public string? Courriel { get; set; }
        [MaxLength(30)] public string? Telephone1 { get; set; }
        [MaxLength(30)] public string? Telephone2 { get; set; }
        [MaxLength(300)] public string? Adresse { get; set; }
        /// <summary>Nouveau NAS (vide = inchange) / New SIN (empty = unchanged).</summary>
        [MaxLength(15)] public string? Nas { get; set; }
        public bool EffacerNas { get; set; }
    }

    [BindProperty(SupportsGet = true)] public int EnfantId { get; set; }
    [BindProperty(SupportsGet = true)] public int? ParentId { get; set; }
    [BindProperty] public Saisie Donnees { get; set; } = new();
    public Enfant Enfant { get; private set; } = null!;
    public bool NasEnregistre { get; private set; }
    public string? NasAffiche { get; private set; }
    public List<Parent> ParentsExistants { get; private set; } = new();

    private async Task<bool> ChargerAsync()
    {
        var e = await db.Enfants.AsNoTracking().FirstOrDefaultAsync(x => x.Id == EnfantId);
        if (e is null) return false;
        Enfant = e;
        var dejaLies = await db.EnfantParents.Where(x => x.EnfantId == EnfantId).Select(x => x.ParentId).ToListAsync();
        ParentsExistants = await db.Parents.AsNoTracking().Where(p => !dejaLies.Contains(p.Id))
            .OrderBy(p => p.Nom).ThenBy(p => p.Prenom).ToListAsync();
        return true;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await ChargerAsync()) return NotFound();
        if (ParentId is null) return Page();
        var lien = await db.EnfantParents.AsNoTracking().Include(x => x.Parent)
            .FirstOrDefaultAsync(x => x.EnfantId == EnfantId && x.ParentId == ParentId);
        if (lien is null) return NotFound();
        var p = lien.Parent;
        Donnees = new Saisie
        {
            Prenom = p.Prenom, Nom = p.Nom, Lien = lien.Lien, EstPayeur = lien.EstPayeur, Courriel = p.Courriel,
            Telephone1 = p.Telephone1, Telephone2 = p.Telephone2, Adresse = p.Adresse,
        };
        NasEnregistre = p.NasChiffre is not null;
        return Page();
    }

    /// <summary>Affiche le NAS en clair (journalise) / Shows the SIN in clear (logged).</summary>
    public async Task<IActionResult> OnPostAfficherNasAsync()
    {
        if (ParentId is null || !await ChargerAsync()) return NotFound();
        var lien = await db.EnfantParents.Include(x => x.Parent).FirstOrDefaultAsync(x => x.EnfantId == EnfantId && x.ParentId == ParentId);
        if (lien is null) return NotFound();
        var p = lien.Parent;
        Donnees = new Saisie
        {
            Prenom = p.Prenom, Nom = p.Nom, Lien = lien.Lien, EstPayeur = lien.EstPayeur, Courriel = p.Courriel,
            Telephone1 = p.Telephone1, Telephone2 = p.Telephone2, Adresse = p.Adresse,
        };
        ModelState.Clear();
        NasEnregistre = p.NasChiffre is not null;
        var nas = chiffreur.DechiffrerTexte(p.NasChiffre, Contextes.NasParent);
        NasAffiche = nas is null ? null : Identifiants.FormaterNas(nas);
        await journal.NoterMaintenantAsync("nas.afficher", "Parent", p.Id, p.NomComplet);
        return Page();
    }

    public async Task<IActionResult> OnPostLierAsync(int parentExistantId, string? lien, bool estPayeur)
    {
        if (!await ChargerAsync()) return NotFound();
        var p = await db.Parents.FindAsync(parentExistantId);
        if (p is null) return NotFound();
        if (!await db.EnfantParents.AnyAsync(x => x.EnfantId == EnfantId && x.ParentId == p.Id))
        {
            if (estPayeur) await RetirerPayeurAsync();
            var ordre = await db.EnfantParents.CountAsync(x => x.EnfantId == EnfantId);
            db.EnfantParents.Add(new EnfantParent
            {
                EnfantId = EnfantId, ParentId = p.Id, Lien = string.IsNullOrWhiteSpace(lien) ? "Parent" : lien.Trim()[..Math.Min(50, lien.Trim().Length)],
                EstPayeur = estPayeur, Ordre = ordre,
            });
            journal.Noter("parent.lier", "Enfant", EnfantId, p.NomComplet);
            await db.SaveChangesAsync();
        }
        TempData["Message"] = $"{p.NomComplet} ajouté au dossier.";
        return RedirectToPage("Dossier", new { id = EnfantId });
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!await ChargerAsync()) return NotFound();
        EnfantParent? lien = null;
        if (ParentId is not null)
        {
            lien = await db.EnfantParents.Include(x => x.Parent).FirstOrDefaultAsync(x => x.EnfantId == EnfantId && x.ParentId == ParentId);
            if (lien is null) return NotFound();
            NasEnregistre = lien.Parent.NasChiffre is not null;
        }

        string? nas = null;
        if (!string.IsNullOrWhiteSpace(Donnees.Nas))
        {
            nas = Identifiants.NormaliserNas(Donnees.Nas);
            if (nas is null) ModelState.AddModelError("Donnees.Nas", "NAS invalide (9 chiffres, clé de contrôle).");
        }
        if (!ModelState.IsValid) return Page();

        if (Donnees.EstPayeur) await RetirerPayeurAsync(ParentId);

        var nouveau = lien is null;
        var p = lien?.Parent ?? new Parent { CreeLe = DateTime.UtcNow };
        p.Prenom = Donnees.Prenom.Trim();
        p.Nom = Donnees.Nom.Trim();
        p.Courriel = Vide(Donnees.Courriel);
        p.Telephone1 = Vide(Donnees.Telephone1);
        p.Telephone2 = Vide(Donnees.Telephone2);
        p.Adresse = Vide(Donnees.Adresse);
        p.ModifieLe = DateTime.UtcNow;
        if (nas is not null) p.NasChiffre = chiffreur.ChiffrerTexte(nas, Contextes.NasParent);
        else if (Donnees.EffacerNas) p.NasChiffre = null;

        if (lien is null)
        {
            var ordre = await db.EnfantParents.CountAsync(x => x.EnfantId == EnfantId);
            lien = new EnfantParent { EnfantId = EnfantId, Parent = p, Ordre = ordre };
            db.EnfantParents.Add(lien);
        }
        lien.Lien = Donnees.Lien.Trim();
        lien.EstPayeur = Donnees.EstPayeur;
        await db.SaveChangesAsync();

        journal.Noter(nouveau ? "parent.creer" : "parent.modifier", "Parent", p.Id,
            $"{p.NomComplet}{(nas is not null ? " ; NAS modifié" : Donnees.EffacerNas ? " ; NAS effacé" : "")}");
        await db.SaveChangesAsync();
        TempData["Message"] = "Parent enregistré.";
        return RedirectToPage("Dossier", new { id = EnfantId });
    }

    /// <summary>Un seul parent payeur par enfant / One paying parent per child.</summary>
    private async Task RetirerPayeurAsync(int? sauf = null) =>
        await db.EnfantParents.Where(x => x.EnfantId == EnfantId && x.EstPayeur && x.ParentId != sauf)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.EstPayeur, false));

    private static string? Vide(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
