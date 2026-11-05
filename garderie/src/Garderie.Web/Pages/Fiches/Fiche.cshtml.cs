using Garderie.Web.Infrastructure;
using System.ComponentModel.DataAnnotations;
using Garderie.Web.Configuration;
using Garderie.Web.Data;
using Garderie.Web.Securite;
using Garderie.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Garderie.Web.Pages.Fiches;

/// <summary>
/// Fiche d'un enfant pour 2 semaines : verification des codes, paiements, signature de la RSGE,
/// envoi du lien au parent (courriel, lien a copier ou signature sur place), PDF.
/// One child's sheet: codes check, payments, RSGE signature, parent link, PDF.
/// </summary>
public class FicheModel(GarderieDb db, FichesService fiches, DocumentsService documents, Courriels courriels,
    GarderieOptions options, Journal journal) : PageModel
{
    /// <summary>
    /// Montants en texte : un champ « number » envoie toujours un point, l'appli est en fr-CA (virgule).
    /// Amounts as text: a number input always sends a dot while the app culture is fr-CA (comma).
    /// </summary>
    public sealed class SaisiePaiement
    {
        public DateOnly? DatePaiement1 { get; set; }
        public string? MontantPaye1 { get; set; }
        public string? Solde1 { get; set; }
        public DateOnly? DatePaiement2 { get; set; }
        public string? MontantPaye2 { get; set; }
        public string? Solde2 { get; set; }
    }

    /// <summary>Montant lu avec point ou virgule ; null si vide ; false si invalide / Amount with dot or comma.</summary>
    public static bool LireMontant(string? texte, out decimal? montant)
    {
        montant = null;
        if (string.IsNullOrWhiteSpace(texte)) return true;
        var t = texte.Trim().Replace(" ", "").Replace("$", "").Replace(',', '.');
        if (!decimal.TryParse(t, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var m)
            || m is < -100000 or > 100000) return false;
        montant = Math.Round(m, 2);
        return true;
    }

    private static string? Texte(decimal? m) => m?.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

    [BindProperty(SupportsGet = true)] public int EnfantId { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly Debut { get; set; }
    [BindProperty] public SaisiePaiement Paiement { get; set; } = new();

    public Enfant Enfant { get; private set; } = null!;
    public FicheAssiduite? Fiche { get; private set; }
    public Parent? ParentPayeur { get; private set; }
    public Dictionary<(DateOnly, DemiJournee), string> Codes { get; private set; } = new();
    public List<DateOnly> NonEnregistres { get; private set; } = new();
    /// <summary>Les presences ont change depuis la signature de la RSGE / Attendance changed since the RSGE signed.</summary>
    public bool CodesModifies { get; private set; }
    public bool ModeleDisponible => fiches.ModeleDisponible;
    public bool CourrielConfigure => courriels.EstConfigure;
    public bool UrlPubliqueConfiguree => !string.IsNullOrWhiteSpace(options.UrlPublique);
    public EtatFiche Etat => Fiche?.Etat ?? EtatFiche.Brouillon;
    /// <summary>Un lien de signature est en cours (le bouton devient « Renvoyer ») / An active link exists.</summary>
    public bool LienActif { get; private set; }
    public string? CourrielParent { get; private set; }

    private async Task<bool> ChargerAsync()
    {
        var e = await db.Enfants.AsNoTracking().FirstOrDefaultAsync(x => x.Id == EnfantId);
        if (e is null) return false;
        Enfant = e;
        var ancre = await fiches.AncreAsync();
        Debut = Debut == default
            ? FichesService.DebutPeriode(Temps.Aujourdhui(), ancre).AddDays(-14)
            : FichesService.DebutPeriode(Debut, ancre);
        Fiche = await db.Fiches.FirstOrDefaultAsync(f => f.EnfantId == EnfantId && f.Debut == Debut);
        ParentPayeur = await fiches.ParentPayeurAsync(EnfantId);
        var actuels = await fiches.CodesAsync(EnfantId, Debut);
        NonEnregistres = FichesService.JoursNonEnregistres(e, Debut, actuels);
        if (Fiche is null || Fiche.Etat == EtatFiche.Brouillon) Codes = actuels;
        else
        {
            Codes = FichesService.Lire(Fiche.CodesSignes);
            CodesModifies = Fiche.Etat == EtatFiche.SigneeRsge && FichesService.Serialiser(actuels) != Fiche.CodesSignes;
        }
        if (Fiche is not null)
        {
            LienActif = (await fiches.AvecLienActifAsync([Fiche.Id])).Count > 0;
            CourrielParent = Fiche.ParentId is { } pidc ? (await db.Parents.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pidc))?.Courriel : ParentPayeur?.Courriel;
            Paiement = new SaisiePaiement
            {
                DatePaiement1 = Fiche.DatePaiement1, MontantPaye1 = Texte(Fiche.MontantPaye1), Solde1 = Texte(Fiche.Solde1),
                DatePaiement2 = Fiche.DatePaiement2, MontantPaye2 = Texte(Fiche.MontantPaye2), Solde2 = Texte(Fiche.Solde2),
            };
        }
        return true;
    }

    private IActionResult Retour(string? message = null, string? erreur = null)
    {
        if (message is not null) TempData["Message"] = message;
        if (erreur is not null) TempData["Erreur"] = erreur;
        return RedirectToPage(new { enfantId = EnfantId, debut = Debut.ToString("yyyy-MM-dd") });
    }

    public async Task<IActionResult> OnGetAsync() => await ChargerAsync() ? Page() : NotFound();

    public async Task<IActionResult> OnPostPaiementAsync()
    {
        var saisie = Paiement;
        if (!await ChargerAsync()) return NotFound();
        if (Etat != EtatFiche.Brouillon) return Retour(erreur: "La fiche est déjà signée : reprenez-la pour changer le paiement.");
        if (!LireMontant(saisie.MontantPaye1, out var m1) || !LireMontant(saisie.Solde1, out var s1)
            || !LireMontant(saisie.MontantPaye2, out var m2) || !LireMontant(saisie.Solde2, out var s2))
            return Retour(erreur: "Montant invalide (ex. 46,75).");
        var f = await fiches.ObtenirOuCreerAsync(EnfantId, Debut);
        f.DatePaiement1 = saisie.DatePaiement1; f.MontantPaye1 = m1; f.Solde1 = s1;
        f.DatePaiement2 = saisie.DatePaiement2; f.MontantPaye2 = m2; f.Solde2 = s2;
        f.ModifieLe = DateTime.UtcNow;
        journal.Noter("fiche.paiement", "Enfant", EnfantId, $"{Debut:yyyy-MM-dd}");
        await db.SaveChangesAsync();
        return Retour("Paiement enregistré.");
    }

    /// <summary>Signature de la RSGE : fige les codes et le nom du parent / RSGE signs: freezes codes and parent name.</summary>
    public async Task<IActionResult> OnPostSignerRsgeAsync(string? signature)
    {
        if (!await ChargerAsync()) return NotFound();
        var png = FichesService.LireSignature(signature);
        if (png is null) return Retour(erreur: "Signez dans le cadre avant d'appuyer sur Signer.");
        var erreur = await fiches.SignerRsgeAsync(EnfantId, Debut, png, User.Identity?.Name);
        if (erreur is not null) return Retour(erreur: erreur);
        await journal.NoterMaintenantAsync("fiche.signer_rsge", "Enfant", EnfantId, $"{Debut:yyyy-MM-dd}");
        return Retour("Fiche signée. Envoyez-la maintenant au parent.");
    }

    /// <summary>Envoie le lien unique au parent par courriel / Emails the one-time link to the parent.</summary>
    public async Task<IActionResult> OnPostEnvoyerAsync()
    {
        if (!await ChargerAsync()) return NotFound();
        if (Etat != EtatFiche.SigneeRsge || Fiche is null) return Retour(erreur: "La fiche doit d'abord être signée par la RSGE.");
        if (!CourrielConfigure || !UrlPubliqueConfiguree) return Retour(erreur: "Le courriel ou l'adresse publique n'est pas configuré dans le module : copiez le lien.");
        var parent = Fiche.ParentId is { } pid ? await db.Parents.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pid) : null;
        if (parent?.Courriel is null) return Retour(erreur: "Le parent n'a pas de courriel : ajoutez-le dans sa fiche, ou copiez le lien.");
        Fiche.Enfant = Enfant;
        try
        {
            var a = await fiches.EnvoyerLienAsync([Fiche]);
            await journal.NoterMaintenantAsync("fiche.lien_envoye", "Enfant", EnfantId, $"{Debut:yyyy-MM-dd} → {a}");
            return Retour($"Lien envoyé à {a}.");
        }
        catch (Exception ex)
        {
            return Retour(erreur: $"L'envoi du courriel a échoué ({ex.GetType().Name}). Vérifiez les réglages SMTP du module, ou copiez le lien.");
        }
    }

    /// <summary>Cree un lien a copier (texto, autre courriel) ; il n'est affiche qu'une fois / Link to copy, shown once.</summary>
    public async Task<IActionResult> OnPostLienAsync()
    {
        if (!await ChargerAsync()) return NotFound();
        if (Etat != EtatFiche.SigneeRsge || Fiche is null) return Retour(erreur: "La fiche doit d'abord être signée par la RSGE.");
        if (!UrlPubliqueConfiguree) return Retour(erreur: "L'adresse publique n'est pas configurée dans le module : utilisez la signature sur place.");
        await fiches.AnnulerLiensAsync(Fiche.Id);
        var jeton = await fiches.CreerLienAsync([Fiche], null, surPlace: false);
        journal.Noter("fiche.lien_cree", "Enfant", EnfantId, $"{Debut:yyyy-MM-dd}");
        await db.SaveChangesAsync();
        TempData["Lien"] = fiches.UrlLien(jeton);
        return Retour();
    }

    /// <summary>Signature du parent sur place (tablette de la RSGE) / Parent signs on site.</summary>
    public async Task<IActionResult> OnPostSurPlaceAsync()
    {
        if (!await ChargerAsync()) return NotFound();
        if (Etat != EtatFiche.SigneeRsge || Fiche is null) return Retour(erreur: "La fiche doit d'abord être signée par la RSGE.");
        await fiches.AnnulerLiensAsync(Fiche.Id);
        var jeton = await fiches.CreerLienAsync([Fiche], null, surPlace: true);
        journal.Noter("fiche.sur_place", "Enfant", EnfantId, $"{Debut:yyyy-MM-dd}");
        await db.SaveChangesAsync();
        return RedirectToPage("/Public/Signer", new { j = jeton });
    }

    /// <summary>Reprend une fiche signee par la RSGE (erreur, presences modifiees) / Reopens an RSGE-signed sheet.</summary>
    public async Task<IActionResult> OnPostReprendreAsync()
    {
        if (!await ChargerAsync()) return NotFound();
        if (Etat != EtatFiche.SigneeRsge || Fiche is null) return Retour(erreur: "Seule une fiche non signée par le parent peut être reprise.");
        await fiches.AnnulerLiensAsync(Fiche.Id);
        documents.SupprimerFichier(Fiche.SignatureRsgeFichier);
        Fiche.SignatureRsgeFichier = null; Fiche.SigneRsgeLe = null; Fiche.SigneRsgePar = null;
        Fiche.CodesSignes = null; Fiche.NomParent = null; Fiche.ParentId = null;
        Fiche.Etat = EtatFiche.Brouillon;
        Fiche.ModifieLe = DateTime.UtcNow;
        journal.Noter("fiche.reprendre", "Enfant", EnfantId, $"{Debut:yyyy-MM-dd}");
        await db.SaveChangesAsync();
        return Retour("Fiche reprise : corrigez, puis signez de nouveau.");
    }

    /// <summary>
    /// Renvoie une copie du PDF signe au parent ou a la RSGE (courriel perdu, demande du parent).
    /// Re-sends a copy of the signed PDF to the parent or the RSGE.
    /// </summary>
    public async Task<IActionResult> OnPostCopieAsync(string pour)
    {
        if (!await ChargerAsync()) return NotFound();
        if (Fiche?.DocumentId is not { } did || await db.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == did) is not { } doc)
            return Retour(erreur: "La fiche n'est pas encore signée par le parent.");
        if (!CourrielConfigure) return Retour(erreur: "Le courriel n'est pas configuré dans le module.");
        var reglages = await db.Parametres.AsNoTracking().FirstOrDefaultAsync();
        var a = pour == "parent" ? CourrielParent : reglages?.CourrielRsge;
        if (a is null) return Retour(erreur: pour == "parent" ? "Le parent n'a pas de courriel." : "Le courriel de la RSGE n'est pas dans Réglages.");
        try
        {
            await courriels.EnvoyerAsync(a, $"Copie : fiche d'assiduité de {Enfant.Prenom} ({Affichage.Periode(Debut, Fiche.Fin)})",
                $"Voici une copie de la fiche d'assiduité signée de {Enfant.NomComplet} pour la période du {Affichage.Periode(Debut, Fiche.Fin)}." +
                (pour == "parent" && Affichage.PiedFacebook(reglages) is { Length: > 0 } fb ? "\n\n" + fb.TrimEnd() : ""),
                [(doc.NomOriginal, await documents.LireAsync(doc))]);
        }
        catch (Exception ex) { return Retour(erreur: $"L'envoi a échoué ({ex.GetType().Name})."); }
        await journal.NoterMaintenantAsync("fiche.copie_envoyee", "Enfant", EnfantId, $"{Debut:yyyy-MM-dd} → {a}");
        return Retour($"Copie envoyée à {a}.");
    }

    /// <summary>PDF : final (archive) si signe par le parent, sinon apercu / Final PDF if parent signed, else preview.</summary>
    public async Task<IActionResult> OnGetPdfAsync(bool afficher = false)
    {
        if (!await ChargerAsync()) return NotFound();
        if (!ModeleDisponible) return Retour(erreur: "Téléversez d'abord le formulaire FO-0310 dans Réglages.");
        if (Fiche?.DocumentId is { } did && await db.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == did) is { } doc)
        {
            await journal.NoterMaintenantAsync(afficher ? "fiche.afficher" : "fiche.telecharger", "Enfant", EnfantId, doc.NomOriginal);
            return this.Fichier(await documents.LireAsync(doc), "application/pdf", doc.NomOriginal, afficher);
        }
        var f = Fiche ?? new FicheAssiduite { EnfantId = EnfantId, Debut = Debut, Enfant = Enfant };
        f.Enfant ??= Enfant;
        var pdf = await fiches.GenererPdfAsync(f);
        await journal.NoterMaintenantAsync("fiche.apercu", "Enfant", EnfantId, $"{Debut:yyyy-MM-dd}");
        return this.Fichier(pdf, "application/pdf", "Aperçu " + FichesService.NomPdf(f, Enfant), afficher);
    }
}
