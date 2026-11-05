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

    public bool CourrielConfigure => releves.CourrielConfigure;

    /// <summary>Envoie un releve par courriel (consentement requis) / Emails one slip (consent required).</summary>
    public async Task<IActionResult> OnPostEnvoyerAsync(int id)
    {
        var r = await db.Releves.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (r is null) return NotFound();
        try
        {
            var (a, erreur) = await releves.EnvoyerAsync(id);
            if (a is null) TempData["Erreur"] = erreur;
            else
            {
                await journal.NoterMaintenantAsync("releve24.envoyer", "Enfant", r.EnfantId, $"{r.Annee} no {r.Numero}");
                TempData["Message"] = $"Relevé no {r.Numero} envoyé à {a}.";
            }
        }
        catch (Exception ex) { TempData["Erreur"] = $"L'envoi a échoué ({ex.GetType().Name})."; }
        return RedirectToPage(new { annee = r.Annee });
    }

    /// <summary>Envoie les releves pas encore envoyes des parents qui ont consenti / Emails all unsent consented slips.</summary>
    public async Task<IActionResult> OnPostEnvoyerToutAsync(int annee)
    {
        var aEnvoyer = (await releves.PropositionsAsync(annee)).Where(p => p.ParCourriel && p.Dernier is { EnvoyeLe: null }).Select(p => p.Dernier!).ToList();
        var n = 0;
        var erreurs = new List<string>();
        foreach (var r in aEnvoyer)
        {
            try
            {
                var (a, erreur) = await releves.EnvoyerAsync(r.Id);
                if (a is null) { erreurs.Add(erreur!); continue; }
                journal.Noter("releve24.envoyer", "Enfant", r.EnfantId, $"{r.Annee} no {r.Numero} (groupé)");
                n++;
            }
            catch (Exception ex) { erreurs.Add($"No {r.Numero} : envoi échoué ({ex.GetType().Name})."); }
        }
        await db.SaveChangesAsync();
        if (n > 0) TempData["Message"] = $"{n} relevé(s) envoyé(s) par courriel.";
        if (erreurs.Count > 0) TempData["Erreur"] = string.Join(" ", erreurs);
        return RedirectToPage(new { annee });
    }

    public async Task<IActionResult> OnGetSommaireAsync(int annee, bool telecharger = false)
    {
        if ((await releves.PrerequisAsync()).Count > 0 || (await releves.TotauxAsync(annee)).Releves == 0) return NotFound();
        await journal.NoterMaintenantAsync("releve24.sommaire", details: annee.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return this.Fichier(await releves.SommaireAsync(annee), "application/pdf", $"Sommaire 24 {annee}.pdf", !telecharger);
    }
}
