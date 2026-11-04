using Garderie.Web.Data;
using Garderie.Web.Securite;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Garderie.Web.Pages;

/// <summary>
/// Parametres de la garderie, saisis dans l'appli (jamais dans le code public).
/// Daycare settings, entered in the app (never in the public code).
/// </summary>
public class ParametresModel(GarderieDb db, Journal journal) : PageModel
{
    [BindProperty] public Parametres Donnees { get; set; } = new();

    public async Task OnGetAsync() => Donnees = await db.Parametres.FindAsync(1) ?? new Parametres();

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        var p = await db.Parametres.FindAsync(1);
        if (p is null) { p = new Parametres { Id = 1 }; db.Parametres.Add(p); }
        p.NomGarderie = Vide(Donnees.NomGarderie);
        p.NomRsge = Vide(Donnees.NomRsge);
        p.Adresse = Vide(Donnees.Adresse);
        p.Telephone = Vide(Donnees.Telephone);
        p.CourrielRsge = Vide(Donnees.CourrielRsge);
        p.NomBureauCoordonnateur = Vide(Donnees.NomBureauCoordonnateur);
        p.HeureOuverture = Donnees.HeureOuverture;
        p.HeureFermeture = Donnees.HeureFermeture;
        journal.Noter("parametres.modifier");
        await db.SaveChangesAsync();
        TempData["Message"] = "Paramètres enregistrés.";
        return RedirectToPage();
    }

    private static string? Vide(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
