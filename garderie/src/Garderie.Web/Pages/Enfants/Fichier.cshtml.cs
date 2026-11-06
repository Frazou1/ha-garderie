using Garderie.Web.Infrastructure;
using Garderie.Web.Data;
using Garderie.Web.Securite;
using Garderie.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Garderie.Web.Pages.Enfants;

/// <summary>
/// Document du dossier affiche dans le navigateur. L'adresse se termine par « titre - enfant.pdf » : l'onglet et
/// l'enregistrement montrent ce nom au lieu d'un numero.
/// Record document shown inline; the URL ends with "title - child.pdf" so the tab and the saved file use that name.
/// </summary>
public class FichierModel(GarderieDb db, Journal journal, DocumentsService documents) : PageModel
{
    public async Task<IActionResult> OnGetAsync(int id, int documentId)
    {
        var d = await db.Documents.AsNoTracking().FirstOrDefaultAsync(x => x.Id == documentId && x.EnfantId == id);
        if (d is null) return NotFound();
        var e = await db.Enfants.AsNoTracking().FirstAsync(x => x.Id == id);
        var contenu = await documents.LireAsync(d);
        var lisible = d.TypeContenu is "application/pdf" or "image/jpeg" or "image/png";
        await journal.NoterMaintenantAsync(lisible ? "document.afficher" : "document.telecharger", "Enfant", id, d.NomOriginal);
        return this.Fichier(contenu, d.TypeContenu, Affichage.NomFichier(d.NomOriginal, e.Prenom, e.NomComplet), lisible);
    }
}
