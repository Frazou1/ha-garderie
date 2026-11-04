using System.ComponentModel.DataAnnotations;
using Garderie.Web.Data;
using Garderie.Web.Securite;
using Garderie.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Garderie.Web.Pages.Enfants;

/// <summary>Creation / modification de l'identite de l'enfant / Create or edit the child's identity.</summary>
public class EditionModel(GarderieDb db, Chiffreur chiffreur, Journal journal) : PageModel
{
    public sealed class Saisie
    {
        [Required(ErrorMessage = "Prénom requis"), MaxLength(100)] public string Prenom { get; set; } = "";
        [Required(ErrorMessage = "Nom requis"), MaxLength(100)] public string Nom { get; set; } = "";
        public DateOnly? DateNaissance { get; set; }
        public Sexe? Sexe { get; set; }
        [MaxLength(300)] public string? Adresse { get; set; }
        [MaxLength(30)] public string? Telephone { get; set; }
        /// <summary>Nouveau NAM (vide = inchange) / New health number (empty = unchanged).</summary>
        [MaxLength(20)] public string? Nam { get; set; }
        public bool EffacerNam { get; set; }
        [Required(ErrorMessage = "Date de début requise")] public DateOnly? DateDebut { get; set; }
        public DateOnly? DateFin { get; set; }
        [MaxLength(2000)] public string? NotesSante { get; set; }
        [MaxLength(2000)] public string? Notes { get; set; }
    }

    [BindProperty(SupportsGet = true)] public int? Id { get; set; }
    [BindProperty] public Saisie Donnees { get; set; } = new();
    public string? NamMasque { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (Id is null)
        {
            Donnees.DateDebut = Temps.Aujourdhui();
            return Page();
        }
        var e = await db.Enfants.FindAsync(Id);
        if (e is null) return NotFound();
        Donnees = new Saisie
        {
            Prenom = e.Prenom, Nom = e.Nom, DateNaissance = e.DateNaissance, Sexe = e.Sexe, Adresse = e.Adresse,
            Telephone = e.Telephone, DateDebut = e.DateDebut, DateFin = e.DateFin, NotesSante = e.NotesSante, Notes = e.Notes,
        };
        NamMasque = Masque(e);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Enfant? e = null;
        if (Id is not null)
        {
            e = await db.Enfants.FindAsync(Id);
            if (e is null) return NotFound();
            NamMasque = Masque(e);
        }

        string? nam = null;
        if (!string.IsNullOrWhiteSpace(Donnees.Nam))
        {
            nam = Identifiants.NormaliserNam(Donnees.Nam);
            if (nam is null) ModelState.AddModelError("Donnees.Nam", "NAM invalide (4 lettres et 8 chiffres).");
        }
        if (Donnees.DateFin is not null && Donnees.DateDebut is not null && Donnees.DateFin < Donnees.DateDebut)
            ModelState.AddModelError("Donnees.DateFin", "La date de fin précède la date de début.");
        if (!ModelState.IsValid) return Page();

        var nouveau = e is null;
        e ??= new Enfant { CreeLe = DateTime.UtcNow };
        e.Prenom = Donnees.Prenom.Trim();
        e.Nom = Donnees.Nom.Trim();
        e.DateNaissance = Donnees.DateNaissance;
        e.Sexe = Donnees.Sexe;
        e.Adresse = Vide(Donnees.Adresse);
        e.Telephone = Vide(Donnees.Telephone);
        e.DateDebut = Donnees.DateDebut!.Value;
        e.DateFin = Donnees.DateFin;
        e.NotesSante = Vide(Donnees.NotesSante);
        e.Notes = Vide(Donnees.Notes);
        e.ModifieLe = DateTime.UtcNow;
        if (nam is not null) e.NamChiffre = chiffreur.ChiffrerTexte(nam, Contextes.NamEnfant);
        else if (Donnees.EffacerNam) e.NamChiffre = null;

        if (nouveau) db.Enfants.Add(e);
        await db.SaveChangesAsync();
        journal.Noter(nouveau ? "enfant.creer" : "enfant.modifier", "Enfant", e.Id,
            nam is not null ? "NAM modifié" : Donnees.EffacerNam ? "NAM effacé" : null);
        await db.SaveChangesAsync();

        TempData["Message"] = nouveau ? "Dossier créé." : "Dossier enregistré.";
        return RedirectToPage("Dossier", new { id = e.Id });
    }

    private string? Masque(Enfant e)
    {
        var nam = chiffreur.DechiffrerTexte(e.NamChiffre, Contextes.NamEnfant);
        return nam is null ? null : Identifiants.MasquerNam(nam);
    }

    private static string? Vide(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
