using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Garderie.Web.Configuration;
using Garderie.Web.Data;
using Garderie.Web.Securite;
using Microsoft.EntityFrameworkCore;

namespace Garderie.Web.Services;

/// <summary>
/// Releves 24 de l'annee : propositions par parent payeur (jours payes tires des presences et de l'horaire, montant
/// selon le tarif), generation sur le formulaire officiel (numero, PDF dans le dossier), sommaire 24.
/// Yearly RL-24 slips: proposals per paying parent, generation on the official form, RL-24.S summary.
/// </summary>
public sealed partial class RelevesService(GarderieDb db, GarderieOptions options, DocumentsService documents, Chiffreur chiffreur)
{
    /// <summary>Codes payes par le parent (presence, absence, fermeture) / Codes the parent pays for.</summary>
    private static readonly HashSet<string> CodesPayes = ["P", "P 1/2", "A", "A 1/2", "F", "F 1/2", "R", "R 1/2"];

    public sealed record EnfantPropose(int EnfantId, string Nom, string Prenom, DateOnly? Naissance, int Jours, decimal Montant);

    public sealed record Proposition(int ParentId, string Nom, string Prenom, List<EnfantPropose> Enfants, Releve? Dernier);

    /// <summary>Annee proposee : l'annee passee jusqu'en avril, sinon l'annee en cours / Suggested year.</summary>
    public static int AnneeProposee(DateOnly aujourdhui) => aujourdhui.Month <= 4 ? aujourdhui.Year - 1 : aujourdhui.Year;

    /// <summary>Tarif par jour tire de Reglages (« 45 $ ») / Daily rate from Settings.</summary>
    public static decimal? Tarif(string? texte)
    {
        var chiffres = new string((texte ?? "").Where(c => char.IsAsciiDigit(c) || c is '.' or ',').ToArray()).Replace(',', '.');
        return decimal.TryParse(chiffres, NumberStyles.Number, CultureInfo.InvariantCulture, out var t) && t > 0 ? t : null;
    }

    public bool FormulairesPresents => File.Exists(options.FichierRl24) && File.Exists(options.FichierRl24S);

    /// <summary>Un releve par parent payeur ayant paye au moins un jour dans l'annee / One slip per paying parent.</summary>
    public async Task<List<Proposition>> PropositionsAsync(int annee)
    {
        var (debut, fin) = (new DateOnly(annee, 1, 1), new DateOnly(annee, 12, 31));
        var p = await db.Parametres.AsNoTracking().FirstOrDefaultAsync() ?? new Parametres();
        var tarif = Tarif(p.RegieTarif) ?? 0m;
        var enfants = await db.Enfants.AsNoTracking().Include(e => e.Horaires).Include(e => e.Parents).ThenInclude(x => x.Parent)
            .Where(e => e.DateDebut <= fin && (e.DateFin == null || e.DateFin >= debut)).ToListAsync();
        var ids = enfants.Select(e => e.Id).ToList();
        var codes = (await db.Presences.AsNoTracking().Where(x => ids.Contains(x.EnfantId) && x.Date >= debut && x.Date <= fin)
                .Select(x => new { x.EnfantId, x.Date, x.Code }).ToListAsync())
            .GroupBy(x => x.EnfantId).ToDictionary(g => g.Key, g => g.GroupBy(x => x.Date).ToDictionary(d => d.Key, d => d.Select(x => x.Code).ToList()));
        var fermetures = await db.Fermetures.AsNoTracking().Where(f => f.Date >= debut && f.Date <= fin).ToListAsync();
        var releves = await db.Releves.AsNoTracking().Where(r => r.Annee == annee && r.RemplaceParId == null).ToListAsync();

        var parPayeur = new Dictionary<int, (Parent Parent, List<EnfantPropose> Enfants)>();
        foreach (var e in enfants.OrderBy(e => e.DateNaissance ?? DateOnly.MaxValue))
        {
            var payeur = e.Parents.OrderByDescending(x => x.EstPayeur).ThenBy(x => x.Ordre).FirstOrDefault()?.Parent;
            if (payeur is null) continue;
            var (jours, demis) = JoursPayes(e, debut, fin, codes.GetValueOrDefault(e.Id), fermetures);
            if (jours == 0) continue;
            if (!parPayeur.TryGetValue(payeur.Id, out var g)) parPayeur[payeur.Id] = g = (payeur, new());
            g.Enfants.Add(new(e.Id, e.Nom, e.Prenom, e.DateNaissance, jours, (jours - demis) * tarif + demis * tarif / 2));
        }
        return parPayeur.Values.OrderBy(g => g.Parent.Nom).ThenBy(g => g.Parent.Prenom)
            .Select(g => new Proposition(g.Parent.Id, g.Parent.Nom, g.Parent.Prenom, g.Enfants,
                releves.Where(r => r.ParentId == g.Parent.Id).OrderByDescending(r => r.Id).FirstOrDefault()))
            .ToList();
    }

