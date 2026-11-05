using System.Globalization;

namespace Garderie.Web.Services;

/// <summary>
/// Releve 24 (frais de garde d'enfants) et sommaire 24 de Revenu Quebec : champs des formulaires officiels
/// (RL-24 2024-10, RL-24.S 2023-10) et numerotation des releves comme le formulaire lui-meme.
/// RL-24 slip and RL-24.S summary: official form fields and slip numbering, as the form itself does.
/// </summary>
public static class Releve24
{
    /// <summary>Champs indispensables du RL-24 / Required RL-24 fields.</summary>
    public static readonly string[] ChampsRl24 =
    [
        "annee", "codeReleve", "numSeq", "nom.1", "prenom.1", "date.1", "nbJours.1", "fraisTot.1", "fraisProv.1", "fraisFed.1",
        "nom.3", "nomPayeur", "prenomPayeur", "noPayeur", "ruePayeur", "villePayeur", "provincePayeur", "codePPayeur", "nas", "neqOuNas",
        "nomService", "noService", "rueService", "villeService", "provinceService", "codePService",
        "rep_annee", "rep_codeReleve", "rep_nom.1", "rep_fraisFed.1", "rep_nas", "rep_neqOuNas", "rep_payeur", "rep_service",
    ];

    /// <summary>Champs indispensables du RL-24.S / Required RL-24.S fields.</summary>
    public static readonly string[] ChampsRl24S =
        ["annee", "nom", "adresse_1", "cp", "neq", "no_permis", "nombre", "nb4", "nb5", "case_e", "titre", "date", "telephone"];

