using Garderie.Web.Configuration;
using Garderie.Web.Data;
using Garderie.Web.Infrastructure;
using Garderie.Web.Securite;
using Garderie.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Garderie.Web.Pages;

/// <summary>
/// Parametres de la garderie, saisis dans l'appli (jamais dans le code public) ; formulaire FO-0310 vierge ;
/// etat du courriel (configure dans les options du module).
/// Daycare settings, blank FO-0310 form, email status (configured in the add-on options).
/// </summary>
public class ParametresModel(GarderieDb db, Journal journal, GarderieOptions options, Courriels courriels) : PageModel
{
    [BindProperty] public Parametres Donnees { get; set; } = new();
    public bool ModeleDisponible => System.IO.File.Exists(options.FichierModeleFo0310);
    public DateTime? ModeleDate => ModeleDisponible ? System.IO.File.GetLastWriteTimeUtc(options.FichierModeleFo0310) : null;
    public bool CourrielConfigure => courriels.EstConfigure;
    public string UrlPublique => options.UrlPublique;
    public string CompteCourriel => options.SmtpUtilisateur;
    [BindProperty] public string? RegieTexte { get; set; }
    public DateTime? RegieModifieeLe { get; private set; }

    /// <summary>Textes du contrat a afficher (Reglages, sinon textes par defaut) / Contract texts to show.</summary>
    public sealed class SaisieContrat
    {
        public string? VilleSignature { get; set; }
        public string? NoteVacances { get; set; }
        public string ObligationsRsge { get; set; } = "";
        public string ObligationsParent { get; set; } = "";
        public string MentionLpc { get; set; } = "";
        public string Attestation { get; set; } = "";
        public string NonRespect { get; set; } = "";
    }

    [BindProperty] public SaisieContrat Contrat { get; set; } = new();

    public async Task OnGetAsync()
    {
        Donnees = await db.Parametres.FindAsync(1) ?? new Parametres();
        Donnees.DebutPeriodes ??= FichesService.AncreParDefaut;
        RegieTexte = Donnees.RegieTexte;
        RegieModifieeLe = Donnees.RegieModifieeLe;
        Contrat = new SaisieContrat
        {
            VilleSignature = Donnees.VilleSignature, NoteVacances = Donnees.ContratNoteVacances,
            ObligationsRsge = Donnees.ContratObligationsRsge ?? TextesContrat.ObligationsRsge,
            ObligationsParent = Donnees.ContratObligationsParent ?? TextesContrat.ObligationsParent,
            MentionLpc = Donnees.ContratMentionLpc ?? TextesContrat.MentionLpc,
            Attestation = Donnees.ContratAttestation ?? TextesContrat.Attestation,
            NonRespect = Donnees.ContratNonRespect ?? TextesContrat.NonRespect,
        };
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        var p = await db.Parametres.FindAsync(1);
        if (p is null) { p = new Parametres { Id = 1 }; db.Parametres.Add(p); }
        p.NomGarderie = Vide(Donnees.NomGarderie);
        p.NomRsge = Vide(Donnees.NomRsge);
        p.Adresse = Vide(Donnees.Adresse);
        p.Telephone = Vide(Donnees.Telephone);
        p.CourrielRsge = Vide(Donnees.CourrielRsge);
        p.NomBureauCoordonnateur = Vide(Donnees.NomBureauCoordonnateur);
        p.HeureOuverture = Donnees.HeureOuverture;
        p.HeureFermeture = Donnees.HeureFermeture;
        p.DebutPeriodes = Donnees.DebutPeriodes is { } d ? PresencesService.Lundi(d) : null;
        journal.Noter("parametres.modifier");
        await db.SaveChangesAsync();
        TempData["Message"] = "Paramètres enregistrés.";
        return RedirectToPage();
    }

    /// <summary>Televerse le formulaire FO-0310 vierge (verifie ses champs) / Uploads the blank FO-0310 (fields checked).</summary>
    public async Task<IActionResult> OnPostModeleAsync(IFormFile? modele)
    {
        if (modele is null || modele.Length == 0 || modele.Length > 5 * 1024 * 1024)
        {
            TempData["Erreur"] = "Choisissez le PDF FO-0310 (5 Mo maximum).";
            return RedirectToPage();
        }
        using var ms = new MemoryStream();
        await modele.CopyToAsync(ms);
        var octets = ms.ToArray();
        if (DocumentsService.DetecterType(octets) != "application/pdf")
        {
            TempData["Erreur"] = "Ce fichier n'est pas un PDF.";
            return RedirectToPage();
        }
        var manquants = Fo0310Pdf.Valider(octets);
        if (manquants.Count > 0)
        {
            TempData["Erreur"] = "Ce PDF n'est pas le formulaire FO-0310 à remplir (champs absents : " + string.Join(", ", manquants.Take(4)) + ").";
            return RedirectToPage();
        }
        Directory.CreateDirectory(Path.GetDirectoryName(options.FichierModeleFo0310)!);
        await System.IO.File.WriteAllBytesAsync(options.FichierModeleFo0310, octets);
        await journal.NoterMaintenantAsync("parametres.modele_fo0310", details: $"{octets.Length} octets");
        TempData["Message"] = "Formulaire FO-0310 enregistré.";
        return RedirectToPage();
    }