    /// <summary>
    /// Jours payes de l'annee : codes enregistres, sinon code propose selon l'horaire (P ou fermeture F) ; une
    /// demi-journee compte pour un jour (case B.1). / Paid days: saved codes, else suggested code from the schedule.
    /// </summary>
    public static (int Jours, int Demis) JoursPayes(Enfant e, DateOnly debut, DateOnly fin,
        Dictionary<DateOnly, List<string>>? enregistres, List<JourFermeture> fermetures)
    {
        int jours = 0, demis = 0;
        var aujourdhui = Temps.Aujourdhui();
        for (var d = Max(debut, e.DateDebut); d <= fin && d <= (e.DateFin ?? fin) && d <= aujourdhui; d = d.AddDays(1))
        {
            if (d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;
            List<string> duJour;
            if (enregistres?.GetValueOrDefault(d) is { Count: > 0 } c) duJour = c;
            else
            {
                var (matin, pm) = e.Horaires.Count == 0 ? (true, true) : PresencesService.HorairePrevu(e.Horaires, d);
                if (!matin && !pm) continue;
                duJour = [];
                if (matin) duJour.Add(PresencesService.Proposer(d, DemiJournee.Matin, fermetures));
                if (pm) duJour.Add(PresencesService.Proposer(d, DemiJournee.Pm, fermetures));
            }
            var payes = duJour.Where(CodesPayes.Contains).ToList();
            if (payes.Count == 0) continue;
            jours++;
            if (payes.Count == 1) demis++;   // une seule demi-case payee / only one paid half
        }
        return (jours, demis);
    }

    private static DateOnly Max(DateOnly a, DateOnly b) => a > b ? a : b;

    // --- Adresse / Address ---

    [GeneratedRegex(@"([A-Za-z]\d[A-Za-z])\s*-?\s*(\d[A-Za-z]\d)\s*$")]
    private static partial Regex RegexCodePostal();

    /// <summary>
    /// Decoupe une adresse saisie librement (« 123 rue X, app. 4, Quebec (Quebec) G1A 1A1 ») ; a verifier.
    /// Splits a free-form address; to be checked by the user.
    /// </summary>
    public static Releve24.Adresse Decouper(string? adresse)
    {
        var a = (adresse ?? "").Replace('\n', ',').Trim().TrimEnd(',', '.');
        string? cp = null, app = null, numero = null, ville = null, province = null;
        if (RegexCodePostal().Match(a) is { Success: true } m)
        {
            cp = (m.Groups[1].Value + m.Groups[2].Value).ToUpperInvariant();
            a = a[..m.Index].Trim().TrimEnd(',');
        }
        foreach (var p in new[] { @"\(\s*Qu[ée]bec\s*\)$", @"\bQC$", @"\bQu[ée]bec$" })
            if (Regex.Match(a, p, RegexOptions.IgnoreCase) is { Success: true } q && a.Contains(',', StringComparison.Ordinal))
            {
                province = "QC";
                a = a[..q.Index].Trim().TrimEnd(',');
                break;
            }
        var morceaux = a.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
        if (morceaux.Count > 1) { ville = morceaux[^1]; morceaux.RemoveAt(morceaux.Count - 1); }
        if (morceaux.FirstOrDefault(x => Regex.IsMatch(x, @"^(app\.?|appartement|apt\.?|#)\s*\S+$", RegexOptions.IgnoreCase)) is { } ap)
        {
            app = Regex.Replace(ap, @"^(app\.?|appartement|apt\.?|#)\s*", "", RegexOptions.IgnoreCase);
            morceaux.Remove(ap);
        }
        var rue = string.Join(", ", morceaux);
        if (Regex.Match(rue, @"^(\d+[A-Za-z]?)(?:-(\d+))?\s+(.+)$") is { Success: true } r)
        {
            numero = r.Groups[1].Value;
            if (r.Groups[2].Success) app ??= r.Groups[2].Value;
            rue = r.Groups[3].Value;
        }
        return new(app, numero, rue.Length == 0 ? null : rue, ville, province ?? (cp is null ? null : "QC"), cp);
    }

    // --- Generation ---

    public sealed class Saisie
    {
        public string Nom { get; set; } = "";
        public string Prenom { get; set; } = "";
        public string? Appartement { get; set; }
        public string? Numero { get; set; }
        public string? Rue { get; set; }
        public string? Ville { get; set; }
        public string? Province { get; set; }
        public string? CodePostal { get; set; }
        public List<SaisieEnfant> Enfants { get; set; } = new();
    }

    public sealed class SaisieEnfant
    {
        public int EnfantId { get; set; }
        public int Jours { get; set; }
        public string? Total { get; set; }
        public string? NonAdmissible { get; set; }
    }

    /// <summary>Montant saisi (« 1 234,50 » ou « 1234.50 ») / Entered amount.</summary>
    public static decimal? Montant(string? s)
    {
        var t = (s ?? "").Replace(" ", "").Replace(" ", "").Replace("$", "").Replace(',', '.');
        if (t.Length == 0) return 0m;
        return decimal.TryParse(t, NumberStyles.Number, CultureInfo.InvariantCulture, out var m) && m >= 0 ? Math.Round(m, 2) : null;
    }

    public string? NasDuParent(Parent parent) => chiffreur.DechiffrerTexte(parent.NasChiffre, Contextes.NasParent);

    private async Task<Releve24.Fournisseur> FournisseurAsync(Parametres p) =>
        await Task.FromResult(new Releve24.Fournisseur(p.NomGarderie ?? "", p.NomRsge,
            new Releve24.Adresse(p.Rl24Appartement, p.Rl24Numero, p.Rl24Rue, p.Rl24Ville, p.Rl24Province, p.Rl24CodePostal),
            chiffreur.DechiffrerTexte(p.Rl24IdentifiantChiffre, Contextes.IdentifiantRl24)));

    /// <summary>Ce qui manque dans Reglages pour produire des releves / Missing settings.</summary>
    public async Task<List<string>> PrerequisAsync()
    {
        var p = await db.Parametres.AsNoTracking().FirstOrDefaultAsync() ?? new Parametres();
        var manque = new List<string>();
        if (!File.Exists(options.FichierRl24)) manque.Add("le formulaire RL-24");
        if (!File.Exists(options.FichierRl24S)) manque.Add("le formulaire RL-24.S");
        if (string.IsNullOrWhiteSpace(p.NomGarderie)) manque.Add("le nom de la garderie");
        if (p.Rl24IdentifiantChiffre is null) manque.Add("votre NEQ ou NAS (case H)");
        if (string.IsNullOrWhiteSpace(p.Rl24Rue) || string.IsNullOrWhiteSpace(p.Rl24Ville) || p.Rl24CodePostal is null) manque.Add("l'adresse du service de garde");
        return manque;
    }

    /// <summary>Contenu d'un releve a partir de la saisie ; erreur sinon / Slip content from the input, else an error.</summary>
    public async Task<(Releve24.ContenuReleve? Contenu, string? Erreur)> ContenuAsync(int annee, Parent parent, Saisie s, string numero, char code, string? original)
    {
        var p = await db.Parametres.AsNoTracking().FirstOrDefaultAsync() ?? new Parametres();
        var ids = s.Enfants.Select(x => x.EnfantId).ToList();
        var enfants = await db.Enfants.AsNoTracking().Where(e => ids.Contains(e.Id)).ToDictionaryAsync(e => e.Id);
        var lignes = new List<Releve24.EnfantReleve>();
        foreach (var x in s.Enfants)
        {
            if (!enfants.TryGetValue(x.EnfantId, out var e)) return (null, "Enfant introuvable.");
            var total = Montant(x.Total);
            var non = Montant(x.NonAdmissible);
            if (total is null || non is null) return (null, $"Montant invalide pour {e.Prenom}.");
            if (non > total) return (null, $"Pour {e.Prenom}, les frais non admissibles dépassent le total.");
            if (x.Jours is < 0 or > 366) return (null, $"Nombre de jours invalide pour {e.Prenom}.");
            if (total > 99999.99m) return (null, $"Montant trop élevé pour {e.Prenom}.");
            lignes.Add(new(e.Nom, e.Prenom, e.DateNaissance, x.Jours, total.Value, non.Value));
        }
        if (lignes.Count is < 1 or > 3) return (null, "Un relevé compte de 1 à 3 enfants.");
        if (string.IsNullOrWhiteSpace(s.Nom) || string.IsNullOrWhiteSpace(s.Prenom)) return (null, "Le nom et le prénom du parent sont obligatoires.");
        var cp = new string((s.CodePostal ?? "").Where(char.IsAsciiLetterOrDigit).ToArray()).ToUpperInvariant();
        if (cp.Length is not (0 or 6)) return (null, "Code postal du parent : 6 caractères, ex. G1A 1A1.");
        static string? V(string? t) => string.IsNullOrWhiteSpace(t) ? null : t.Trim();
        var adresse = new Releve24.Adresse(V(s.Appartement), V(s.Numero), V(s.Rue), V(s.Ville), V(s.Province)?.ToUpperInvariant(), cp.Length == 0 ? null : cp);
        return (new Releve24.ContenuReleve(annee, numero, code, original, s.Nom.Trim(), s.Prenom.Trim(), adresse, NasDuParent(parent),
            await FournisseurAsync(p), lignes), null);
    }

    /// <summary>Apercu : prochain numero, rien d'enregistre / Preview: next number, nothing saved.</summary>
    public async Task<(byte[]? Pdf, string? Erreur)> ApercuAsync(int annee, Parent parent, Saisie s)
    {
        var modele = await File.ReadAllBytesAsync(options.FichierRl24);
        var p = await db.Parametres.AsNoTracking().FirstOrDefaultAsync() ?? new Parametres();
        var dernier = await DernierAsync(annee, parent.Id);
        var (contenu, erreur) = await ContenuAsync(annee, parent, s, Releve24.Prochain(FormulaireProtege.LireValeurs(modele)["numSeq"], p.Rl24DernierNumero),
            dernier is null ? 'R' : 'A', dernier?.Numero);
        return contenu is null ? (null, erreur) : (FormulaireProtege.Remplir(modele, Releve24.ChampsReleve(contenu)), null);
    }

    public Task<Releve?> DernierAsync(int annee, int parentId) =>
        db.Releves.Where(r => r.Annee == annee && r.ParentId == parentId && r.RemplaceParId == null).OrderByDescending(r => r.Id).FirstOrDefaultAsync();

    /// <summary>
    /// Produit le releve : reserve le numero, remplit le formulaire, classe le PDF dans le dossier du premier enfant.
    /// S'il en existe deja un pour ce parent et cette annee, c'est un releve modifie (code A) qui le remplace.
    /// Issues the slip: reserves the number, fills the form, files the PDF; an existing slip is amended (code A).
    /// </summary>
    public async Task<(Releve? Releve, string? Erreur)> GenererAsync(int annee, Parent parent, Saisie s, string? par)
    {
        var modele = await File.ReadAllBytesAsync(options.FichierRl24);
        var p = await db.Parametres.FirstAsync();
        var numero = Releve24.Prochain(FormulaireProtege.LireValeurs(modele)["numSeq"], p.Rl24DernierNumero);
        while (await db.Releves.AnyAsync(r => r.Numero == numero)) numero = Releve24.Suivant(numero);
        var precedent = await DernierAsync(annee, parent.Id);
        var (contenu, erreur) = await ContenuAsync(annee, parent, s, numero, precedent is null ? 'R' : 'A', precedent?.Numero);
        if (contenu is null) return (null, erreur);
        var pdf = FormulaireProtege.Remplir(modele, Releve24.ChampsReleve(contenu));

        var premier = s.Enfants[0].EnfantId;
        var nom = $"Relevé 24 {annee} {contenu.PayeurPrenom} {contenu.PayeurNom}{(contenu.Code == 'A' ? " (modifié)" : "")}.pdf";
        var doc = await documents.EnregistrerPdfAsync(premier, TypeDocument.Releve24, nom, pdf, $"No {numero}");
        db.Documents.Add(doc);
        p.Rl24DernierNumero = numero;
        var releve = new Releve
        {
            Annee = annee, ParentId = parent.Id, Numero = numero, Code = contenu.Code.ToString(), NumeroOriginal = contenu.NumeroOriginal,
            Contenu = JsonSerializer.Serialize(contenu with { PayeurNas = null, Fournisseur = contenu.Fournisseur with { Identifiant = null } }),
            EnfantId = premier, CreeLe = DateTime.UtcNow, CreePar = par,
        };
        db.Releves.Add(releve);
        await db.SaveChangesAsync();
        releve.DocumentId = doc.Id;
        if (precedent is not null) precedent.RemplaceParId = releve.Id;
        await db.SaveChangesAsync();
        return (releve, null);
    }

    public static Releve24.ContenuReleve LireContenu(Releve r) => JsonSerializer.Deserialize<Releve24.ContenuReleve>(r.Contenu)!;

    public async Task<byte[]?> PdfAsync(Releve r) =>
        r.DocumentId is { } id && await db.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id) is { } doc ? await documents.LireAsync(doc) : null;

