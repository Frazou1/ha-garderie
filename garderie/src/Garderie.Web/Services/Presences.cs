using Garderie.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace Garderie.Web.Services;

/// <summary>Date du jour au Quebec / Today's date in Quebec.</summary>
public static class Temps
{
    private static readonly TimeZoneInfo Fuseau = TimeZoneInfo.FindSystemTimeZoneById("America/Toronto");
    public static DateTime MaintenantLocal() => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Fuseau);
    public static DateOnly Aujourdhui() => DateOnly.FromDateTime(MaintenantLocal());
    public static DateTime VersLocal(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Fuseau);
}

/// <summary>Une case (un jour) a l'ecran / One day cell on screen.</summary>
/// <param name="Inscrit">L'enfant est inscrit ce jour-la (sinon pas de case) / Enrolled that day.</param>
public sealed record CaseJour(DateOnly Date, string Code, bool Enregistre, bool Inscrit);

/// <summary>Une ligne (un enfant, une semaine) / One row: one child, one week.</summary>
public sealed record LigneSemaine(Enfant Enfant, IReadOnlyList<CaseJour> Jours);

public sealed class PresencesService(GarderieDb db)
{
    /// <summary>Lundi de la semaine du jour donne / Monday of the given day's week.</summary>
    public static DateOnly Lundi(DateOnly jour) => jour.AddDays(-(((int)jour.DayOfWeek + 6) % 7));

    /// <summary>Jours ouvrables (lundi au vendredi) de la semaine / Weekdays of the week.</summary>
    public static IReadOnlyList<DateOnly> JoursOuvrables(DateOnly jour)
    {
        var lundi = Lundi(jour);
        return Enumerable.Range(0, 5).Select(lundi.AddDays).ToList();
    }

    /// <summary>
    /// Horaire applicable : la version la plus recente dont DateEffective &lt;= jour.
    /// Applicable schedule: latest version effective on or before the day.
    /// </summary>
    public static (bool Matin, bool Pm) HorairePrevu(IEnumerable<HoraireType> horaires, DateOnly jour)
    {
        var liste = horaires.Where(h => h.DateEffective <= jour).ToList();
        if (liste.Count == 0) return (false, false);
        var version = liste.Max(h => h.DateEffective);
        var h = liste.FirstOrDefault(x => x.DateEffective == version && x.Jour == jour.DayOfWeek);
        return h is null ? (false, false) : (h.Matin, h.Pm);
    }

    /// <summary>
    /// Code propose (non enregistre) : P tous les jours ouvrables ; F si la garderie est fermee toute la journee,
    /// « F 1/2 » si elle est fermee une demi-journee ; rien la fin de semaine.
    /// Suggested (unsaved) code: P on every weekday; F if closed all day, "F 1/2" if closed half a day.
    /// </summary>
    public static string Proposer(DateOnly jour, IEnumerable<JourFermeture> fermetures)
    {
        if (jour.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) return "";
        var duJour = fermetures.Where(f => f.Date == jour).ToList();
        if (duJour.Any(f => f.Demi is null)) return "F";
        if (duJour.Count > 0) return "F 1/2";
        return "P";
    }

    /// <summary>Lignes de la semaine (enfants inscrits au moins un jour) / Week rows.</summary>
    public async Task<List<LigneSemaine>> ChargerSemaineAsync(DateOnly jour)
    {
        var jours = JoursOuvrables(jour);
        var (debut, fin) = (jours[0], jours[^1]);
        var enfants = await db.Enfants.AsNoTracking()
            .Where(e => e.DateDebut <= fin && (e.DateFin == null || e.DateFin >= debut))
            .OrderBy(e => e.Prenom).ThenBy(e => e.Nom)
            .ToListAsync();
        var presences = await db.Presences.AsNoTracking().Where(p => p.Date >= debut && p.Date <= fin).ToListAsync();
        var fermetures = await db.Fermetures.AsNoTracking().Where(f => f.Date >= debut && f.Date <= fin).ToListAsync();

        return enfants.Select(e => new LigneSemaine(e, jours.Select(d =>
        {
            if (!e.EstInscritLe(d)) return new CaseJour(d, "", false, false);
            var existante = presences.FirstOrDefault(x => x.EnfantId == e.Id && x.Date == d);
            return existante is not null
                ? new CaseJour(d, existante.Code, true, true)
                : new CaseJour(d, Proposer(d, fermetures), false, true);
        }).ToList())).ToList();
    }

    /// <summary>
    /// Enregistre des codes (code vide = supprime). Ignore les jours ou l'enfant n'est pas inscrit.
    /// Retourne le nombre de changements. / Saves codes (empty = delete); returns the number of changes.
    /// </summary>
    public async Task<int> EnregistrerAsync(IEnumerable<(int EnfantId, DateOnly Date, string Code)> codes, string utilisateur)
    {
        var liste = codes.ToList();
        if (liste.Count == 0) return 0;
        var (debut, fin) = (liste.Min(c => c.Date), liste.Max(c => c.Date));
        var existantes = await db.Presences.Where(p => p.Date >= debut && p.Date <= fin).ToListAsync();
        var enfants = await db.Enfants.AsNoTracking().ToDictionaryAsync(e => e.Id);
        var changements = 0;
        foreach (var (enfantId, date, brut) in liste)
        {
            var code = (brut ?? "").Trim();
            if (!enfants.TryGetValue(enfantId, out var e) || !e.EstInscritLe(date) || !CodesPresence.EstValide(code)) continue;
            var p = existantes.FirstOrDefault(x => x.EnfantId == enfantId && x.Date == date);
            if (string.IsNullOrEmpty(code))
            {
                if (p is not null) { db.Presences.Remove(p); existantes.Remove(p); changements++; }
                continue;
            }
            if (p is null)
            {
                p = new Presence { EnfantId = enfantId, Date = date, Code = code, ModifieLe = DateTime.UtcNow, ModifiePar = utilisateur };
                db.Presences.Add(p);
                existantes.Add(p);
                changements++;
            }
            else if (p.Code != code)
            {
                p.Code = code; p.ModifieLe = DateTime.UtcNow; p.ModifiePar = utilisateur;
                changements++;
            }
        }
        await db.SaveChangesAsync();
        return changements;
    }
}
