using Garderie.Web.Infrastructure;
using Garderie.Web.Configuration;
using Garderie.Web.Data;
using Garderie.Web.Securite;
using Garderie.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Garderie.Web.Pages.Contrats;

/// <summary>
/// Un contrat : dates (brouillon), envoi au parent (courriel, lien, sur place), contresignature, PDF, resiliation.
/// One contract: dates (draft), sending to the parent, countersignature, PDF, termination.
/// </summary>
public class ContratModel(GarderieDb db, ContratsService contrats, Courriels courriels, GarderieOptions options, Journal journal) : PageModel
{
    [BindProperty(SupportsGet = true)] public int Id { get; set; }
    public Contrat Contrat { get; private set; } = null!;
    public InfosContrat Infos { get; private set; } = null!;
    public EnfantParent? Parent { get; private set; }
    public bool EnvoiPossible => courriels.EstConfigure && !string.IsNullOrWhiteSpace(options.UrlPublique);
    public bool UrlPubliqueConfiguree => !string.IsNullOrWhiteSpace(options.UrlPublique);
    public bool LienActif { get; private set; }

    private async Task<bool> ChargerAsync()
    {
        var c = await db.Contrats.Include(x => x.Enfant).FirstOrDefaultAsync(x => x.Id == Id);
        if (c is null) return false;
        Contrat = c;
        Infos = await contrats.InfosAsync(c);
        Parent = await contrats.LienParentAsync(c);
        LienActif = await db.LiensContrats.AnyAsync(l => l.ContratId == Id && l.UtiliseLe == null && l.AnnuleLe == null && l.ExpireLe > DateTime.UtcNow);
        return true;
    }

    private IActionResult Retour(string? message = null, string? erreur = null)
    {
        if (message is not null) TempData["Message"] = message;
        if (erreur is not null) TempData["Erreur"] = erreur;
        return RedirectToPage(new { id = Id });
    }

    public async Task<IActionResult> OnGetAsync() => await ChargerAsync() ? Page() : NotFound();

    public async Task<IActionResult> OnPostDatesAsync(DateOnly debut, DateOnly fin)
    {
        if (!await ChargerAsync()) return NotFound();
        if (Contrat.Etat != EtatContrat.Brouillon) return Retour(erreur: "Le contrat est déjà signé.");
        if (fin < debut) return Retour(erreur: "La date de fin précède la date de début.");
        Contrat.Debut = debut; Contrat.Fin = fin; Contrat.ModifieLe = DateTime.UtcNow;
        journal.Noter("contrat.dates", "Enfant", Contrat.EnfantId, $"{debut:yyyy-MM-dd} → {fin:yyyy-MM-dd}");
        await db.SaveChangesAsync();
        return Retour("Dates enregistrées.");
    }

    public async Task<IActionResult> OnPostEnvoyerAsync(ButLien but = ButLien.Signature)
    {
        if (!await ChargerAsync()) return NotFound();
        if (!AttendLeParent(but)) return Retour(erreur: "Ce contrat n'attend pas de signature du parent.");
        if (!EnvoiPossible) return Retour(erreur: "Le courriel ou l'adresse publique n'est pas configuré : copiez le lien ou faites signer sur place.");
        try
        {
            var a = await contrats.EnvoyerAsync(Contrat, but);
            await journal.NoterMaintenantAsync(but == ButLien.Signature ? "contrat.lien_envoye" : "contrat.resiliation_envoyee", "Enfant", Contrat.EnfantId, a);
            return Retour($"Lien envoyé à {a}.");
        }
        catch (InvalidOperationException ex) { return Retour(erreur: ex.Message); }
        catch (Exception ex) { return Retour(erreur: $"L'envoi du courriel a échoué ({ex.GetType().Name})."); }
    }