    /// <summary>Totaux de l'annee (releves en vigueur) / Year totals (current slips).</summary>
    public async Task<(int Releves, int Enfants, int Jours, decimal Admissible)> TotauxAsync(int annee)
    {
        var contenus = (await db.Releves.AsNoTracking().Where(r => r.Annee == annee && r.RemplaceParId == null).ToListAsync()).Select(LireContenu).ToList();
        return (contenus.Count, contenus.Sum(c => c.Enfants.Count), contenus.Sum(c => c.Enfants.Sum(e => e.Jours)),
            contenus.Sum(c => c.Enfants.Sum(e => e.Admissible)));
    }

    /// <summary>Sommaire 24 rempli (a signer et poster avec les copies 1) / Filled summary (to sign and mail).</summary>
    public async Task<byte[]> SommaireAsync(int annee)
    {
        var p = await db.Parametres.AsNoTracking().FirstOrDefaultAsync() ?? new Parametres();
        var (releves, enfants, jours, admissible) = await TotauxAsync(annee);
        return FormulaireProtege.Remplir(await File.ReadAllBytesAsync(options.FichierRl24S), Releve24.ChampsSommaire(new Releve24.ContenuSommaire(
            annee, await FournisseurAsync(p), releves, enfants, jours, admissible, p.Rl24Signataire, p.Telephone, Temps.Aujourdhui())));
    }
}
