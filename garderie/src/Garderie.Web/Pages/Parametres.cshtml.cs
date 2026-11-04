using Garderie.Web.Configuration;
using Garderie.Web.Data;
using Garderie.Web.Securite;
using Garderie.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

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

    public async Task OnGetAsync()
    {
        Donnees = await db.Parametres.FindAsync(1) ?? new Parametres();
        Donnees.DebutPeriodes ??= FichesService.AncreParDefaut;
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
