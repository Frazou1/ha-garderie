using Garderie.Web.Configuration;
using Garderie.Web.Data;
using Garderie.Web.Securite;
using Garderie.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Garderie.Web.Pages.Fiches;

/// <summary>
/// Fiches d'assiduite d'une periode de 2 semaines : signature groupee de la RSGE (une signature pour les enfants
/// coches), puis envoi groupe aux parents (un courriel et un lien par parent, pour tous ses enfants).
/// Sheets of a 2-week period: RSGE batch signature, then batch sending (one email and link per parent).
/// </summary>
public class IndexModel(GarderieDb db, FichesService fiches, Courriels courriels, GarderieOptions options, Journal journal) : PageModel
{
    [BindProperty(SupportsGet = true)] public DateOnly? Periode { get; set; }
    public DateOnly Debut { get; private set; }
    public DateOnly Fin => Debut.AddDays(13);
    public DateOnly Courante { get; private set; }
    public List<SituationFiche> Situations { get; private set; } = new();
    public bool ModeleDisponible => fiches.ModeleDisponible;
    public bool EnvoiPossible => courriels.EstConfigure && !string.IsNullOrWhiteSpace(options.UrlPublique);
    /// <summary>Periode signee sur papier (Reglages) / Period signed on paper.</summary>
    public bool Papier { get; private set; }
    public DateOnly? PapierJusquau { get; private set; }
    public int PretesASigner => Situations.Count(s => s.PreteASigner);
    /// <summary>Fiches signees par la RSGE sans lien en cours / RSGE-signed sheets with no active link.</summary>
    public int AEnvoyer => Situations.Count(s => s.Etat == EtatFiche.SigneeRsge && !s.LienActif);

    private async Task ChargerAsync()
    {
        var ancre = await fiches.AncreAsync();
        Courante = FichesService.DebutPeriode(Temps.Aujourdhui(), ancre);
        // Par defaut : la periode en cours / Default: current period
        Debut = FichesService.DebutPeriode(Periode ?? Courante, ancre);
        PapierJusquau = await fiches.PapierJusquauAsync();
        Papier = PapierJusquau is { } p && Fin <= p;
        Situations = Papier ? new() : await fiches.PeriodeAsync(Debut);
    }

    private IActionResult Retour(string? message, string? erreur = null)
    {
        if (message is not null) TempData["Message"] = message;
        if (erreur is not null) TempData["Erreur"] = erreur;
        return RedirectToPage(new { periode = Debut.ToString("yyyy-MM-dd") });
    }

    public async Task OnGetAsync() => await ChargerAsync();

    /// <summary>Une signature de la RSGE pour toutes les fiches cochees / One RSGE signature for all checked sheets.</summary>
    public async Task<IActionResult> OnPostSignerToutAsync(List<int> enfants, string? signature)
    {
        await ChargerAsync();
        var png = FichesService.LireSignature(signature);
        if (png is null) return Retour(null, "Signez dans le cadre avant d'appuyer sur Signer.");
        var choisis = Situations.Where(s => enfants.Contains(s.Enfant.Id)).ToList();
        if (choisis.Count == 0) return Retour(null, "Cochez au moins un enfant.");

        var erreurs = new List<string>();
        var n = 0;
        foreach (var s in choisis)
        {
            var erreur = await fiches.SignerRsgeAsync(s.Enfant.Id, Debut, png, User.Identity?.Name);
            if (erreur is not null) { erreurs.Add(erreur); continue; }
            journal.Noter("fiche.signer_rsge", "Enfant", s.Enfant.Id, $"{Debut:yyyy-MM-dd} (groupée)");
            n++;
        }
        await db.SaveChangesAsync();
        return Retour(n > 0 ? $"{n} fiche(s) signée(s). Envoyez-les maintenant aux parents." : null,
            erreurs.Count > 0 ? string.Join(" ", erreurs) : null);
    }

    /// <summary>
    /// Envoie les fiches signees sans lien en cours : un courriel par parent, qui regroupe tous ses enfants.
    /// Sends signed sheets with no active link: one email per parent, grouping all of their children.
    /// </summary>
    public async Task<IActionResult> OnPostEnvoyerToutAsync()
    {
        await ChargerAsync();
        if (!EnvoiPossible) return Retour(null, "Le courriel ou l'adresse publique n'est pas configuré dans le module.");
        var ids = Situations.Where(s => s.Etat == EtatFiche.SigneeRsge && !s.LienActif).Select(s => s.Fiche!.Id).ToList();
        if (ids.Count == 0) return Retour(null, "Aucune fiche à envoyer.");
        var aEnvoyer = await db.Fiches.Include(f => f.Enfant).Where(f => ids.Contains(f.Id)).ToListAsync();

        var envoyes = new List<string>();
        var erreurs = new List<string>();
        foreach (var groupe in aEnvoyer.GroupBy(f => f.ParentId))
        {
            var liste = groupe.OrderBy(f => f.Enfant.Prenom).ToList();
            var prenoms = FichesService.Liste(liste.Select(f => f.Enfant.Prenom).ToList());
            var parent = groupe.Key is { } pid ? await db.Parents.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pid) : null;
            if (parent?.Courriel is null) { erreurs.Add($"{prenoms} : le parent n'a pas de courriel."); continue; }
            try
            {
                var a = await fiches.EnvoyerLienAsync(liste);
                foreach (var f in liste) journal.Noter("fiche.lien_envoye", "Enfant", f.EnfantId, $"{Debut:yyyy-MM-dd} → {a}");
                envoyes.Add($"{prenoms} → {a}");
            }
            catch (Exception ex)
            {
                erreurs.Add($"{prenoms} : envoi impossible ({ex.GetType().Name}).");
            }
        }
        await db.SaveChangesAsync();
        return Retour(envoyes.Count > 0 ? $"{envoyes.Count} courriel(s) envoyé(s) : {string.Join(" ; ", envoyes)}." : null,
            erreurs.Count > 0 ? string.Join(" ", erreurs) : null);
    }
}
