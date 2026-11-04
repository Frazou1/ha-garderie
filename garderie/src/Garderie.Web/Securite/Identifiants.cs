using System.Text.RegularExpressions;

namespace Garderie.Web.Securite;

/// <summary>
/// Validation et masquage du NAS (Canada) et du NAM (RAMQ).
/// Validation and masking of the SIN (Canada) and the RAMQ health insurance number.
/// </summary>
public static partial class Identifiants
{
    [GeneratedRegex(@"[\s\-]")]
    private static partial Regex Separateurs();

    [GeneratedRegex(@"^[A-Z]{4}\d{8}$")]
    private static partial Regex FormatNam();

    /// <summary>NAS normalise (9 chiffres) ou null s'il est invalide (cle de Luhn). / Normalized SIN or null.</summary>
    public static string? NormaliserNas(string? saisie)
    {
        if (string.IsNullOrWhiteSpace(saisie)) return null;
        var n = Separateurs().Replace(saisie, "");
        if (n.Length != 9 || !n.All(char.IsAsciiDigit) || n[0] == '0') return null;
        var somme = 0;
        for (var i = 0; i < 9; i++)
        {
            var c = n[i] - '0';
            if (i % 2 == 1) { c *= 2; if (c > 9) c -= 9; }
            somme += c;
        }
        return somme % 10 == 0 ? n : null;
    }

    /// <summary>NAM normalise (AAAA99999999) ou null / Normalized RAMQ number or null.</summary>
    public static string? NormaliserNam(string? saisie)
    {
        if (string.IsNullOrWhiteSpace(saisie)) return null;
        var n = Separateurs().Replace(saisie, "").ToUpperInvariant();
        return FormatNam().IsMatch(n) ? n : null;
    }

    public static string FormaterNas(string nas) => $"{nas[..3]} {nas[3..6]} {nas[6..]}";
    public static string FormaterNam(string nam) => $"{nam[..4]} {nam[4..8]} {nam[8..]}";

    /// <summary>Affichage masque : *** *** 123 / Masked display.</summary>
    public static string MasquerNas(string nas) => $"*** *** {nas[6..]}";
    public static string MasquerNam(string nam) => $"{nam[..4]} **** **{nam[10..]}";
}
