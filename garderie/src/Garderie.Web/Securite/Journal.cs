using Garderie.Web.Data;
using Garderie.Web.Infrastructure;

namespace Garderie.Web.Securite;

/// <summary>
/// Journal des acces (consultation de dossier, NAS, documents) et des modifications.
/// Access log (record views, SIN, documents) and change log.
/// </summary>
public sealed class Journal(GarderieDb db, IHttpContextAccessor http)
{
    /// <summary>Ajoute une entree ; enregistree au prochain SaveChanges / Added on next SaveChanges.</summary>
    public void Noter(string action, string? entite = null, int? entiteId = null, string? details = null)
    {
        var ctx = http.HttpContext;
        db.Journal.Add(new JournalAcces
        {
            Horodatage = DateTime.UtcNow,
            Utilisateur = Tronquer(ctx?.User.Identity?.Name ?? "(anonyme)", 100),
            Ip = Tronquer(ctx?.IpClient() ?? "?", 64),
            Action = Tronquer(action, 50),
            Entite = entite,
            EntiteId = entiteId,
            Details = details is null ? null : Tronquer(details, 500),
        });
    }

    /// <summary>Note et enregistre tout de suite / Log and save immediately.</summary>
    public async Task NoterMaintenantAsync(string action, string? entite = null, int? entiteId = null, string? details = null)
    {
        Noter(action, entite, entiteId, details);
        await db.SaveChangesAsync();
    }

    private static string Tronquer(string s, int max) => s.Length <= max ? s : s[..max];
}
