using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Net.Http.Headers;

namespace Garderie.Web.Infrastructure;

/// <summary>
/// Fichier affiche dans le navigateur (inline) ou telecharge (piece jointe) / File shown inline or downloaded.
/// </summary>
public static class ReponsesFichier
{
    public static IActionResult Fichier(this PageModel page, byte[] contenu, string type, string nom, bool afficher)
    {
        if (!afficher) return page.File(contenu, type, nom);
        page.Response.Headers[HeaderNames.ContentDisposition] = new ContentDispositionHeaderValue("inline") { FileNameStar = nom }.ToString();
        return page.File(contenu, type);
    }
}
