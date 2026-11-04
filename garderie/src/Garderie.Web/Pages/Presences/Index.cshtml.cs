using Garderie.Web.Data;
using Garderie.Web.Securite;
using Garderie.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Garderie.Web.Pages.Presences;

/// <summary>
/// Presences de la semaine : 2 cases par jour (matin, apres-midi) et par enfant, comme le FO-0310 ; P propose
/// du lundi au vendredi, F les jours de fermeture. Rien n'est enregistre avant « Enregistrer la semaine ».
/// Weekly attendance: 2 cells per day and child, P suggested by default, F on closures.
/// </summary>
public class IndexModel(PresencesService presences, Journal journal) : PageModel
{
    public sealed class SaisieCode
    {
        public int EnfantId { get; set; }
        public DateOnly Date { get; set; }
        public DemiJournee Demi { get; set; }
        public string? Code { get; set; }
    }

    /// <summary>N'importe quel jour de la semaine voulue / Any day of the wanted week.</summary>
    [BindProperty(SupportsGet = true)] public DateOnly? Semaine { get; set; }
    public DateOnly Lundi { get; private set; }
    public IReadOnlyList<DateOnly> Jours { get; private set; } = [];
    public DateOnly Aujourdhui { get; } = Temps.Aujourdhui();
    public List<LigneSemaine> Lignes { get; private set; } = new();
    public bool AuMoinsUnPropose => Lignes.Any(l => l.Jours.Any(j => j.Inscrit && new[] { j.Matin, j.Pm }.Any(c => !c.Enregistre && c.Code != "")));

    public async Task OnGetAsync()
    {
        Lundi = PresencesService.Lundi(Semaine ?? Aujourdhui);
        Jours = PresencesService.JoursOuvrables(Lundi);
        Lignes = await presences.ChargerSemaineAsync(Lundi);
    }

    public async Task<IActionResult> OnPostAsync(List<SaisieCode> codes)
    {
        if (Semaine is null) return BadRequest();
        var lundi = PresencesService.Lundi(Semaine.Value);
        var jours = PresencesService.JoursOuvrables(lundi);
        // Seulement les jours de la semaine affichee / Only the days of the displayed week
        var liste = codes.Where(c => jours.Contains(c.Date)).Select(c => (c.EnfantId, c.Date, c.Demi, c.Code ?? ""));
        var n = await presences.EnregistrerAsync(liste, User.Identity?.Name ?? "?");
        await journal.NoterMaintenantAsync("presences.enregistrer", details: $"semaine du {lundi:yyyy-MM-dd} : {n} changement(s)");
        TempData["Message"] = $"Semaine du {Affichage.Date(lundi)} enregistrée ({n} changement(s)).";
        return RedirectToPage(new { semaine = lundi.ToString("yyyy-MM-dd") });
    }
}