    /// <summary>Textes du contrat (vide = texte par defaut) / Contract texts (empty = default text).</summary>
    public async Task<IActionResult> OnPostContratAsync()
    {
        var p = await db.Parametres.FindAsync(1);
        if (p is null) { p = new Parametres { Id = 1 }; db.Parametres.Add(p); }
        static string? T(string? s, string defaut, int max)
        {
            var t = (s ?? "").Replace("\r\n", "\n").Trim();
            if (t.Length == 0 || t == defaut.Replace("\r\n", "\n").Trim()) return null;   // null = texte par defaut
            return t.Length > max ? t[..max] : t;
        }
        p.VilleSignature = Vide(Contrat.VilleSignature);
        p.ContratNoteVacances = Vide(Contrat.NoteVacances);
        p.ContratObligationsRsge = T(Contrat.ObligationsRsge, TextesContrat.ObligationsRsge, 4000);
        p.ContratObligationsParent = T(Contrat.ObligationsParent, TextesContrat.ObligationsParent, 4000);
        p.ContratMentionLpc = T(Contrat.MentionLpc, TextesContrat.MentionLpc, 6000);
        p.ContratAttestation = T(Contrat.Attestation, TextesContrat.Attestation, 2000);
        p.ContratNonRespect = T(Contrat.NonRespect, TextesContrat.NonRespect, 2000);
        journal.Noter("parametres.contrat");
        await db.SaveChangesAsync();
        TempData["Message"] = "Textes du contrat enregistrés (ils s'appliquent aux contrats pas encore signés).";
        return RedirectToPage();
    }

    public const int RegieMax = 60000;

    /// <summary>Texte de la regie interne (le PDF est genere a partir de lui) / Internal rules text (PDF generated from it).</summary>
    public async Task<IActionResult> OnPostRegieAsync()
    {
        var p = await db.Parametres.FindAsync(1);
        if (p is null) { p = new Parametres { Id = 1 }; db.Parametres.Add(p); }
        var t = (RegieTexte ?? "").Replace("\r\n", "\n").Trim('\n', ' ');
        if (t.Length > RegieMax) { TempData["Erreur"] = $"La régie interne dépasse {RegieMax} caractères."; return RedirectToPage(); }
        try { if (t.Length > 0) RegiePdf.Generer(t, p.NomRsge, p.NomGarderie, DateTime.UtcNow); }
        catch (Exception ex) { TempData["Erreur"] = $"Le PDF n'a pas pu être généré ({ex.GetType().Name})."; return RedirectToPage(); }
        p.RegieTexte = t.Length == 0 ? null : t;
        p.RegieModifieeLe = DateTime.UtcNow;
        journal.Noter("parametres.regie_interne", details: $"{t.Length} caractères");
        await db.SaveChangesAsync();
        TempData["Message"] = t.Length == 0 ? "Régie interne retirée : les contrats ne peuvent plus être envoyés." : "Régie interne enregistrée : le PDF remis aux parents est à jour.";
        return RedirectToPage(null, null, "regie");
    }