    /// <summary>
    /// Numero suivant, comme la fonction noSequentiel() du formulaire : 5 chiffres fixes, compteur de 3 chiffres
    /// (1 apres 599), chiffre de controle = (8 premiers chiffres) modulo 7.
    /// Next number, like the form's noSequentiel(): 5 fixed digits, 3-digit counter, check digit = first 8 mod 7.
    /// </summary>
    public static string Suivant(string actuel)
    {
        if (actuel.Length != 9 || !actuel.All(char.IsAsciiDigit)) throw new ArgumentException("Numéro séquentiel invalide.", nameof(actuel));
        var fixe = actuel[..5];
        var compteur = int.Parse(actuel[5..8], CultureInfo.InvariantCulture);
        compteur = compteur > 599 ? 1 : compteur + 1;
        var huit = fixe + compteur.ToString("D3", CultureInfo.InvariantCulture);
        return huit + (long.Parse(huit, CultureInfo.InvariantCulture) % 7).ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>Numero valide (chiffre de controle) / Valid number (check digit).</summary>
    public static bool Valide(string? n) =>
        n is { Length: 9 } && n.All(char.IsAsciiDigit) && long.Parse(n[..8], CultureInfo.InvariantCulture) % 7 == n[8] - '0';

    /// <summary>
    /// Prochain numero a utiliser : apres le dernier utilise s'il vient du meme formulaire (5 premiers chiffres),
    /// sinon apres celui inscrit dans le formulaire vierge. / Next number: after the last used one if from the
    /// same form (first 5 digits), else after the blank form's number.
    /// </summary>
    public static string Prochain(string numeroDuFormulaire, string? dernierUtilise) =>
        Suivant(dernierUtilise is { Length: 9 } d && d[..5] == numeroDuFormulaire[..5] && Valide(d) ? d : numeroDuFormulaire);

    /// <summary>Date au format du formulaire (AAAAMMJJ) / Date in the form's format.</summary>
    public static string Date(DateOnly d) => d.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

    /// <summary>Montant comme AFNumber_Format(2, 0) : 9,540.00 / Amount like AFNumber_Format(2, 0).</summary>
    public static string Montant(decimal m) => m.ToString("#,##0.00", CultureInfo.InvariantCulture);

    /// <summary>Applique un masque util.printx : 9 chiffre, A lettre, * tout, &gt; majuscules / util.printx mask.</summary>
    public static string Masque(string masque, string valeur)
    {
        var b = new System.Text.StringBuilder();
        var majuscules = false;
        var i = 0;
        foreach (var m in masque)
        {
            switch (m)
            {
                case '>': majuscules = true; continue;
                case '9':
                    while (i < valeur.Length && !char.IsAsciiDigit(valeur[i])) i++;
                    if (i < valeur.Length) b.Append(valeur[i++]);
                    break;
                case 'A':
                    while (i < valeur.Length && !char.IsAsciiLetter(valeur[i])) i++;
                    if (i < valeur.Length) b.Append(majuscules ? char.ToUpperInvariant(valeur[i++]) : valeur[i++]);
                    break;
                case '*':
                    b.Append(majuscules ? valeur[i..].ToUpperInvariant() : valeur[i..]);
                    i = valeur.Length;
                    break;
                default: b.Append(m); break;
            }
        }
        return b.ToString();
    }

    // --- Contenu d'un releve et du sommaire / Slip and summary content ---

    /// <summary>Une adresse comme sur le formulaire / An address as on the form.</summary>
    public sealed record Adresse(string? Appartement, string? Numero, string? Rue, string? Ville, string? Province, string? CodePostal)
    {
        public string Ligne1 => string.Join(" ", new[] { Numero, Rue }.Where(x => !string.IsNullOrWhiteSpace(x)))
                                + (string.IsNullOrWhiteSpace(Appartement) ? "" : $", app. {Appartement}");
        public string Ligne2 => string.Join(" ", new[] { Ville, Province?.ToUpperInvariant(), CodePostalAffiche }.Where(x => !string.IsNullOrWhiteSpace(x)));
        public string? CodePostalAffiche => CodePostal is { Length: 6 } c ? $"{c[..3]} {c[3..]}".ToUpperInvariant() : CodePostal;
    }

    public sealed record EnfantReleve(string Nom, string Prenom, DateOnly? Naissance, int Jours, decimal Total, decimal NonAdmissible)
    {
        public decimal Admissible => Total - NonAdmissible;
    }

    public sealed record Fournisseur(string Nom, string? Nom2, Adresse Adresse, string? Identifiant);

    /// <param name="Code">R original, A modifie / R original, A amended.</param>
    public sealed record ContenuReleve(int Annee, string Numero, char Code, string? NumeroOriginal,
        string PayeurNom, string PayeurPrenom, Adresse PayeurAdresse, string? PayeurNas, Fournisseur Fournisseur, IReadOnlyList<EnfantReleve> Enfants);

    public sealed record ContenuSommaire(int Annee, Fournisseur Fournisseur, int Releves, int Enfants, int Jours, decimal TotalAdmissible,
        string? Signataire, string? Telephone, DateOnly Date);

    /// <summary>Champs du RL-24 (copie 1 et copie 2 du parent) / RL-24 fields (copy 1 and the parent's copy 2).</summary>
    public static Dictionary<string, FormulaireProtege.Champ> ChampsReleve(ContenuReleve c)
    {
        if (c.Enfants.Count is < 1 or > 3) throw new ArgumentException("Un relevé 24 compte de 1 à 3 enfants.", nameof(c));
        static FormulaireProtege.Champ T(string? v, string? affiche = null) => new(v ?? "", affiche);
        var annee = c.Annee.ToString(CultureInfo.InvariantCulture);
        var f = c.Fournisseur;
        var v = new Dictionary<string, FormulaireProtege.Champ>
        {
            ["annee"] = T(annee, " " + annee), ["rep_annee"] = T(annee),
            ["codeReleve"] = c.Code == 'A' ? T("2", "      A") : T("1", "      R"), ["rep_codeReleve"] = T(c.Code.ToString()),
            ["numSeq"] = T(c.Numero),
            ["nomPayeur"] = T(c.PayeurNom), ["prenomPayeur"] = T(c.PayeurPrenom), ["appPayeur"] = T(c.PayeurAdresse.Appartement),
            ["noPayeur"] = T(c.PayeurAdresse.Numero), ["ruePayeur"] = T(c.PayeurAdresse.Rue), ["villePayeur"] = T(c.PayeurAdresse.Ville),
            ["provincePayeur"] = T(c.PayeurAdresse.Province?.ToUpperInvariant()),
            ["codePPayeur"] = T(c.PayeurAdresse.CodePostal, c.PayeurAdresse.CodePostal is null ? null : Masque(MasqueCodePostal, c.PayeurAdresse.CodePostal)),
            ["nas"] = T(c.PayeurNas, c.PayeurNas is null ? null : Masque(MasqueNas, c.PayeurNas)), ["rep_nas"] = T(c.PayeurNas),
            ["neqOuNas"] = T(f.Identifiant), ["rep_neqOuNas"] = T(f.Identifiant),
            ["nomService"] = T(f.Nom), ["nom2Service"] = T(f.Nom2), ["appService"] = T(f.Adresse.Appartement), ["noService"] = T(f.Adresse.Numero),
            ["rueService"] = T(f.Adresse.Rue), ["villeService"] = T(f.Adresse.Ville), ["provinceService"] = T(f.Adresse.Province?.ToUpperInvariant()),
            ["codePService"] = T(f.Adresse.CodePostal, f.Adresse.CodePostal is null ? null : Masque(MasqueCodePostal, f.Adresse.CodePostal)),
            ["rep_payeur"] = T($"{c.PayeurNom} {c.PayeurPrenom}\n{c.PayeurAdresse.Ligne1}\n{c.PayeurAdresse.Ligne2}"),
            ["rep_service"] = T(string.Join("\n", new[] { f.Nom, f.Nom2, f.Adresse.Ligne1, f.Adresse.Ligne2 }.Where(x => !string.IsNullOrWhiteSpace(x)))),
        };
        if (c.Code == 'A' && c.NumeroOriginal is { } o) { v["noOriginal"] = T(o); v["rep_noOriginal"] = T(o); }
        for (var i = 0; i < c.Enfants.Count; i++)
        {
            var e = c.Enfants[i];
            var k = i + 1;
            var date = e.Naissance is { } n ? Date(n) : null;
            foreach (var p in new[] { "", "rep_" })
            {
                v[$"{p}nom.{k}"] = T(e.Nom);
                v[$"{p}prenom.{k}"] = T(e.Prenom);
                v[$"{p}date.{k}"] = T(date, date is null ? null : Masque(MasqueDate, date));
                v[$"{p}nbJours.{k}"] = T(e.Jours.ToString(CultureInfo.InvariantCulture));
                v[$"{p}fraisTot.{k}"] = T(e.Total.ToString("0.00", CultureInfo.InvariantCulture), Montant(e.Total));
                v[$"{p}fraisProv.{k}"] = T(e.NonAdmissible.ToString("0.00", CultureInfo.InvariantCulture), Montant(e.NonAdmissible));
                v[$"{p}fraisFed.{k}"] = T(e.Admissible.ToString("0.00", CultureInfo.InvariantCulture), Montant(e.Admissible));
            }
        }
        return v;
    }

    /// <summary>Champs du RL-24.S / RL-24.S fields.</summary>
    public static Dictionary<string, FormulaireProtege.Champ> ChampsSommaire(ContenuSommaire s)
    {
        var id = s.Fournisseur.Identifiant ?? "";
        var adresse = string.Join(", ", new[] { s.Fournisseur.Adresse.Ligne1, string.Join(" ", new[] { s.Fournisseur.Adresse.Ville, s.Fournisseur.Adresse.Province?.ToUpperInvariant() }.Where(x => !string.IsNullOrWhiteSpace(x))) }.Where(x => x.Length > 0));
        var tel = new string((s.Telephone ?? "").Where(char.IsAsciiDigit).ToArray());
        return new()
        {
            ["annee"] = new(s.Annee.ToString(CultureInfo.InvariantCulture)),
            ["nom"] = new(s.Fournisseur.Nom), ["adresse_1"] = new(adresse), ["cp"] = new(s.Fournisseur.Adresse.CodePostal?.ToUpperInvariant() ?? ""),
            // NEQ : ligne 10 ; NAS : ligne 11 (9 cases, le formulaire en declare 8) / NEQ line 10; SIN line 11 (9 boxes)
            ["neq"] = new(id.Length == 10 ? id : ""), ["no_permis"] = new(id.Length == 9 ? id : "", null, 9),
            ["nombre"] = new(s.Releves.ToString(CultureInfo.InvariantCulture)),
            ["nb4"] = new(s.Enfants.ToString(CultureInfo.InvariantCulture)), ["nb5"] = new(s.Jours.ToString(CultureInfo.InvariantCulture)),
            ["case_e"] = new(s.TotalAdmissible.ToString("0.00", CultureInfo.InvariantCulture), Montant(s.TotalAdmissible)),
            ["titre"] = new((s.Signataire ?? "").ToUpperInvariant()), ["date"] = new(Date(s.Date), Masque("9999  99   99", Date(s.Date))),
            ["telephone"] = new(tel.Length == 10 ? tel : "", tel.Length == 10 ? Masque("999 999-9999", tel) : null),
        };
    }

    /// <summary>Masques d'affichage du RL-24 / RL-24 display masks.</summary>
    public const string MasqueDate = "      9999         99       99";
    public const string MasqueNas = "     999            999            999";
    public const string MasqueCodePostal = ">   A9A       9A9";
}
