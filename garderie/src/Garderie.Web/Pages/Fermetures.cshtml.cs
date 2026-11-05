using Garderie.Web.Data;
using Garderie.Web.Securite;
using Garderie.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Garderie.Web.Pages;

/// <summary>Jours de fermeture (pre-remplissent « F ») / Closure days (prefill "F").</summary>
public class FermeturesModel(GarderieDb db, Journal journal) : PageModel
{
    public List<JourFermeture> Fermetures { get; private set; } = new();
    public bool Passees { get; private set; }

    public async Task OnGetAsync(bool passees = false)
    {
        Passees = passees;
        var q = db.Fermetures.AsNoTracking();
        if (!passees) q = q.Where(f => f.Date >= Temps.Aujourdhui().AddDays(-14));
        Fermetures = await q.OrderBy(f => f.Date).ToListAsync();
    }

    public async Task<IActionResult> OnPostAsync(DateOnly? debut, DateOnly? fin, string? demi, string? motif)
    {
        if (debut is null) { TempData["Erreur"] = "Date requise."; return RedirectToPage(); }
        var dernier = fin ?? debut.Value;
        if (dernier < debut || dernier.DayNumber - debut.Value.DayNumber > 60)
        {
            TempData["Erreur"] = "Période invalide (60 jours au plus).";
            return RedirectToPage();
        }
        DemiJournee? d = demi switch { "Matin" => DemiJournee.Matin, "Pm" => DemiJournee.Pm, _ => null };
        if (d is not null && dernier != debut) { TempData["Erreur"] = "Une demi-journée ne s'applique qu'à une seule date."; return RedirectToPage(); }

        var texte = string.IsNullOrWhiteSpace(motif) ? null : motif.Trim()[..Math.Min(200, motif.Trim().Length)];
        var existants = await db.Fermetures.Where(f => f.Date >= debut && f.Date <= dernier).Select(f => f.Date).ToListAsync();
        var n = 0;
        for (var jour = debut.Value; jour <= dernier; jour = jour.AddDays(1))
        {
            if (existants.Contains(jour)) continue;
            // Fins de semaine ignorees pour une periode / Weekends skipped for a range
            if (dernier != debut && jour.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;
            db.Fermetures.Add(new JourFermeture { Date = jour, Demi = d, Motif = texte });
            n++;
        }
        journal.Noter("fermeture.ajouter", details: $"{debut:yyyy-MM-dd} → {dernier:yyyy-MM-dd} ({n} j) {texte}");
        await db.SaveChangesAsync();
        TempData["Message"] = n == 0 ? "Aucun jour ajouté (déjà présents)." : $"{n} jour(s) de fermeture ajouté(s).";
        return RedirectToPage();
    }

    /// <summary>Ajoute les jours feries du Quebec d'une annee (sans doublon) / Adds a year's Quebec holidays.</summary>
    public async Task<IActionResult> OnPostFeriesAsync(int annee)
    {
        if (annee is < 2020 or > 2100) { TempData["Erreur"] = "Année invalide."; return RedirectToPage(); }
        var feries = Feries.Calculer(annee);
        var dates = feries.Select(f => f.Date).ToList();
        var existants = await db.Fermetures.Where(f => dates.Contains(f.Date)).Select(f => f.Date).ToListAsync();
        var n = 0;
        foreach (var (date, nom) in feries.Where(f => !existants.Contains(f.Date)))
        {
            db.Fermetures.Add(new JourFermeture { Date = date, Motif = nom });
            n++;
        }
        journal.Noter("fermeture.feries", details: $"{annee} : {n} jour(s)");
        await db.SaveChangesAsync();
        TempData["Message"] = n == 0 ? $"Les jours fériés de {annee} sont déjà inscrits." : $"{n} jour(s) férié(s) de {annee} ajouté(s).";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSupprimerAsync(int id)
    {
        var f = await db.Fermetures.FindAsync(id);
        if (f is not null)
        {
            db.Fermetures.Remove(f);
            journal.Noter("fermeture.supprimer", details: $"{f.Date:yyyy-MM-dd}");
            await db.SaveChangesAsync();
        }
        return RedirectToPage();
    }
}