    public async Task<IActionResult> OnPostLienAsync(ButLien but = ButLien.Signature)
    {
        if (!await ChargerAsync()) return NotFound();
        if (!AttendLeParent(but)) return Retour(erreur: "Ce contrat n'attend pas de signature du parent.");
        if (!UrlPubliqueConfiguree) return Retour(erreur: "L'adresse publique n'est pas configurée : faites signer sur place.");
        var jeton = await contrats.CreerLienAsync(Contrat, but, null, surPlace: false);
        await journal.NoterMaintenantAsync("contrat.lien_cree", "Enfant", Contrat.EnfantId, but.ToString());
        TempData["Lien"] = contrats.UrlLien(jeton);
        return Retour();
    }

    public async Task<IActionResult> OnPostSurPlaceAsync(ButLien but = ButLien.Signature)
    {
        if (!await ChargerAsync()) return NotFound();
        if (!AttendLeParent(but)) return Retour(erreur: "Ce contrat n'attend pas de signature du parent.");
        var jeton = await contrats.CreerLienAsync(Contrat, but, null, surPlace: true);
        await journal.NoterMaintenantAsync("contrat.sur_place", "Enfant", Contrat.EnfantId, but.ToString());
        return RedirectToPage("/Public/Contrat", new { j = jeton });
    }


    private bool AttendLeParent(ButLien but) => but == ButLien.Signature
        ? Contrat.Etat == EtatContrat.Brouillon && Parent is not null
        : Contrat.Etat == EtatContrat.Signe && Contrat.ResilieLe is not null;

    public async Task<IActionResult> OnPostContresignerAsync(string? signature)
    {
        if (!await ChargerAsync()) return NotFound();
        var png = FichesService.LireSignature(signature);
        if (png is null) return Retour(erreur: "Signez dans le cadre avant d'appuyer sur Signer.");
        var erreur = await contrats.SignerRsgeAsync(Id, png, User.Identity?.Name);
        if (erreur is not null) return Retour(erreur: erreur);
        await journal.NoterMaintenantAsync("contrat.signer_rsge", "Enfant", Contrat.EnfantId, $"{Contrat.Debut:yyyy-MM-dd}");
        return Retour("Contrat signé ; une copie est envoyée au parent.");
    }

    /// <summary>Retour au brouillon (correction) avant la contresignature / Back to draft before countersigning.</summary>
    public async Task<IActionResult> OnPostReprendreAsync()
    {
        if (!await ChargerAsync()) return NotFound();
        if (Contrat.Etat != EtatContrat.SigneParent) return Retour(erreur: "Seul un contrat signé par le parent seulement peut être repris.");
        Contrat.Etat = EtatContrat.Brouillon;
        Contrat.Fige = null; Contrat.NasChiffre = null;
        Contrat.SignatureParentFichier = null; Contrat.SigneParentLe = null; Contrat.SignataireParent = null; Contrat.IpParent = null;
        Contrat.ModifieLe = DateTime.UtcNow;
        journal.Noter("contrat.reprendre", "Enfant", Contrat.EnfantId);
        await db.SaveChangesAsync();
        return Retour("Contrat repris : le parent devra signer de nouveau.");
    }

    public async Task<IActionResult> OnPostSupprimerAsync()
    {
        if (!await ChargerAsync()) return NotFound();
        if (Contrat.Etat != EtatContrat.Brouillon) return Retour(erreur: "Seul un brouillon peut être supprimé.");
        db.Contrats.Remove(Contrat);
        journal.Noter("contrat.supprimer", "Enfant", Contrat.EnfantId);
        await db.SaveChangesAsync();
        TempData["Message"] = "Brouillon supprimé.";
        return RedirectToPage("Index");
    }

    /// <summary>Prepare la resiliation (date) ; le parent signe ensuite la formule / Prepares the termination.</summary>
    public async Task<IActionResult> OnPostResilierAsync(DateOnly? date)
    {
        if (!await ChargerAsync()) return NotFound();
        var erreur = await contrats.DemanderResiliationAsync(Contrat, date ?? Temps.Aujourdhui());
        if (erreur is not null) return Retour(erreur: erreur);
        await journal.NoterMaintenantAsync("contrat.resiliation_preparee", "Enfant", Contrat.EnfantId, $"{Contrat.ResilieLe:yyyy-MM-dd}");
        return Retour("Résiliation préparée : faites signer la formule par le parent.");
    }

