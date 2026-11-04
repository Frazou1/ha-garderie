using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Garderie.Web.Pages;

/// <summary>Accueil = presences du jour / Home = today's attendance.</summary>
public class IndexModel : PageModel
{
    public IActionResult OnGet() => RedirectToPage("/Presences/Index");
}
