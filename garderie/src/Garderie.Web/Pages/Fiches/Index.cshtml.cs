using Garderie.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Garderie.Web.Pages.Fiches;

/// <summary>Fiches d'assiduite d'une periode de 2 semaines / Attendance sheets of a 2-week period.</summary>
public class IndexModel(FichesService fiches) : PageModel
{
    [BindProperty(SupportsGet = true)] public DateOnly? Periode { get; set; }
    public DateOnly Debut { get; private set; }
    public DateOnly Fin => Debut.AddDays(13);
    public DateOnly Courante { get; private set; }
    public List<SituationFiche> Situations { get; private set; } = new();
    public bool ModeleDisponible => fiches.ModeleDisponible;

    public async Task OnGetAsync()
    {
        var ancre = await fiches.AncreAsync();
        Courante = FichesService.DebutPeriode(Temps.Aujourdhui(), ancre);
        // Par defaut : la periode precedente (celle qu'on fait signer) / Default: previous period (the one to sign)
        Debut = FichesService.DebutPeriode(Periode ?? Courante.AddDays(-14), ancre);
        Situations = await fiches.PeriodeAsync(Debut);
    }
}
