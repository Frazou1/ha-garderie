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

    /// <summary>Masques d'affichage du RL-24 / RL-24 display masks.</summary>
    public const string MasqueDate = "      9999         99       99";
    public const string MasqueNas = "     999            999            999";
    public const string MasqueCodePostal = ">   A9A       9A9";
}
