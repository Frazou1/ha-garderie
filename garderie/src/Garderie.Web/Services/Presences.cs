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

/// <summary>Une demi-journee a l'ecran / One half-day on screen.</summary>
public sealed record CaseDemi(string Code, bool Enregistre, bool Prevu);

/// <summary>Une ligne de l'ecran des presences / One row of the attendance screen.</summary>
public sealed record LignePresence(Enfant Enfant, CaseDemi Matin, CaseDemi Pm)
{
    public bool Prevu => Matin.Prevu || Pm.Prevu;
}

public sealed class PresencesService(GarderieDb db)
{
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
    /// Code propose (non enregistre) pour une demi-journee, selon l'horaire et les fermetures.
    /// Regle a valider avec la RSGE : journee complete prevue = P le matin et P l'apres-midi ;
    /// demi-journee prevue = « P 1/2 » ; fermeture = F (ou « F 1/2 » si une seule demi-journee est prevue/fermee).
    /// Suggested (unsaved) code for a half-day. Rule to be validated with the RSGE.
    /// </summary>
    public static string Proposer(bool prevuMatin, bool prevuPm, DemiJournee demi, IReadOnlyCollection<JourFermeture> fermetures)
    {
        var prevu = demi == DemiJournee.Matin ? prevuMatin : prevuPm;
        if (!prevu) return "";
        var journeeComplete = prevuMatin && prevuPm;
        var ferme = fermetures.Any(f => f.Demi is null || f.Demi == demi);
        var fermeComplet = fermetures.Any(f => f.Demi is null);
        if (ferme) return journeeComplete && fermeComplet ? "F" : "F 1/2";
        return journeeComplete ? "P" : "P 1/2";
    }

    /// <summary>Lignes de l'ecran pour un jour / Screen rows for one day.</summary>
    public async Task<List<LignePresence>> ChargerJourAsync(DateOnly jour)
    {
        var enfants = await db.Enfants.AsNoTracking()
            .Include(e => e.Horaires)
            .Where(e => e.DateDebut <= jour && (e.DateFin == null || e.DateFin >= jour))
            .OrderBy(e => e.Prenom).ThenBy(e => e.Nom)
            .ToListAsync();
        var presences = await db.Presences.AsNoTracking().Where(p => p.Date == jour).ToListAsync();
        var fermetures = await db.Fermetures.AsNoTracking().Where(f => f.Date == jour).ToListAsync();

        var lignes = new List<LignePresence>();
        foreach (var e in enfants)
        {
            var (m, p) = HorairePrevu(e.Horaires, jour);
            CaseDemi Case(DemiJournee d, bool prevu)
            {
                var existante = presences.FirstOrDefault(x => x.EnfantId == e.Id && x.Demi == d);
                return existante is not null
                    ? new CaseDemi(existante.Code, true, prevu)
                    : new CaseDemi(Proposer(m, p, d, fermetures), false, prevu);
            }
            lignes.Add(new LignePresence(e, Case(DemiJournee.Matin, m), Case(DemiJournee.Pm, p)));
        }
        // Enfants prevus d'abord / Planned children first
        return lignes.OrderByDescending(l => l.Prevu).ToList();
    }

    /// <summary>
    /// Enregistre les codes d'une journee (code vide = supprime). Retourne le nombre de changements.
    /// Saves a day's codes (empty code = delete). Returns the number of changes.
    /// </summary>
    public async Task<int> EnregistrerJourAsync(DateOnly jour, IEnumerable<(int EnfantId, DemiJournee Demi, string Code)> codes, string utilisateur)
    {
        var existantes = await db.Presences.Where(p => p.Date == jour).ToListAsync();
        var idsValides = (await db.Enfants.Select(e => e.Id).ToListAsync()).ToHashSet();
        var changements = 0;
        foreach (var (enfantId, demi, brut) in codes)
        {
            var code = (brut ?? "").Trim();
            if (!idsValides.Contains(enfantId) || !CodesPresence.EstValide(code)) continue;
            var p = existantes.FirstOrDefault(x => x.EnfantId == enfantId && x.Demi == demi);
            if (string.IsNullOrEmpty(code))
            {
                if (p is not null) { db.Presences.Remove(p); changements++; }
                continue;
            }
            if (p is null)
            {
                db.Presences.Add(new Presence { EnfantId = enfantId, Date = jour, Demi = demi, Code = code, ModifieLe = DateTime.UtcNow, ModifiePar = utilisateur });
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