    public async Task<IActionResult> OnPostAnnulerResiliationAsync()
    {
        if (!await ChargerAsync()) return NotFound();
        if (Contrat.Etat != EtatContrat.Signe || Contrat.ResilieLe is null) return Retour();
        Contrat.ResilieLe = null; Contrat.ModifieLe = DateTime.UtcNow;
        await contrats.AnnulerLiensAsync(Id);
        journal.Noter("contrat.resiliation_annulee", "Enfant", Contrat.EnfantId);
        await db.SaveChangesAsync();
        return Retour("Résiliation annulée.");
    }

    /// <summary>
    /// Renvoie une copie du contrat signe (et de la resiliation) au parent ou a la RSGE.
    /// Re-sends a copy of the signed contract (and termination) to the parent or the RSGE.
    /// </summary>
    public async Task<IActionResult> OnPostCopieAsync(string pour)
    {
        if (!await ChargerAsync()) return NotFound();
        if (!courriels.EstConfigure) return Retour(erreur: "Le courriel n'est pas configuré dans le module.");
        var ids = new[] { Contrat.DocumentId, Contrat.DocumentResiliationId }.OfType<int>().ToList();
        if (ids.Count == 0) return Retour(erreur: "Le contrat n'est pas encore signé par les deux parties.");
        var reglages = await db.Parametres.AsNoTracking().FirstOrDefaultAsync();
        var a = pour == "parent" ? Parent?.Parent.Courriel : reglages?.CourrielRsge;
        if (a is null) return Retour(erreur: pour == "parent" ? "Le parent n'a pas de courriel." : "Le courriel de la RSGE n'est pas dans Réglages.");
        var docs = HttpContext.RequestServices.GetRequiredService<DocumentsService>();
        var pieces = new List<(string, byte[])>();
        foreach (var d in await db.Documents.AsNoTracking().Where(d => ids.Contains(d.Id)).ToListAsync())
            pieces.Add((d.NomOriginal, await docs.LireAsync(d)));
        try
        {
            await courriels.EnvoyerAsync(a, $"Copie : entente de service de {Contrat.Enfant.NomComplet}",
                $"Voici une copie de l'entente de service de {Contrat.Enfant.NomComplet} (du {Affichage.Date(Contrat.Debut)} au {Affichage.Date(Contrat.Fin)})" +
                (Contrat.DocumentResiliationId is null ? "." : ", avec la formule de résiliation.") +
                (pour == "parent" && Affichage.PiedFacebook(reglages) is { Length: > 0 } fb ? "\n\n" + fb.TrimEnd() : ""), pieces);
        }
        catch (Exception ex) { return Retour(erreur: $"L'envoi a échoué ({ex.GetType().Name})."); }
        await journal.NoterMaintenantAsync("contrat.copie_envoyee", "Enfant", Contrat.EnfantId, a);
        return Retour($"Copie envoyée à {a}.");
    }

    /// <summary>PDF : document signe s'il existe, sinon apercu / Signed document if any, else preview.</summary>
    public async Task<IActionResult> OnGetPdfAsync(bool resiliation = false, bool afficher = false)
    {
        if (!await ChargerAsync()) return NotFound();
        var docId = resiliation ? Contrat.DocumentResiliationId : Contrat.DocumentId;
        if (docId is { } did && await db.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == did) is { } doc)
        {
            await journal.NoterMaintenantAsync(afficher ? "contrat.afficher" : "contrat.telecharger", "Enfant", Contrat.EnfantId, doc.NomOriginal);
            var docs = HttpContext.RequestServices.GetRequiredService<DocumentsService>();
            return this.Fichier(await docs.LireAsync(doc), "application/pdf", doc.NomOriginal, afficher);
        }
        var pdf = resiliation ? await contrats.PdfResiliationAsync(Contrat) : await contrats.PdfAsync(Contrat);
        await journal.NoterMaintenantAsync("contrat.apercu", "Enfant", Contrat.EnfantId);
        return this.Fichier(pdf, "application/pdf", "Aperçu " + ContratsService.NomPdf(Contrat, Contrat.Enfant), afficher);
    }
}
