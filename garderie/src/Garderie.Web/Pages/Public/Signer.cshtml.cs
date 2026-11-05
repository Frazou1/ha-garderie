using System.ComponentModel.DataAnnotations;
using Garderie.Web.Data;
using Garderie.Web.Infrastructure;
using Garderie.Web.Securite;
using Garderie.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Garderie.Web.Pages.Public;

/// <summary>
/// Page du parent (lien unique) : voit la ou les fiches signees par la RSGE (fratrie), signe une seule fois pour
/// toutes, ou signale une erreur. A la signature : PDF final de chaque enfant range dans son dossier et envoye a la RSGE.
/// Parent page (one-time link): sees the RSGE-signed sheet(s) (siblings), signs once for all, or reports an error.
/// </summary>
public class SignerModel(GarderieDb db, FichesService fiches, Courriels courriels, Journal journal, ILogger<SignerModel> log) : PageModel
{
    [BindProperty(SupportsGet = true, Name = "j")] public string? Jeton { get; set; }
    public LienSignature? Lien { get; private set; }
    /// <summary>Fiches a signer, par prenom / Sheets to sign, by first name.</summary>
    public List<FicheAssiduite> Fiches { get; private set; } = new();
    public Dictionary<int, Dictionary<(DateOnly, DemiJournee), string>> Codes { get; private set; } = new();
    public Parametres? Garderie { get; private set; }
    public bool Termine { get; private set; }
    public bool ErreurEnvoyee { get; private set; }
    public bool DejaSignee { get; private set; }
    public string Prenoms => FichesService.Liste(Fiches.Select(f => f.Enfant.Prenom).ToList());

    /// <summary>Le paiement est-il rempli ? (sinon la section n'est pas affichee) / Is the payment filled in?</summary>
    public static bool PaiementSaisi(FicheAssiduite f) =>
        f.DatePaiement1 is not null || f.MontantPaye1 is not null || f.Solde1 is not null ||
        f.DatePaiement2 is not null || f.MontantPaye2 is not null || f.Solde2 is not null;

    private async Task<bool> ChargerAsync()
    {
        Lien = await fiches.TrouverLienAsync(Jeton);
        if (Lien is null)
        {
            DejaSignee = await fiches.DejaSigneParCeLienAsync(Jeton);
            return false;
        }
        Fiches = FichesService.AttendentLeParent(Lien);
        Codes = Fiches.ToDictionary(f => f.Id, f => FichesService.Lire(f.CodesSignes));
        Garderie = await db.Parametres.AsNoTracking().FirstOrDefaultAsync();
        return true;
    }

    private void NoterPourChaque(string action, string details)
    {
        foreach (var f in Fiches) journal.Noter(action, "Enfant", f.EnfantId, $"{f.Debut:yyyy-MM-dd}{details}");
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await ChargerAsync()) return Page();
        NoterPourChaque("fiche.parent_ouvre", "");
        await db.SaveChangesAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostSignerAsync(string? signature, [MaxLength(200)] string? nom, bool atteste)
    {
        if (!await ChargerAsync()) return Page();
        var png = FichesService.LireSignature(signature);
        var signataire = (nom ?? "").Trim();
        if (png is null || !atteste || signataire.Length is < 2 or > 200)
        {
            ModelState.AddModelError("", "Cochez l'attestation, écrivez votre nom et signez dans le cadre.");
            return Page();
        }

        var surPlace = Lien!.SurPlace;
        var signees = await fiches.SignerParentAsync(Lien, png, signataire, HttpContext.IpClient());
        foreach (var (f, doc, _) in signees)
            journal.Noter("fiche.signer_parent", "Enfant", f.EnfantId,
                $"{f.Debut:yyyy-MM-dd} : {signataire}{(surPlace ? " (sur place)" : "")} · PDF {doc.Sha256[..12]}");
        await db.SaveChangesAsync();

        // Copie a la RSGE : un courriel, un PDF par enfant / Copy to the RSGE: one email, one PDF per child
        if (courriels.EstConfigure && Garderie?.CourrielRsge is { } courrielRsge && signees.Count > 0)
        {
            var noms = FichesService.Liste(signees.Select(x => x.Fiche.Enfant.NomComplet).ToList());
            var periode = Affichage.Periode(signees[0].Fiche.Debut, signees[0].Fiche.Fin);
            try
            {
                await courriels.EnvoyerAsync(courrielRsge,
                    $"{(signees.Count > 1 ? "Fiches signées" : "Fiche signée")} : {noms} ({periode})",
                    $"{(signees.Count > 1 ? "Les fiches" : "La fiche")} d'assiduité de {noms} pour la période du {periode} " +
                    $"{(signees.Count > 1 ? "ont été signées" : "a été signée")} par {signataire} le {Affichage.Horodatage(DateTime.UtcNow)}.\n\n" +
                    $"{(signees.Count > 1 ? "Les PDF signés sont joints ; ils sont aussi rangés" : "Le PDF signé est joint ; il est aussi rangé")} " +
                    "dans les documents du dossier.",
                    signees.Select(x => (x.Document.NomOriginal, x.Pdf)));
            }
            catch (Exception ex)
            {
                // Sans donnee personnelle dans le journal du module / No personal data in the add-on log
                log.LogWarning("Envoi du PDF signe a la RSGE impossible : {Type}", ex.GetType().Name);
            }
        }
        Termine = true;
        return Page();
    }

    public async Task<IActionResult> OnPostErreurAsync([MaxLength(1000)] string? message)
    {
        if (!await ChargerAsync()) return Page();
        var texte = (message ?? "").Trim();
        if (texte.Length is < 3 or > 1000)
        {
            ModelState.AddModelError("", "Décrivez l'erreur en quelques mots.");
            return Page();
        }
        foreach (var f in Fiches)
        {
            f.ErreurSignalee = texte;
            f.ErreurSignaleeLe = DateTime.UtcNow;
            f.ModifieLe = DateTime.UtcNow;
        }
        NoterPourChaque("fiche.erreur_signalee", "");
        await db.SaveChangesAsync();

        if (courriels.EstConfigure && Garderie?.CourrielRsge is { } courrielRsge)
        {
            var noms = FichesService.Liste(Fiches.Select(f => f.Enfant.NomComplet).ToList());
            try
            {
                await courriels.EnvoyerAsync(courrielRsge, $"Erreur signalée : {Prenoms} ({Affichage.Periode(Fiches[0].Debut, Fiches[0].Fin)})",
                    $"Le parent signale une erreur sur la fiche de {noms} :\n\n« {texte} »\n\n" +
                    "Ouvrez la fiche dans Garderie, corrigez les présences, reprenez la fiche et renvoyez-la.");
            }
            catch (Exception ex)
            {
                log.LogWarning("Envoi de l'erreur signalee impossible : {Type}", ex.GetType().Name);
            }
        }
        ErreurEnvoyee = true;
        return Page();
    }
}
