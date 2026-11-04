using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Garderie.Web.Pages.Public;

/// <summary>
/// Accueil public : aucune information. Les pages des parents (lien unique) viendront a l'etape 2.
/// Public home: no information. Parent pages (one-time link) come in step 2.
/// </summary>
public class IndexModel : PageModel
{
    public void OnGet() { }
}
