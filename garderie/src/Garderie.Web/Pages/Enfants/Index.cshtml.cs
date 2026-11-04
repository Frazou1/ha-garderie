using Garderie.Web.Data;
using Garderie.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Garderie.Web.Pages.Enfants;

public class IndexModel(GarderieDb db) : PageModel
{
    [BindProperty(SupportsGet = true)] public bool Tous { get; set; }
    public List<Enfant> Enfants { get; private set; } = new();
    public DateOnly Aujourdhui { get; } = Temps.Aujourdhui();

    public async Task OnGetAsync()
    {
        var q = db.Enfants.AsNoTracking().Include(e => e.Allergies).AsQueryable();
        if (!Tous) q = q.Where(e => e.DateFin == null || e.DateFin >= Aujourdhui);
        Enfants = await q.OrderBy(e => e.Prenom).ThenBy(e => e.Nom).ToListAsync();
    }
}