    /// <summary>Importe la regie depuis le Word (.docx) : remplace le texte / Imports the rules from Word, replacing the text.</summary>
    public async Task<IActionResult> OnPostImporterRegieAsync(IFormFile? word)
    {
        if (word is null || word.Length == 0 || word.Length > 15 * 1024 * 1024)
        {
            TempData["Erreur"] = "Choisissez la régie interne en Word (.docx, 15 Mo maximum).";
            return RedirectToPage();
        }
        using var ms = new MemoryStream();
        await word.CopyToAsync(ms);
        string texte;
        try { texte = RegiePdf.ImporterDocx(ms.ToArray()); }
        catch (InvalidDataException)
        {
            TempData["Erreur"] = "Ce fichier n'est pas un document Word .docx (un ancien .doc doit d'abord être enregistré en .docx).";
            return RedirectToPage();
        }
        if (texte.Trim().Length == 0 || texte.Length > RegieMax)
        {
            TempData["Erreur"] = texte.Trim().Length == 0 ? "Ce document Word est vide." : $"Ce document dépasse {RegieMax} caractères.";
            return RedirectToPage();
        }
        var p = await db.Parametres.FindAsync(1);
        if (p is null) { p = new Parametres { Id = 1 }; db.Parametres.Add(p); }
        p.RegieTexte = texte.Trim('\n');
        p.RegieModifieeLe = DateTime.UtcNow;
        journal.Noter("parametres.regie_importee", details: $"{texte.Length} caractères");
        await db.SaveChangesAsync();
        TempData["Message"] = "Régie interne importée du Word. Vérifiez le texte et l'aperçu PDF ; vous pouvez le modifier ici.";
        return RedirectToPage(null, null, "regie");
    }

    /// <summary>Apercu du PDF de la regie (tel que remis aux parents) / Rules PDF preview (as given to parents).</summary>
    public async Task<IActionResult> OnGetRegiePdfAsync(bool telecharger = false)
    {
        var p = await db.Parametres.AsNoTracking().FirstOrDefaultAsync();
        if (string.IsNullOrWhiteSpace(p?.RegieTexte)) return NotFound();
        return this.Fichier(RegiePdf.Generer(p.RegieTexte, p.NomRsge, p.NomGarderie, p.RegieModifieeLe), "application/pdf", "Régie interne.pdf", !telecharger);
    }

    /// <summary>
    /// Police du contrat (TrueType / OpenType) copiee depuis l'ordinateur de la RSGE ; gardee dans /data seulement.
    /// Contract font copied from the RSGE's computer; kept in /data only.
    /// </summary>
    public async Task<IActionResult> OnPostPoliceAsync(string? cle, IFormFile? police)
    {
        if (cle is null || PolicesPdf.Emplacements.All(e => e.Cle != cle)) return BadRequest();
        if (police is null || police.Length == 0 || police.Length > 30 * 1024 * 1024)
        {
            TempData["Erreur"] = "Choisissez un fichier de police (.ttf ou .otf, 30 Mo maximum).";
            return RedirectToPage();
        }
        using var ms = new MemoryStream();
        await police.CopyToAsync(ms);
        var o = ms.ToArray();
        var signature = o.Length >= 4 ? Convert.ToHexString(o, 0, 4) : "";
        if (signature is not ("00010000" or "4F54544F" or "74727565"))   // TrueType, « OTTO », « true »
        {
            TempData["Erreur"] = "Ce fichier n'est pas une police TrueType ou OpenType (.ttf / .otf).";
            return RedirectToPage();
        }
        Directory.CreateDirectory(options.DossierPolices);
        await System.IO.File.WriteAllBytesAsync(Path.Combine(options.DossierPolices, cle + ".ttf"), o);
        await journal.NoterMaintenantAsync("parametres.police", details: $"{cle} ({o.Length} octets)");
        TempData["Message"] = "Police enregistrée : elle sera utilisée pour les prochains contrats.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRetirerPoliceAsync(string? cle)
    {
        if (cle is null || PolicesPdf.Emplacements.All(e => e.Cle != cle)) return BadRequest();
        var f = Path.Combine(options.DossierPolices, cle + ".ttf");
        if (System.IO.File.Exists(f)) System.IO.File.Delete(f);
        await journal.NoterMaintenantAsync("parametres.police_retiree", details: cle);
        TempData["Message"] = "Police retirée (police de remplacement utilisée).";
        return RedirectToPage();
    }

    /// <summary>Courriel de test a la RSGE / Test email to the RSGE.</summary>
    public async Task<IActionResult> OnPostTestCourrielAsync()
    {
        var p = await db.Parametres.FindAsync(1);
        if (!courriels.EstConfigure || p?.CourrielRsge is null)
        {
            TempData["Erreur"] = "Configurez le compte courriel dans les options du module et le courriel de la RSGE ici.";
            return RedirectToPage();
        }
        try
        {
            await courriels.EnvoyerAsync(p.CourrielRsge, "Garderie : courriel de test", "Ce courriel confirme que l'envoi fonctionne.");
            TempData["Message"] = $"Courriel de test envoyé à {p.CourrielRsge}.";
        }
        catch (Exception ex)
        {
            TempData["Erreur"] = $"Échec de l'envoi ({ex.GetType().Name}) : vérifiez le compte et le mot de passe d'application.";
        }
        return RedirectToPage();
    }

    private static string? Vide(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
