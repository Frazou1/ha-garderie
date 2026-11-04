namespace Garderie.Web.Data;

/// <summary>
/// Codes de la fiche d'assiduite FO-0310 (06-2025), valeurs exactes des listes du PDF.
/// Attendance codes of form FO-0310, exact values of the PDF list fields.
/// </summary>
public static class CodesPresence
{
    public sealed record Code(string Valeur, string Libelle, bool Courant);

    public static readonly IReadOnlyList<Code> Tous =
    [
        new("P", "Présence 1 jour", true),
        new("P 1/2", "Présence ½ jour", true),
        new("A", "Absence 1 jour", true),
        new("A 1/2", "Absence ½ jour", true),
        new("R", "Enfant remplaçant 1 jour", false),
        new("R 1/2", "Enfant remplaçant ½ jour", false),
        new("F", "1 jour de fermeture non subventionné", true),
        new("F 1/2", "½ jour de fermeture non subventionné", false),
        new("AN", "Journée non déterminée d'absence de prestation de services subventionnée (APSS)", false),
        new("AD", "Journée prédéterminée APSS", false),
        new("L", "Jour de libération pour activités associatives", false),
        new("S", "Jour de suspension (DPJ / art. 77.1 RSGEE)", false),
        new("S 1/2", "½ jour de suspension (DPJ / art. 77.1 RSGEE)", false),
    ];

    public static bool EstValide(string? code) => string.IsNullOrEmpty(code) || Tous.Any(c => c.Valeur == code);

    public static string Libelle(string? code) => Tous.FirstOrDefault(c => c.Valeur == code)?.Libelle ?? "";

    /// <summary>Affichage court (½ au lieu de 1/2) / Short display.</summary>
    public static string Court(string? code) => (code ?? "").Replace(" 1/2", "½");
}
