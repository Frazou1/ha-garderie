using System.Security.Cryptography;
using System.Text;
using Garderie.Web.Configuration;
using Garderie.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace Garderie.Web.Services;

/// <summary>Etat d'une fiche pour l'ecran / Sheet status for the screen.</summary>
public sealed record SituationFiche(Enfant Enfant, FicheAssiduite? Fiche, int JoursNonEnregistres);

/// <summary>
/// Fiches d'assiduite aux 2 semaines : periodes, codes figes, PDF, liens uniques des parents, signatures.
/// 2-week attendance sheets: periods, frozen codes, PDF, one-time parent links, signatures.
/// </summary>
public sealed class FichesService(GarderieDb db, GarderieOptions options, DocumentsService documents)
{
    /// <summary>Periode de reference par defaut (semaines du 19 et du 26 octobre 2026) / Default reference period.</summary>
    public static readonly DateOnly AncreParDefaut = new(2026, 10, 19);
    public static readonly TimeSpan DureeLien = TimeSpan.FromDays(7);

    // --- Periodes / Periods ---

    /// <summary>Lundi qui commence la periode de 2 semaines contenant le jour / Monday starting the 2-week period.</summary>
    public static DateOnly DebutPeriode(DateOnly jour, DateOnly ancre)
    {
        var lundiAncre = PresencesService.Lundi(ancre);
        var ecart = jour.DayNumber - lundiAncre.DayNumber;
        var n = (int)Math.Floor(ecart / 14.0);
        return lundiAncre.AddDays(n * 14);
    }

    public async Task<DateOnly> AncreAsync() =>
        (await db.Parametres.AsNoTracking().FirstOrDefaultAsync())?.DebutPeriodes ?? AncreParDefaut;

    // --- Codes / Codes ---

    /// <summary>Codes enregistres de la periode / Saved codes of the period.</summary>
    public async Task<Dictionary<(DateOnly, DemiJournee), string>> CodesAsync(int enfantId, DateOnly debut)
    {
        var fin = debut.AddDays(13);
        return await db.Presences.AsNoTracking()
            .Where(p => p.EnfantId == enfantId && p.Date >= debut && p.Date <= fin && p.Code != "")
            .ToDictionaryAsync(p => (p.Date, p.Demi), p => p.Code);
    }

    /// <summary>
    /// Jours ouvrables ou l'enfant est inscrit mais dont les presences ne sont pas enregistrees.
    /// Weekdays where the child is enrolled but attendance is not saved.
    /// </summary>
    public static List<DateOnly> JoursNonEnregistres(Enfant e, DateOnly debut, IReadOnlyDictionary<(DateOnly, DemiJournee), string> codes) =>
        Enumerable.Range(0, 14).Select(debut.AddDays)
            .Where(d => d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && e.EstInscritLe(d))
            .Where(d => !codes.ContainsKey((d, DemiJournee.Matin)) || !codes.ContainsKey((d, DemiJournee.Pm)))
            .ToList();

    public static string Serialiser(IReadOnlyDictionary<(DateOnly, DemiJournee), string> codes) =>
        string.Join(';', codes.OrderBy(c => c.Key.Item1).ThenBy(c => c.Key.Item2)
            .Select(c => $"{c.Key.Item1:yyyy-MM-dd}{(c.Key.Item2 == DemiJournee.Matin ? 'M' : 'P')}={c.Value}"));

