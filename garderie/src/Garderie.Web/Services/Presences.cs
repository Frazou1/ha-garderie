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

/// <summary>Une demi-case (matin ou apres-midi) a l'ecran / One half cell on screen.</summary>
public sealed record CaseDemi(DemiJournee Demi, string Code, bool Enregistre);

/// <summary>Un jour : 2 demi-cases, ou rien si l'enfant n'est pas inscrit / One day: 2 half cells.</summary>
public sealed record JourSemaine(DateOnly Date, bool Inscrit, CaseDemi Matin, CaseDemi Pm);

/// <summary>Une ligne (un enfant, une semaine) / One row: one child, one week.</summary>
public sealed record LigneSemaine(Enfant Enfant, IReadOnlyList<JourSemaine> Jours);

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
    /// Code propose (non enregistre) pour une demi-case : P du lundi au vendredi (journee complete = P dans les
    /// 2 cases) ; fermeture toute la journee = F dans les 2 ; fermeture d'une demi-journee = « F 1/2 » dans la
    /// demi-case fermee et « P 1/2 » dans l'autre ; rien la fin de semaine.
    /// Suggested (unsaved) code for a half cell.
    /// </summary>
    public static string Proposer(DateOnly jour, DemiJournee demi, IEnumerable<JourFermeture> fermetures)
    {
        if (jour.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) return "";
        var duJour = fermetures.Where(f => f.Date == jour).ToList();
        if (duJour.Count == 0) return "P";
        if (duJour.Any(f => f.Demi is null)) return "F";
        return duJour.Any(f => f.Demi == demi) ? "F 1/2" : "P 1/2";
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

        CaseDemi Case(Enfant e, DateOnly d, DemiJournee demi)
        {
            var existante = presences.FirstOrDefault(x => x.EnfantId == e.Id && x.Date == d && x.Demi == demi);
            return existante is not null ? new CaseDemi(demi, existante.Code, true) : new CaseDemi(demi, Proposer(d, demi, fermetures), false);
        }
        return enfants.Select(e => new LigneSemaine(e, jours.Select(d => e.EstInscritLe(d)
            ? new JourSemaine(d, true, Case(e, d, DemiJournee.Matin), Case(e, d, DemiJournee.Pm))
            : new JourSemaine(d, false, new CaseDemi(DemiJournee.Matin, "", false), new CaseDemi(DemiJournee.Pm, "", false))
        ).ToList())).ToList();
    }

    /// <summary>
    /// Enregistre des codes (code vide = supprime). Ignore les jours ou l'enfant n'est pas inscrit.
    /// Retourne le nombre de changements. / Saves codes (empty = delete); returns the number of changes.
    /// </summary>
    public async Task<int> EnregistrerAsync(IEnumerable<(int EnfantId, DateOnly Date, DemiJournee Demi, string Code)> codes, string utilisateur)
    {
        var liste = codes.ToList();
        if (liste.Count == 0) return 0;
        var (debut, fin) = (liste.Min(c => c.Date), liste.Max(c => c.Date));
        var existantes = await db.Presences.Where(p => p.Date >= debut && p.Date <= fin).ToListAsync();
        var enfants = await db.Enfants.AsNoTracking().ToDictionaryAsync(e => e.Id);
        var changements = 0;
        foreach (var (enfantId, date, demi, brut) in liste)
        {
            var code = (brut ?? "").Trim();
            if (!enfants.TryGetValue(enfantId, out var e) || !e.EstInscritLe(date) || !Enum.IsDefined(demi) || !CodesPresence.EstValide(code)) continue;
            var p = existantes.FirstOrDefault(x => x.EnfantId == enfantId && x.Date == date && x.Demi == demi);
            if (string.IsNullOrEmpty(code))
            {
                if (p is not null) { db.Presences.Remove(p); existantes.Remove(p); changements++; }
                continue;
            }
            if (p is null)
            {
                p = new Presence { EnfantId = enfantId, Date = date, Demi = demi, Code = code, ModifieLe = DateTime.UtcNow, ModifiePar = utilisateur };
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
