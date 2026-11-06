using System.Security.Cryptography;
using System.Text;
using Garderie.Web.Configuration;
using Garderie.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace Garderie.Web.Services;

/// <summary>Etat d'une fiche pour l'ecran / Sheet status for the screen.</summary>
/// <param name="AParent">Un parent est au dossier (son nom va sur la fiche) / A parent is on file.</param>
/// <param name="LienActif">Un lien de signature non utilise existe deja / An unused signature link exists.</param>
public sealed record SituationFiche(Enfant Enfant, FicheAssiduite? Fiche, int JoursNonEnregistres, bool AParent = true, bool LienActif = false)
{
    public EtatFiche Etat => Fiche?.Etat ?? EtatFiche.Brouillon;
    /// <summary>Prete pour la signature groupee de la RSGE / Ready for the RSGE's batch signature.</summary>
    public bool PreteASigner => Etat == EtatFiche.Brouillon && JoursNonEnregistres == 0 && AParent;
}

/// <summary>
/// Fiches d'assiduite aux 2 semaines : periodes, codes figes, PDF, liens uniques des parents, signatures.
/// 2-week attendance sheets: periods, frozen codes, PDF, one-time parent links, signatures.
/// </summary>
public sealed class FichesService(GarderieDb db, GarderieOptions options, DocumentsService documents, Courriels courriels)
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

    /// <summary>Derniere date des fiches signees sur papier (Reglages) / Last date of paper-signed sheets.</summary>
    public async Task<DateOnly?> PapierJusquauAsync() =>
        (await db.Parametres.AsNoTracking().FirstOrDefaultAsync())?.FichesPapierJusquau;

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
        var ids = enfants.Select(e => e.Id).ToList();
        var avecParent = (await db.EnfantParents.AsNoTracking().Where(x => ids.Contains(x.EnfantId)).Select(x => x.EnfantId).ToListAsync()).ToHashSet();
        var actifs = await AvecLienActifAsync(fiches.Select(f => f.Id));
        return enfants.Select(e =>
        {
            var codes = presences.Where(p => p.EnfantId == e.Id).ToDictionary(p => (p.Date, p.Demi), p => p.Code);
            var f = fiches.FirstOrDefault(x => x.EnfantId == e.Id);
            return new SituationFiche(e, f, JoursNonEnregistres(e, debut, codes).Count, avecParent.Contains(e.Id), f is not null && actifs.Contains(f.Id));
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

    // --- Signature de la RSGE / RSGE signature ---

    /// <summary>
    /// Signe la fiche d'un enfant pour la RSGE : fige les codes et le nom du parent. Retourne une erreur ou null.
    /// Signs one child's sheet for the RSGE: freezes codes and parent name. Returns an error or null.
    /// </summary>
    public async Task<string?> SignerRsgeAsync(int enfantId, DateOnly debut, byte[] png, string? par)
    {
        var e = await db.Enfants.AsNoTracking().FirstOrDefaultAsync(x => x.Id == enfantId);
        if (e is null) return "Enfant introuvable.";
        if (await PapierJusquauAsync() is { } papier && debut.AddDays(13) <= papier)
            return "Cette période a été signée sur papier (Réglages) : aucune fiche électronique.";
        if (!ModeleDisponible) return "Téléversez d'abord le formulaire FO-0310 dans Réglages.";
        var existante = await db.Fiches.AsNoTracking().FirstOrDefaultAsync(f => f.EnfantId == enfantId && f.Debut == debut);
        if (existante is not null && existante.Etat != EtatFiche.Brouillon) return $"{e.Prenom} : fiche déjà signée.";
        var codes = await CodesAsync(enfantId, debut);
        if (JoursNonEnregistres(e, debut, codes).Count > 0) return $"{e.Prenom} : enregistrez d'abord toutes les présences de la période.";
        var parent = await ParentPayeurAsync(enfantId);
        if (parent is null) return $"{e.Prenom} : ajoutez d'abord un parent au dossier.";

        var f = await ObtenirOuCreerAsync(enfantId, debut);
        f.CodesSignes = Serialiser(codes);
        f.NomParent = parent.NomComplet;
        f.ParentId = parent.Id;
        f.SignatureRsgeFichier = await documents.EnregistrerOctetsAsync(png);
        f.SigneRsgeLe = DateTime.UtcNow;
        f.SigneRsgePar = par;
        f.ErreurSignalee = null; f.ErreurSignaleeLe = null;
        f.Etat = EtatFiche.SigneeRsge;
        f.ModifieLe = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return null;
    }

    // --- Liens uniques / One-time links ---

    public static string Hacher(string jeton) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(jeton)));

    /// <summary>
    /// Cree un lien pour une ou plusieurs fiches (fratrie) ; le jeton en clair n'est jamais stocke.
    /// Creates a link for one or more sheets (siblings); the clear token is never stored.
    /// </summary>
    public async Task<string> CreerLienAsync(IReadOnlyList<FicheAssiduite> fiches, string? envoyeA, bool surPlace)
    {
        var jeton = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var lien = new LienSignature
        {
            FicheId = fiches[0].Id, JetonHash = Hacher(jeton), CreeLe = DateTime.UtcNow,
            ExpireLe = DateTime.UtcNow + (surPlace ? TimeSpan.FromHours(1) : DureeLien), EnvoyeA = envoyeA, SurPlace = surPlace,
        };
        foreach (var f in fiches) lien.Groupe.Add(new LienFiche { FicheId = f.Id });
        db.Liens.Add(lien);
        await db.SaveChangesAsync();
        return jeton;
    }

    /// <summary>
    /// Lien valide (non utilise, non annule, non expire) avec au moins une fiche a signer par le parent.
    /// Valid link with at least one sheet awaiting the parent.
    /// </summary>
    public async Task<LienSignature?> TrouverLienAsync(string? jeton)
    {
        if (string.IsNullOrWhiteSpace(jeton) || jeton.Length > 100) return null;
        var hash = Hacher(jeton);
        var lien = await db.Liens.Include(l => l.Groupe).ThenInclude(g => g.Fiche).ThenInclude(f => f.Enfant)
            .AsSplitQuery().FirstOrDefaultAsync(l => l.JetonHash == hash);
        if (lien is null || lien.UtiliseLe is not null || lien.AnnuleLe is not null || lien.ExpireLe < DateTime.UtcNow) return null;
        return AttendentLeParent(lien).Count > 0 ? lien : null;
    }

    /// <summary>Fiches du lien en attente du parent, par prenom / Link sheets awaiting the parent.</summary>
    public static List<FicheAssiduite> AttendentLeParent(LienSignature lien) =>
        lien.Groupe.Select(g => g.Fiche).Where(f => f.Etat == EtatFiche.SigneeRsge)
            .OrderBy(f => f.Enfant.Prenom).ThenBy(f => f.Enfant.Nom).ToList();

    /// <summary>
    /// Le lien a deja servi a signer (ex. double toucher sur « Signer ») : on remercie au lieu d'une erreur.
    /// The link was already used to sign (e.g. double tap): thank instead of an error.
    /// </summary>
    public async Task<bool> DejaSigneParCeLienAsync(string? jeton)
    {
        if (string.IsNullOrWhiteSpace(jeton) || jeton.Length > 100) return false;
        var hash = Hacher(jeton);
        return await db.Liens.AnyAsync(l => l.JetonHash == hash && l.UtiliseLe != null && l.Fiche.Etat == EtatFiche.SigneeParent);
    }

    /// <summary>Annule les liens actifs qui couvrent cette fiche / Cancels active links covering this sheet.</summary>
    public async Task AnnulerLiensAsync(int ficheId)
    {
        await db.Liens.Where(l => l.UtiliseLe == null && l.AnnuleLe == null && l.Groupe.Any(g => g.FicheId == ficheId))
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.AnnuleLe, DateTime.UtcNow));
    }

    /// <summary>Fiches de la liste qui ont deja un lien actif / Sheets that already have an active link.</summary>
    public async Task<HashSet<int>> AvecLienActifAsync(IEnumerable<int> ficheIds)
    {
        var ids = ficheIds.ToList();
        var maintenant = DateTime.UtcNow;
        return (await db.LiensFiches.AsNoTracking()
            .Where(g => ids.Contains(g.FicheId) && g.Lien.UtiliseLe == null && g.Lien.AnnuleLe == null && g.Lien.ExpireLe > maintenant)
            .Select(g => g.FicheId).ToListAsync()).ToHashSet();
    }

    /// <summary>Adresse publique du lien (vide si l'adresse publique n'est pas configuree) / Public link URL.</summary>
    public string? UrlLien(string jeton) =>
        string.IsNullOrWhiteSpace(options.UrlPublique) ? null : $"{options.UrlPublique}/Public/Signer?j={jeton}";

    /// <summary>
    /// Envoie au parent UN courriel et UN lien pour toutes ces fiches (meme parent, meme periode).
    /// Annule les liens precedents. Retourne le courriel du parent. Leve une exception si l'envoi echoue.
    /// Sends ONE email and ONE link to the parent for all these sheets; cancels previous links.
    /// </summary>
    public async Task<string> EnvoyerLienAsync(IReadOnlyList<FicheAssiduite> fiches)
    {
        var parentId = fiches[0].ParentId ?? throw new InvalidOperationException("Fiche sans parent.");
        if (fiches.Any(f => f.ParentId != parentId || f.Debut != fiches[0].Debut || f.Etat != EtatFiche.SigneeRsge))
            throw new InvalidOperationException("Fiches de parents ou de périodes différents.");
        var parent = await db.Parents.AsNoTracking().FirstAsync(p => p.Id == parentId);
        if (parent.Courriel is null) throw new InvalidOperationException("Le parent n'a pas de courriel.");
        foreach (var f in fiches) await AnnulerLiensAsync(f.Id);
        var jeton = await CreerLienAsync(fiches, parent.Courriel, surPlace: false);

        var p = await db.Parametres.AsNoTracking().FirstOrDefaultAsync();
        var garderie = p?.NomGarderie ?? "la garderie";
        var prenoms = Liste(fiches.Select(f => f.Enfant.Prenom).ToList());
        var plusieurs = fiches.Count > 1;
        var periode = Affichage.Periode(fiches[0].Debut, fiches[0].Fin);
        try
        {
            await courriels.EnvoyerAsync(parent.Courriel,
                $"{(plusieurs ? "Fiches" : "Fiche")} d'assiduité de {prenoms} à signer ({periode})",
                $"Bonjour {parent.Prenom},\n\n" +
                $"{(plusieurs ? "Les fiches" : "La fiche")} d'assiduité de {prenoms} pour la période du {periode} " +
                $"{(plusieurs ? "sont prêtes" : "est prête")}.\n" +
                $"Vérifiez et signez ici (lien personnel, valide 7 jours, une seule utilisation" +
                $"{(plusieurs ? ", une seule signature pour toutes les fiches" : "")}) :\n\n" +
                $"{UrlLien(jeton)}\n\n" +
                "Si un code vous semble inexact, utilisez « Signaler une erreur » sur la même page.\n\n" +
                Affichage.PiedFacebook(p) +
                $"Merci,\n{p?.NomRsge ?? garderie}\n{garderie}");
        }
        catch
        {
            foreach (var f in fiches) await AnnulerLiensAsync(f.Id);
            throw;
        }
        return parent.Courriel;
    }

    /// <summary>« Léa », « Léa et Noah », « Léa, Noah et Zoé » / French list.</summary>
    public static string Liste(IReadOnlyList<string> noms) =>
        noms.Count <= 1 ? string.Concat(noms) : string.Join(", ", noms.Take(noms.Count - 1)) + " et " + noms[^1];

    // --- Signature du parent / Parent signature ---

    /// <summary>
    /// Signature du parent pour toutes les fiches du lien : PDF final de chacune range dans son dossier,
    /// lien marque utilise. Retourne les PDF (pour le courriel a la RSGE).
    /// Parent signs all the link's sheets: each final PDF stored in its record, link marked used.
    /// </summary>
    public async Task<List<(FicheAssiduite Fiche, Document Document, byte[] Pdf)>> SignerParentAsync(
        LienSignature lien, byte[] png, string signataire, string ip)
    {
        var resultats = new List<(FicheAssiduite, Document, byte[])>();
        foreach (var f in AttendentLeParent(lien))
        {
            f.SignatureParentFichier = await documents.EnregistrerOctetsAsync(png);
            f.SigneParentLe = DateTime.UtcNow;
            f.SignataireParent = signataire;
            f.IpParent = ip.Length > 64 ? ip[..64] : ip;
            f.SigneSurPlace = lien.SurPlace;
            f.Etat = EtatFiche.SigneeParent;
            f.ModifieLe = DateTime.UtcNow;
            var pdf = await GenererPdfAsync(f);
            var doc = await documents.EnregistrerPdfAsync(f.EnfantId, TypeDocument.FicheAssiduite, NomPdf(f, f.Enfant), pdf,
                $"Période du {Affichage.Periode(f.Debut, f.Fin)}");
            db.Documents.Add(doc);
            await db.SaveChangesAsync();
            f.DocumentId = doc.Id;
            f.PdfSha256 = doc.Sha256;
            resultats.Add((f, doc, pdf));
        }
        lien.UtiliseLe = DateTime.UtcNow;
        await db.SaveChangesAsync();
        foreach (var (f, _, _) in resultats) await AnnulerLiensAsync(f.Id);
        return resultats;
    }

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
