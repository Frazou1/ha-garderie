using Garderie.Web.Configuration;
using Garderie.Web.Data;
using Garderie.Web.Infrastructure;
using Garderie.Web.Securite;
using Garderie.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Garderie.Web.Pages.Public;

/// <summary>
/// Page du parent pour un contrat (lien unique) : signature (renseignements a completer, entente, regie interne)
/// ou formule de resiliation.
/// Parent page for a contract (one-time link): signature (info to complete, agreement, internal rules) or termination.
/// </summary>
public class ContratModel(ContratsService contrats, Journal journal) : PageModel
{
    [BindProperty(SupportsGet = true, Name = "j")] public string? Jeton { get; set; }
    [BindProperty] public SaisieParent Saisie { get; set; } = new();
    public LienContrat? Lien { get; private set; }
    public InfosContrat Infos { get; private set; } = null!;
    public EnfantParent? Parent { get; private set; }
    public bool NasEnregistre { get; private set; }
    public bool DejaConsenti { get; private set; }
    public bool Termine { get; private set; }
    public bool DejaUtilise { get; private set; }
    public bool EstResiliation => Lien?.But == ButLien.Resiliation;

    private async Task<bool> ChargerAsync()
    {
        Lien = await contrats.TrouverLienAsync(Jeton);
        if (Lien is null)
        {
            DejaUtilise = await contrats.DejaUtiliseAsync(Jeton);
            return false;
        }
        Infos = await contrats.InfosAsync(Lien.Contrat);
        Parent = await contrats.LienParentAsync(Lien.Contrat);
        NasEnregistre = Parent?.Parent.NasChiffre is not null;
        DejaConsenti = Parent?.Parent.ConsentRl24Le is not null;
        return true;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await ChargerAsync()) return Page();
        Saisie = new SaisieParent
        {
            EnfantAdresse = Infos.EnfantAdresse, EnfantTelephone = Infos.EnfantTelephone,
            ParentAdresse = Infos.ParentAdresse, ParentTelephone1 = Infos.ParentTelephone1, ParentTelephone2 = Infos.ParentTelephone2,
            Lien = Infos.ParentLien, VacancesParent = Lien!.Contrat.VacancesParent,
        };
        await journal.NoterMaintenantAsync(EstResiliation ? "contrat.resiliation_ouverte" : "contrat.parent_ouvre", "Enfant", Lien.Contrat.EnfantId);
        return Page();
    }

    /// <summary>Regie interne (PDF) accessible avec le lien / Internal rules PDF, available with the link.</summary>
    public async Task<IActionResult> OnGetRegieAsync(bool telecharger = false)
    {
        if (!await ChargerAsync()) return NotFound();
        var pdf = await contrats.RegiePdfAsync();
        await journal.NoterMaintenantAsync("contrat.regie_ouverte", "Enfant", Lien!.Contrat.EnfantId);
        return this.Fichier(pdf, "application/pdf", "Régie interne.pdf", !telecharger);
    }

    public async Task<IActionResult> OnPostSignerAsync(string? signature, string? nom, bool atteste, string? methode)
    {
        if (!await ChargerAsync()) return Page();
        var png = FichesService.LireSignature(signature);
        var signataire = (nom ?? "").Trim();
        if (png is null || !atteste || signataire.Length is < 2 or > 200)
        {
            ModelState.AddModelError("", "Cochez la case « J'ai lu et j'accepte » / « Je confirme », écrivez votre nom et signez dans le cadre.");
            return Page();
        }
        var ip = HttpContext.IpClient();
        if (EstResiliation)
        {
            await contrats.SignerResiliationAsync(Lien!, png, signataire, ip);
            await journal.NoterMaintenantAsync("contrat.resiliation_signee", "Enfant", Lien!.Contrat.EnfantId,
                $"{Lien.Contrat.ResilieLe:yyyy-MM-dd} : {signataire}{(Lien.SurPlace ? " (sur place)" : "")}{Methode(methode)}");
        }
        else
        {
            var erreur = await contrats.SignerParentAsync(Lien!, Saisie, png, signataire, ip);
            if (erreur is not null)
            {
                ModelState.AddModelError("", erreur);
                return Page();
            }
            await journal.NoterMaintenantAsync("contrat.signer_parent", "Enfant", Lien!.Contrat.EnfantId,
                $"{Lien.Contrat.Debut:yyyy-MM-dd} : {signataire}{(Lien.SurPlace ? " (sur place)" : "")}{Methode(methode)}");
        }
        Termine = true;
        return Page();
    }

    /// <summary>Mention au journal quand la signature vient d'une image importee / Journal note for an imported image.</summary>
    internal static string Methode(string? methode) => methode == "image" ? " · signature importée (image)" : "";
}