    public static Dictionary<(DateOnly, DemiJournee), string> Lire(string? texte)
    {
        var codes = new Dictionary<(DateOnly, DemiJournee), string>();
        foreach (var morceau in (texte ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var egal = morceau.IndexOf('=');
            if (egal != 11 || !DateOnly.TryParseExact(morceau[..10], "yyyy-MM-dd", out var d)) continue;
            codes[(d, morceau[10] == 'M' ? DemiJournee.Matin : DemiJournee.Pm)] = morceau[(egal + 1)..];
        }
        return codes;
    }

    // --- Fiches / Sheets ---

    /// <summary>Enfants inscrits pendant la periode et l'etat de leur fiche / Children enrolled and their sheet.</summary>
    public async Task<List<SituationFiche>> PeriodeAsync(DateOnly debut)
    {
        var fin = debut.AddDays(13);
        var enfants = await db.Enfants.AsNoTracking()
            .Where(e => e.DateDebut <= fin && (e.DateFin == null || e.DateFin >= debut))
            .OrderBy(e => e.Prenom).ThenBy(e => e.Nom).ToListAsync();
        var fiches = await db.Fiches.AsNoTracking().Where(f => f.Debut == debut).ToListAsync();
        var presences = await db.Presences.AsNoTracking().Where(p => p.Date >= debut && p.Date <= fin && p.Code != "").ToListAsync();
        return enfants.Select(e =>
        {
            var codes = presences.Where(p => p.EnfantId == e.Id).ToDictionary(p => (p.Date, p.Demi), p => p.Code);
            return new SituationFiche(e, fiches.FirstOrDefault(f => f.EnfantId == e.Id), JoursNonEnregistres(e, debut, codes).Count);
        }).ToList();
    }

    public async Task<FicheAssiduite> ObtenirOuCreerAsync(int enfantId, DateOnly debut)
    {
        var f = await db.Fiches.Include(x => x.Enfant).FirstOrDefaultAsync(x => x.EnfantId == enfantId && x.Debut == debut);
        if (f is not null) return f;
        f = new FicheAssiduite { EnfantId = enfantId, Debut = debut, CreeLe = DateTime.UtcNow, ModifieLe = DateTime.UtcNow };
        db.Fiches.Add(f);
        await db.SaveChangesAsync();
        await db.Entry(f).Reference(x => x.Enfant).LoadAsync();
        return f;
    }

    /// <summary>Parent payeur (nom sur la fiche), sinon le premier / Paying parent, else the first one.</summary>
    public async Task<Parent?> ParentPayeurAsync(int enfantId) =>
        await db.EnfantParents.AsNoTracking().Where(x => x.EnfantId == enfantId)
            .OrderByDescending(x => x.EstPayeur).ThenBy(x => x.Ordre)
            .Select(x => x.Parent).FirstOrDefaultAsync();

    public bool ModeleDisponible => File.Exists(options.FichierModeleFo0310);

    /// <summary>
    /// PDF de la fiche : codes figes si la RSGE a signe, sinon codes actuels (apercu).
    /// Sheet PDF: frozen codes once the RSGE signed, else current codes (preview).
    /// </summary>
    public async Task<byte[]> GenererPdfAsync(FicheAssiduite f)
    {
        var modele = await File.ReadAllBytesAsync(options.FichierModeleFo0310);
        var p = await db.Parametres.AsNoTracking().FirstOrDefaultAsync();
        var e = f.Enfant ?? await db.Enfants.AsNoTracking().FirstAsync(x => x.Id == f.EnfantId);
        var codes = f.Etat == EtatFiche.Brouillon ? await CodesAsync(f.EnfantId, f.Debut) : Lire(f.CodesSignes);
        var nomParent = f.NomParent ?? (await ParentPayeurAsync(f.EnfantId))?.NomComplet;
        // Date de fin : seulement si l'enfant quitte pendant ou avant cette periode / Only if leaving by this period
        var fin = e.DateFin is { } df && df <= f.Fin ? df : (DateOnly?)null;
        var contenu = new ContenuFiche(
            p?.NomBureauCoordonnateur, e.NomComplet, nomParent, p?.NomRsge, fin, f.Debut, codes,
            f.DatePaiement1, f.MontantPaye1, f.Solde1, f.DatePaiement2, f.MontantPaye2, f.Solde2,
            await documents.LireFichierAsync(f.SignatureRsgeFichier), f.SigneRsgeLe is { } r ? DateOnly.FromDateTime(Temps.VersLocal(r)) : null,
            await documents.LireFichierAsync(f.SignatureParentFichier), f.SigneParentLe is { } s ? DateOnly.FromDateTime(Temps.VersLocal(s)) : null);
        return Fo0310Pdf.Remplir(modele, contenu);
    }

    public static string NomPdf(FicheAssiduite f, Enfant e) =>
        $"FO-0310 {e.Prenom} {e.Nom} {f.Debut:yyyy-MM-dd}.pdf";

    // --- Liens uniques / One-time links ---

    public static string Hacher(string jeton) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(jeton)));

    /// <summary>Cree un lien (le jeton en clair n'est jamais stocke) / Creates a link (clear token never stored).</summary>
    public async Task<string> CreerLienAsync(FicheAssiduite f, string? envoyeA, bool surPlace)
    {
        var jeton = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        db.Liens.Add(new LienSignature
        {
            FicheId = f.Id, JetonHash = Hacher(jeton), CreeLe = DateTime.UtcNow,
            ExpireLe = DateTime.UtcNow + (surPlace ? TimeSpan.FromHours(1) : DureeLien), EnvoyeA = envoyeA, SurPlace = surPlace,
        });
        await db.SaveChangesAsync();
        return jeton;
    }

    /// <summary>Lien valide (non utilise, non annule, non expire, fiche signee par la RSGE) / Valid link.</summary>
    public async Task<LienSignature?> TrouverLienAsync(string? jeton)
    {
        if (string.IsNullOrWhiteSpace(jeton) || jeton.Length > 100) return null;
        var hash = Hacher(jeton);
        var lien = await db.Liens.Include(l => l.Fiche).ThenInclude(f => f.Enfant).FirstOrDefaultAsync(l => l.JetonHash == hash);
        if (lien is null || lien.UtiliseLe is not null || lien.AnnuleLe is not null || lien.ExpireLe < DateTime.UtcNow) return null;
        return lien.Fiche.Etat == EtatFiche.SigneeRsge ? lien : null;
    }

    public async Task AnnulerLiensAsync(int ficheId)
    {
        await db.Liens.Where(l => l.FicheId == ficheId && l.UtiliseLe == null && l.AnnuleLe == null)
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.AnnuleLe, DateTime.UtcNow));
    }

    /// <summary>Adresse publique du lien (vide si l'adresse publique n'est pas configuree) / Public link URL.</summary>
    public string? UrlLien(string jeton) =>
        string.IsNullOrWhiteSpace(options.UrlPublique) ? null : $"{options.UrlPublique}/Public/Signer?j={jeton}";

    // --- Signatures ---

    /// <summary>
    /// Image PNG d'une signature (data URL du canevas), ou null si invalide. Max 400 Ko.
    /// PNG signature image from the canvas data URL, or null if invalid.
    /// </summary>
    public static byte[]? LireSignature(string? dataUrl)
    {
        const string prefixe = "data:image/png;base64,";
        if (dataUrl is null || !dataUrl.StartsWith(prefixe, StringComparison.Ordinal) || dataUrl.Length > 600_000) return null;
        try
        {
            var png = Convert.FromBase64String(dataUrl[prefixe.Length..]);
            return DocumentsService.DetecterType(png) == "image/png" && png.Length <= 400_000 ? png : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
