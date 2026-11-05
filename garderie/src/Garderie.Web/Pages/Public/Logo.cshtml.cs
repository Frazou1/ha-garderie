using Garderie.Web.Configuration;
using Garderie.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Garderie.Web.Pages.Public;

/// <summary>
/// Logo de la garderie (Reglages), servi aussi sur le port public pour les pages des parents.
/// Daycare logo, also served on the public port for parent pages.
/// </summary>
public class LogoModel(GarderieOptions options) : PageModel
{
    public IActionResult OnGet()
    {
        if (!System.IO.File.Exists(options.FichierLogo)) return NotFound();
        var octets = System.IO.File.ReadAllBytes(options.FichierLogo);
        var type = DocumentsService.DetecterType(octets);
        if (type is not ("image/png" or "image/jpeg")) return NotFound();
        Response.Headers.CacheControl = "public, max-age=86400";   // l'adresse change avec le logo / URL changes with the logo
        return File(octets, type);
    }

    /// <summary>Version du logo pour l'adresse (cache) ; 0 si absent / Logo version for the URL; 0 when absent.</summary>
    public static long Version(GarderieOptions options) =>
        System.IO.File.Exists(options.FichierLogo) ? System.IO.File.GetLastWriteTimeUtc(options.FichierLogo).Ticks : 0;
}
