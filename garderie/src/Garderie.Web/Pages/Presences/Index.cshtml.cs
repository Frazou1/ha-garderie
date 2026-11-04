using Garderie.Web.Data;
using Garderie.Web.Securite;
using Garderie.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Garderie.Web.Pages.Presences;

/// <summary>
/// Presences du jour : enfants prevus, code propose selon l'horaire, un tap pour corriger, puis Enregistrer.
/// Today's attendance: planned children, suggested code, one tap to fix, then Save.
/// </summary>
public class IndexModel(PresencesService presences, Journal journal) : PageModel
{
    public sealed class SaisieCode
    {
        public int EnfantId { get; set; }
        public DemiJournee Demi { get; set; }
        /// <summary>Code rapide (boutons) / Quick code (buttons).</summary>
        public string? Code { get; set; }
        /// <summary>Code moins courant (liste), prioritaire / Less common code (list), takes precedence.</summary>
        public string? Autre { get; set; }
    }

    /// <summary>Codes en boutons ; les autres sont dans la liste « Autre » / Button codes; others in the list.</summary>
    public static readonly string[] CodesRapides = ["P", "P 1/2", "A", "A 1/2", "F"];
    public static IEnumerable<CodesPresence.Code> CodesAutres => CodesPresence.Tous.Where(c => !CodesRapides.Contains(c.Valeur));

    [BindProperty(SupportsGet = true)] public DateOnly? Date { get; set; }
    public DateOnly Jour { get; private set; }
    public List<LignePresence> Lignes { get; private set; } = new();
    public bool AuMoinsUnPropose => Lignes.Any(l => (!l.Matin.Enregistre && l.Matin.Code != "") || (!l.Pm.Enregistre && l.Pm.Code != ""));

    public async Task OnGetAsync()
    {
        Jour = Date ?? Temps.Aujourdhui();
        Lignes = await presences.ChargerJourAsync(Jour);
    }

    public async Task<IActionResult> OnPostAsync(List<SaisieCode> codes)
    {
        if (Date is null) return BadRequest();
        var liste = codes.Select(c => (c.EnfantId, c.Demi, string.IsNullOrEmpty(c.Autre) ? c.Code ?? "" : c.Autre));
        var n = await presences.EnregistrerJourAsync(Date.Value, liste, User.Identity?.Name ?? "?");
        await journal.NoterMaintenantAsync("presences.enregistrer", details: $"{Date:yyyy-MM-dd} : {n} changement(s)");
        TempData["Message"] = $"Présences du {Affichage.Date(Date)} enregistrées ({n} changement(s)).";
        return RedirectToPage(new { date = Date.Value.ToString("yyyy-MM-dd") });
    }
}
