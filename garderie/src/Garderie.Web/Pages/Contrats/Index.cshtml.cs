using Garderie.Web.Data;
using Garderie.Web.Securite;
using Garderie.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Garderie.Web.Pages.Contrats;

/// <summary>
/// Contrats : dernier contrat de chaque enfant inscrit, creation, contresignature groupee de la RSGE.
/// Contracts: latest contract per enrolled child, creation, RSGE batch countersignature.
/// </summary>
public class IndexModel(GarderieDb db, ContratsService contrats, Journal journal) : PageModel
{
    public sealed record Ligne(Enfant Enfant, Contrat? Contrat, bool AParent);

    public List<Ligne> Lignes { get; private set; } = new();
    public DateOnly Aujourdhui { get; } = Temps.Aujourdhui();
    public int AContresigner => Lignes.Count(l => l.Contrat?.Etat == EtatContrat.SigneParent);

    public async Task OnGetAsync()
    {
        var enfants = await db.Enfants.AsNoTracking().Where(e => e.DateFin == null || e.DateFin >= Aujourdhui)
            .OrderBy(e => e.Prenom).ThenBy(e => e.Nom).ToListAsync();
        var ids = enfants.Select(e => e.Id).ToList();
        var tous = await db.Contrats.AsNoTracking().Where(c => ids.Contains(c.EnfantId)).ToListAsync();
        var avecParent = (await db.EnfantParents.AsNoTracking().Where(x => ids.Contains(x.EnfantId)).Select(x => x.EnfantId).ToListAsync()).ToHashSet();
        Lignes = enfants.Select(e => new Ligne(e,
            tous.Where(c => c.EnfantId == e.Id).OrderByDescending(c => c.Debut).ThenByDescending(c => c.Id).FirstOrDefault(),
            avecParent.Contains(e.Id))).ToList();
    }

    /// <summary>Nouveau contrat (brouillon) / New contract (draft).</summary>
    public async Task<IActionResult> OnPostCreerAsync(int enfantId, DateOnly? debut, DateOnly? fin)
    {
        var e = await db.Enfants.AsNoTracking().FirstOrDefaultAsync(x => x.Id == enfantId);
        if (e is null) return NotFound();
        var d = debut ?? (e.DateDebut > Aujourdhui ? e.DateDebut : Aujourdhui);
        var f = fin ?? d.AddYears(1).AddDays(-1);
        if (f < d) { TempData["Erreur"] = "La date de fin précède la date de début."; return RedirectToPage(); }
        var c = new Contrat { EnfantId = enfantId, Debut = d, Fin = f, CreeLe = DateTime.UtcNow, ModifieLe = DateTime.UtcNow };
        db.Contrats.Add(c);
        await db.SaveChangesAsync();
        await journal.NoterMaintenantAsync("contrat.creer", "Enfant", enfantId, $"{d:yyyy-MM-dd} → {f:yyyy-MM-dd}");
        return RedirectToPage("Contrat", new { id = c.Id });
    }

    /// <summary>Une signature de la RSGE pour tous les contrats coches / One RSGE signature for all checked contracts.</summary>
    public async Task<IActionResult> OnPostContresignerAsync(List<int> contratIds, string? signature)
    {
        var png = FichesService.LireSignature(signature);
        if (png is null) { TempData["Erreur"] = "Signez dans le cadre avant d'appuyer sur Signer."; return RedirectToPage(); }
        var n = 0;
        var erreurs = new List<string>();
        foreach (var id in contratIds.Distinct())
        {
            var erreur = await contrats.SignerRsgeAsync(id, png, User.Identity?.Name);
            if (erreur is not null) { erreurs.Add(erreur); continue; }
            var c = await db.Contrats.AsNoTracking().FirstAsync(x => x.Id == id);
            journal.Noter("contrat.signer_rsge", "Enfant", c.EnfantId, $"{c.Debut:yyyy-MM-dd} (groupée)");
            n++;
        }
        await db.SaveChangesAsync();
        if (n > 0) TempData["Message"] = $"{n} contrat(s) signé(s) ; une copie est envoyée aux parents.";
        if (erreurs.Count > 0) TempData["Erreur"] = string.Join(" ", erreurs);
        return RedirectToPage();
    }
}
