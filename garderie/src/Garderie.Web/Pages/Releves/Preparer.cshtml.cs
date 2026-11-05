using Garderie.Web.Data;
using Garderie.Web.Infrastructure;
using Garderie.Web.Securite;
using Garderie.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Garderie.Web.Pages.Releves;

/// <summary>
/// Preparer le releve 24 d'un parent payeur : montants proposes a corriger, adresse decoupee, apercu, production.
/// Prepare a paying parent's RL-24: proposed amounts to correct, split address, preview, issue.
/// </summary>
public class PreparerModel(GarderieDb db, RelevesService releves, Journal journal) : PageModel
{
    [BindProperty(SupportsGet = true)] public int Annee { get; set; }
    [BindProperty(SupportsGet = true)] public int ParentId { get; set; }
    [BindProperty] public RelevesService.Saisie Saisie { get; set; } = new();
    public Parent Parent { get; private set; } = null!;
    public Dictionary<int, RelevesService.EnfantPropose> Proposes { get; private set; } = new();
    public Releve? Dernier { get; private set; }
    public bool NasAuDossier { get; private set; }
    public int? EnfantDuParent { get; private set; }
    public List<string> Manque { get; private set; } = new();
    public int EnTrop { get; private set; }

    private async Task<bool> ChargerAsync()
    {
        if (await db.Parents.AsNoTracking().FirstOrDefaultAsync(p => p.Id == ParentId) is not { } parent) return false;
        Parent = parent;
        var prop = (await releves.PropositionsAsync(Annee)).FirstOrDefault(p => p.ParentId == ParentId);
        var enfants = prop?.Enfants ?? [];
        EnTrop = Math.Max(0, enfants.Count - 3);
        Proposes = enfants.Take(3).ToDictionary(e => e.EnfantId);
        Dernier = await releves.DernierAsync(Annee, ParentId);
        NasAuDossier = releves.NasDuParent(parent) is not null;
        EnfantDuParent = await db.EnfantParents.Where(x => x.ParentId == ParentId).Select(x => (int?)x.EnfantId).FirstOrDefaultAsync();
        Manque = await releves.PrerequisAsync();
        return true;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await ChargerAsync()) return NotFound();
        if (Dernier is not null)
        {
            // Releve modifie : on repart de ce qui a ete produit / Amended slip: start from the issued one
            var c = RelevesService.LireContenu(Dernier);
            Saisie = new RelevesService.Saisie
            {
                Nom = c.PayeurNom, Prenom = c.PayeurPrenom, Appartement = c.PayeurAdresse.Appartement, Numero = c.PayeurAdresse.Numero,
                Rue = c.PayeurAdresse.Rue, Ville = c.PayeurAdresse.Ville, Province = c.PayeurAdresse.Province, CodePostal = c.PayeurAdresse.CodePostalAffiche,
            };
            var parNom = Proposes.Values.ToDictionary(e => (e.Nom, e.Prenom));
            Saisie.Enfants = c.Enfants.Where(e => parNom.ContainsKey((e.Nom, e.Prenom))).Select(e => new RelevesService.SaisieEnfant
            {
                EnfantId = parNom[(e.Nom, e.Prenom)].EnfantId, Jours = e.Jours,
                Total = e.Total.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                NonAdmissible = e.NonAdmissible.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
            }).ToList();
        }
        if (Saisie.Enfants.Count == 0)
        {
            var a = RelevesService.Decouper(Parent.Adresse);
            Saisie = new RelevesService.Saisie
            {
                Nom = Parent.Nom, Prenom = Parent.Prenom, Appartement = a.Appartement, Numero = a.Numero, Rue = a.Rue, Ville = a.Ville,
                Province = a.Province ?? "QC", CodePostal = a.CodePostalAffiche,
                Enfants = Proposes.Values.Select(e => new RelevesService.SaisieEnfant
                {
                    EnfantId = e.EnfantId, Jours = e.Jours, Total = e.Montant.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture), NonAdmissible = "0.00",
                }).ToList(),
            };
        }
        return Page();
    }

    private bool SaisieValide() =>
        Saisie.Enfants.Count > 0 && Saisie.Enfants.All(e => Proposes.ContainsKey(e.EnfantId)) && Saisie.Enfants.Select(e => e.EnfantId).Distinct().Count() == Saisie.Enfants.Count;

    public async Task<IActionResult> OnPostApercuAsync()
    {
        if (!await ChargerAsync()) return NotFound();
        if (Manque.Count > 0 || !SaisieValide()) return BadRequest();
        var (pdf, erreur) = await releves.ApercuAsync(Annee, Parent, Saisie);
        if (pdf is null) return Content(erreur ?? "Erreur.", "text/plain; charset=utf-8");
        return this.Fichier(pdf, "application/pdf", "Aperçu relevé 24.pdf", afficher: true);
    }

    public async Task<IActionResult> OnPostGenererAsync()
    {
        if (!await ChargerAsync()) return NotFound();
        if (Manque.Count > 0) { ModelState.AddModelError("", "Complétez d'abord Réglages → Relevé 24."); return Page(); }
        if (!SaisieValide()) { ModelState.AddModelError("", "Les enfants du relevé ne correspondent pas à ce parent."); return Page(); }
        var (releve, erreur) = await releves.GenererAsync(Annee, Parent, Saisie, User.Identity?.Name);
        if (releve is null) { ModelState.AddModelError("", erreur ?? "Erreur."); return Page(); }
        await journal.NoterMaintenantAsync("releve24.produire", "Enfant", releve.EnfantId, $"{Annee} no {releve.Numero} ({releve.Code})");
        TempData["Message"] = $"Relevé 24 no {releve.Numero} produit{(releve.Code == "A" ? " (relevé modifié, il remplace le précédent)" : "")}.";
        return RedirectToPage("Index", new { annee = Annee });
    }
}
