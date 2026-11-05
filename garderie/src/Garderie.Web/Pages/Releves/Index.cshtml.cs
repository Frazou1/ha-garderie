using Garderie.Web.Data;
using Garderie.Web.Infrastructure;
using Garderie.Web.Securite;
using Garderie.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Garderie.Web.Pages.Releves;

/// <summary>
/// Releves 24 d'une annee : un par parent payeur, etat (a produire, produit, modifie), sommaire 24.
/// RL-24 slips of a year: one per paying parent, status, RL-24.S summary.
/// </summary>
public class IndexModel(GarderieDb db, RelevesService releves, Journal journal) : PageModel
{
    [BindProperty(SupportsGet = true)] public int? Annee { get; set; }
    public int AnneeAffichee { get; private set; }
    public List<RelevesService.Proposition> Propositions { get; private set; } = new();
    public List<string> Manque { get; private set; } = new();
    public (int Releves, int Enfants, int Jours, decimal Admissible) Totaux { get; private set; }

    public async Task OnGetAsync()
    {
        AnneeAffichee = Annee is >= 2000 and <= 2100 ? Annee.Value : RelevesService.AnneeProposee(Temps.Aujourdhui());
        Propositions = await releves.PropositionsAsync(AnneeAffichee);
        Manque = await releves.PrerequisAsync();
        Totaux = await releves.TotauxAsync(AnneeAffichee);
    }

    public async Task<IActionResult> OnGetPdfAsync(int id, bool telecharger = false)
    {
        var r = await db.Releves.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (r is null || await releves.PdfAsync(r) is not { } pdf) return NotFound();
        var c = RelevesService.LireContenu(r);
        await journal.NoterMaintenantAsync("releve24.afficher", "Enfant", r.EnfantId, $"{r.Annee} no {r.Numero}");
        return this.Fichier(pdf, "application/pdf", $"Relevé 24 {r.Annee} {c.PayeurPrenom} {c.PayeurNom}.pdf", !telecharger);
    }

    public async Task<IActionResult> OnGetSommaireAsync(int annee, bool telecharger = false)
    {
        if ((await releves.PrerequisAsync()).Count > 0 || (await releves.TotauxAsync(annee)).Releves == 0) return NotFound();
        await journal.NoterMaintenantAsync("releve24.sommaire", details: annee.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return this.Fichier(await releves.SommaireAsync(annee), "application/pdf", $"Sommaire 24 {annee}.pdf", !telecharger);
    }
}
