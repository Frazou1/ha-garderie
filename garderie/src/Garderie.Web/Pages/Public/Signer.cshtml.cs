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
/// Page du parent (lien unique) : voit la fiche signee par la RSGE, signe, ou signale une erreur.
/// A la signature : PDF final range dans le dossier et envoye a la RSGE par courriel.
/// Parent page (one-time link): sees the RSGE-signed sheet, signs or reports an error.
/// </summary>
public class SignerModel(GarderieDb db, FichesService fiches, DocumentsService documents, Courriels courriels,
    Journal journal, ILogger<SignerModel> log) : PageModel
{
    [BindProperty(SupportsGet = true, Name = "j")] public string? Jeton { get; set; }
    public LienSignature? Lien { get; private set; }
    public FicheAssiduite Fiche => Lien!.Fiche;
    public Dictionary<(DateOnly, DemiJournee), string> Codes { get; private set; } = new();
    public Parametres? Garderie { get; private set; }
    public bool Termine { get; private set; }
    public bool ErreurEnvoyee { get; private set; }

    private async Task<bool> ChargerAsync()
    {
        Lien = await fiches.TrouverLienAsync(Jeton);
        if (Lien is null) return false;
        Codes = FichesService.Lire(Lien.Fiche.CodesSignes);
        Garderie = await db.Parametres.AsNoTracking().FirstOrDefaultAsync();
        return true;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await ChargerAsync()) return Page();
        await journal.NoterMaintenantAsync("fiche.parent_ouvre", "Enfant", Fiche.EnfantId, $"{Fiche.Debut:yyyy-MM-dd}");
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

        var f = Fiche;
        var ip = HttpContext.IpClient();
        f.SignatureParentFichier = await documents.EnregistrerOctetsAsync(png);
        f.SigneParentLe = DateTime.UtcNow;
        f.SignataireParent = signataire;
        f.IpParent = ip.Length > 64 ? ip[..64] : ip;
        f.SigneSurPlace = Lien!.SurPlace;
        f.Etat = EtatFiche.SigneeParent;
        f.ModifieLe = DateTime.UtcNow;
        Lien.UtiliseLe = DateTime.UtcNow;

        // PDF final range dans le dossier (chiffre) / Final PDF stored in the record (encrypted)
        var pdf = await fiches.GenererPdfAsync(f);
        var nomPdf = FichesService.NomPdf(f, f.Enfant);
        var doc = await documents.EnregistrerPdfAsync(f.EnfantId, TypeDocument.FicheAssiduite, nomPdf, pdf, $"Période du {Affichage.Periode(f.Debut, f.Fin)}");
        db.Documents.Add(doc);
        await db.SaveChangesAsync();
        f.DocumentId = doc.Id;
        f.PdfSha256 = doc.Sha256;
        journal.Noter("fiche.signer_parent", "Enfant", f.EnfantId,
            $"{f.Debut:yyyy-MM-dd} : {signataire}{(Lien.SurPlace ? " (sur place)" : "")} · PDF {doc.Sha256[..12]}");
        await db.SaveChangesAsync();
        await fiches.AnnulerLiensAsync(f.Id);

        // Copie a la RSGE / Copy to the RSGE
        if (courriels.EstConfigure && Garderie?.CourrielRsge is { } courrielRsge)
        {
            try
            {
                await courriels.EnvoyerAsync(courrielRsge, $"Fiche signée : {f.Enfant.Prenom} {f.Enfant.Nom} ({Affichage.Periode(f.Debut, f.Fin)})",
                    $"La fiche d'assiduité de {f.Enfant.NomComplet} pour la période du {Affichage.Periode(f.Debut, f.Fin)} " +
                    $"a été signée par {signataire} le {Affichage.Horodatage(f.SigneParentLe.Value)}.\n\n" +
                    "Le PDF signé est joint ; il est aussi rangé dans les documents du dossier.", nomPdf, pdf);
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
        var f = Fiche;
        f.ErreurSignalee = texte;
        f.ErreurSignaleeLe = DateTime.UtcNow;
        f.ModifieLe = DateTime.UtcNow;
        journal.Noter("fiche.erreur_signalee", "Enfant", f.EnfantId, $"{f.Debut:yyyy-MM-dd}");
        await db.SaveChangesAsync();

        if (courriels.EstConfigure && Garderie?.CourrielRsge is { } courrielRsge)
        {
            try
            {
                await courriels.EnvoyerAsync(courrielRsge, $"Erreur signalée : fiche de {f.Enfant.Prenom} ({Affichage.Periode(f.Debut, f.Fin)})",
                    $"Le parent signale une erreur sur la fiche de {f.Enfant.NomComplet} :\n\n« {texte} »\n\n" +
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
