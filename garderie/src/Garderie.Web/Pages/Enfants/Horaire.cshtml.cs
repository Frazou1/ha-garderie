using Garderie.Web.Infrastructure;
using Garderie.Web.Data;
using Garderie.Web.Securite;
using Garderie.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Garderie.Web.Pages.Enfants;

/// <summary>
/// Horaire prevu par demi-journee. Une nouvelle date d'effet cree une nouvelle version (l'historique
/// reste valable pour les fiches passees) ; la meme date remplace la version.
/// Planned schedule per half-day; a new effective date creates a new version.
/// </summary>
[LectureSeuleSiAncien("id")]
public class HoraireModel(GarderieDb db, Journal journal) : PageModel
{
    [BindProperty(SupportsGet = true)] public int Id { get; set; }
    [BindProperty] public DateOnly DateEffective { get; set; }
    /// <summary>Cases cochees : "1-M", "1-P" (jour 0-6, M = matin, P = apres-midi) / Checked boxes.</summary>
    [BindProperty] public List<string> Cases { get; set; } = new();
    /// <summary>Heures par jour (cle = jour 0-6) / Hours per day (key = day 0-6).</summary>
    [BindProperty] public Dictionary<int, TimeOnly?> Arrivees { get; set; } = new();
    [BindProperty] public Dictionary<int, TimeOnly?> Departs { get; set; } = new();

    public Enfant Enfant { get; private set; } = null!;
    public List<DateOnly> Versions { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(DateOnly? version)
    {
        var e = await db.Enfants.AsNoTracking().Include(x => x.Horaires).FirstOrDefaultAsync(x => x.Id == Id);
        if (e is null) return NotFound();
        Enfant = e;
        Versions = e.Horaires.Select(h => h.DateEffective).Distinct().OrderByDescending(d => d).ToList();

        var aujourdhui = Temps.Aujourdhui();
        var base_ = version ?? Versions.FirstOrDefault(v => v <= aujourdhui);
        if (base_ == default && Versions.Count > 0) base_ = Versions[0];
        DateEffective = version ?? (Versions.Count == 0 ? (e.DateDebut > aujourdhui ? e.DateDebut : aujourdhui) : aujourdhui);
        Cases = e.Horaires.Where(h => h.DateEffective == base_)
            .SelectMany(h => new[] { h.Matin ? $"{(int)h.Jour}-M" : null, h.Pm ? $"{(int)h.Jour}-P" : null })
            .OfType<string>().ToList();
        foreach (var h in e.Horaires.Where(h => h.DateEffective == base_))
        {
            Arrivees[(int)h.Jour] = h.Arrivee;
            Departs[(int)h.Jour] = h.Depart;
        }
        if (Versions.Count == 0)
        {
            // Defaut : lundi au vendredi, journee complete / Default: Mon-Fri, full day
            Cases = Enumerable.Range(1, 5).SelectMany(j => new[] { $"{j}-M", $"{j}-P" }).ToList();
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!await db.Enfants.AnyAsync(x => x.Id == Id)) return NotFound();
        if (DateEffective == default)
        {
            TempData["Erreur"] = "Date d'effet requise.";
            return RedirectToPage(new { id = Id });
        }
        var anciennes = await db.Horaires.Where(h => h.EnfantId == Id && h.DateEffective == DateEffective).ToListAsync();
        db.Horaires.RemoveRange(anciennes);
        foreach (var j in Affichage.JoursSemaine)
        {
            var m = Cases.Contains($"{(int)j}-M");
            var p = Cases.Contains($"{(int)j}-P");
            var a = m || p ? Arrivees.GetValueOrDefault((int)j) : null;
            var d = m || p ? Departs.GetValueOrDefault((int)j) : null;
            if (a is not null && d is not null && d <= a) (a, d) = (null, null);   // incoherent : ignore / inconsistent: ignored
            db.Horaires.Add(new HoraireType { EnfantId = Id, DateEffective = DateEffective, Jour = j, Matin = m, Pm = p, Arrivee = a, Depart = d });
        }
        var heures = string.Join(",", Arrivees.Where(x => x.Value is not null).Select(x => $"{x.Key}:{x.Value:HH\\:mm}-{Departs.GetValueOrDefault(x.Key):HH\\:mm}"));
        journal.Noter("horaire.modifier", "Enfant", Id, $"En vigueur le {DateEffective:yyyy-MM-dd} : {string.Join(",", Cases.Order())}{(heures.Length > 0 ? " ; heures " + heures : "")}");
        await db.SaveChangesAsync();
        TempData["Message"] = "Horaire enregistré.";
        return RedirectToPage("Dossier", new { id = Id });
    }

    public async Task<IActionResult> OnPostSupprimerVersionAsync(DateOnly version)
    {
        var lignes = await db.Horaires.Where(h => h.EnfantId == Id && h.DateEffective == version).ToListAsync();
        db.Horaires.RemoveRange(lignes);
        journal.Noter("horaire.supprimer", "Enfant", Id, $"Version du {version:yyyy-MM-dd}");
        await db.SaveChangesAsync();
        TempData["Message"] = "Version supprimée.";
        return RedirectToPage(new { id = Id });
    }
}
