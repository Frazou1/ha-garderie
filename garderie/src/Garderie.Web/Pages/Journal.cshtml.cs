using Garderie.Web.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Garderie.Web.Pages;

/// <summary>Consultation du journal des acces (500 dernieres entrees) / Access log viewer.</summary>
public class JournalModel(GarderieDb db) : PageModel
{
    public List<JournalAcces> Entrees { get; private set; } = new();
    public Dictionary<int, string> NomsEnfants { get; private set; } = new();

    public async Task OnGetAsync()
    {
        Entrees = await db.Journal.AsNoTracking().OrderByDescending(j => j.Id).Take(500).ToListAsync();
        var ids = Entrees.Where(e => e.Entite == "Enfant" && e.EntiteId != null).Select(e => e.EntiteId!.Value).Distinct().ToList();
        NomsEnfants = await db.Enfants.AsNoTracking().Where(e => ids.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => e.Prenom + " " + e.Nom);
    }
}
