using Garderie.Web.Data;
using Garderie.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;

namespace Garderie.Web.Infrastructure;

/// <summary>
/// Dossier archive (date de fin passee) = lecture seule : tout POST est refuse sauf les gestionnaires permis
/// (Reactiver, SupprimerEnfant, AfficherNas...). Les donnees restent consultables pour l'impot (releve 24).
/// Archived record (end date passed) = read-only: every POST is refused except the allowed handlers.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class LectureSeuleSiAncienAttribute(string cleEnfant, params string[] permis) : Attribute, IAsyncPageFilter
{
    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;

    public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        var req = context.HttpContext.Request;
        var handler = context.HandlerMethod?.Name;
        if (!HttpMethods.IsPost(req.Method) || (handler is not null && permis.Contains(handler, StringComparer.OrdinalIgnoreCase)))
        {
            await next();
            return;
        }

        var brut = req.RouteValues[cleEnfant]?.ToString() ?? req.Query[cleEnfant].FirstOrDefault();
        if (int.TryParse(brut, out var id))
        {
            var db = context.HttpContext.RequestServices.GetRequiredService<GarderieDb>();
            var fin = await db.Enfants.Where(e => e.Id == id).Select(e => e.DateFin).FirstOrDefaultAsync();
            if (Enfant.EstAncienSelon(fin, Temps.Aujourdhui()))
            {
                var tempData = context.HttpContext.RequestServices.GetRequiredService<ITempDataDictionaryFactory>().GetTempData(context.HttpContext);
                tempData["Erreur"] = "Dossier archivé : lecture seule. Réactivez-le d'abord pour le modifier.";
                context.Result = new RedirectToPageResult("/Enfants/Dossier", new { id });
                return;
            }
        }
        await next();
    }
}
