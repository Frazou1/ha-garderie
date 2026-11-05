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
public class ParametresModel(GarderieDb db, Journal journal, GarderieOptions options, Courriels courriels, ContratsService contrats) : PageModel
{
    [BindProperty] public Parametres Donnees { get; set; } = new();
    public bool ModeleDisponible => System.IO.File.Exists(options.FichierModeleFo0310);
    public DateTime? ModeleDate => ModeleDisponible ? System.IO.File.GetLastWriteTimeUtc(options.FichierModeleFo0310) : null;
    public bool CourrielConfigure => courriels.EstConfigure;
    public string UrlPublique => options.UrlPublique;
    public string CompteCourriel => options.SmtpUtilisateur;
    /// <summary>Texte de chaque section de la regie (cle -> texte) / Text of each rules section.</summary>
    [BindProperty] public Dictionary<string, string?> Regie { get; set; } = new();
    [BindProperty] public string? RegieTarif { get; set; }
    [BindProperty] public string? RegieJourPaiement { get; set; }
    [BindProperty] public string? RegieFraisRetard { get; set; }
    public DateTime? RegieModifieeLe { get; private set; }
    /// <summary>Sections modifiees (differentes du texte par defaut) / Edited sections.</summary>
    public HashSet<string> RegieModifiees { get; private set; } = new();

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
        var saisies = RegieInterne.Lire(Donnees.RegieSections);
        Regie = RegieInterne.Sections.ToDictionary(x => x.Cle, x => (string?)RegieInterne.Texte(x, saisies));
        RegieModifiees = saisies.Keys.ToHashSet();
        RegieTarif = Donnees.RegieTarif;
        RegieJourPaiement = Donnees.RegieJourPaiement;
        RegieFraisRetard = Donnees.RegieFraisRetard;
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

    /// <summary>Sections et valeurs de la regie interne (le PDF est genere) / Rules sections and values (PDF generated).</summary>
    public async Task<IActionResult> OnPostRegieAsync()
    {
        var p = await db.Parametres.FindAsync(1);
        if (p is null) { p = new Parametres { Id = 1 }; db.Parametres.Add(p); }
        if (Regie.Values.Any(t => (t?.Length ?? 0) > 20000))
        {
            TempData["Erreur"] = "Une section dépasse 20 000 caractères.";
            return RedirectToPage(null, null, "regie");
        }
        static string? Court(string? s, int max) => string.IsNullOrWhiteSpace(s) ? null : s.Trim() is var t && t.Length > max ? t[..max] : s.Trim();
        p.RegieSections = RegieInterne.Ecrire(Regie);
        p.RegieTarif = Court(RegieTarif, 50);
        p.RegieJourPaiement = Court(RegieJourPaiement, 30);
        p.RegieFraisRetard = Court(RegieFraisRetard, 200);
        p.RegieModifieeLe = DateTime.UtcNow;
        try { RegieInterne.Pdf(p); }
        catch (Exception ex) { TempData["Erreur"] = $"Le PDF n'a pas pu être généré ({ex.GetType().Name})."; return RedirectToPage(null, null, "regie"); }
        journal.Noter("parametres.regie_interne");
        await db.SaveChangesAsync();
        TempData["Message"] = "Régie interne enregistrée : le PDF remis aux parents est à jour.";
        return RedirectToPage(null, null, "regie");
    }

    /// <summary>Apercu du modele de contrat (champs de l'enfant et du parent a remplir) / Contract template preview.</summary>
    public async Task<IActionResult> OnGetContratModeleAsync(bool telecharger = false) =>
        this.Fichier(await contrats.ModelePdfAsync(), "application/pdf", "Modèle de contrat.pdf", !telecharger);

    /// <summary>PDF de la regie (tel que remis aux parents) / Rules PDF (as given to parents).</summary>
    public async Task<IActionResult> OnGetRegiePdfAsync(bool telecharger = false)
    {
        var p = await db.Parametres.AsNoTracking().FirstOrDefaultAsync() ?? new Parametres();
        return this.Fichier(RegieInterne.Pdf(p), "application/pdf", "Régie interne.pdf", !telecharger);
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
