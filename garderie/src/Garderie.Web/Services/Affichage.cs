using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Garderie.Web.Data;

namespace Garderie.Web.Services;

/// <summary>Libelles francais pour l'affichage / French display labels.</summary>
public static class Affichage
{
    public static readonly DayOfWeek[] JoursSemaine =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday];

    public static string Jour(DayOfWeek j) => j switch
    {
        DayOfWeek.Monday => "Lundi",
        DayOfWeek.Tuesday => "Mardi",
        DayOfWeek.Wednesday => "Mercredi",
        DayOfWeek.Thursday => "Jeudi",
        DayOfWeek.Friday => "Vendredi",
        DayOfWeek.Saturday => "Samedi",
        _ => "Dimanche",
    };

    public static string Nom<T>(T valeur) where T : struct, Enum =>
        typeof(T).GetField(valeur.ToString())?.GetCustomAttribute<DisplayAttribute>()?.Name ?? valeur.ToString();

    public static string Gravite(Gravite g) => g switch
    {
        Data.Gravite.Legere => "Légère",
        Data.Gravite.Severe => "Sévère",
        _ => "Modérée",
    };

    public static string Sexe(Sexe? s) => s switch
    {
        Data.Sexe.F => "Fille",
        Data.Sexe.M => "Garçon",
        Data.Sexe.X => "Autre",
        _ => "—",
    };

    public static string Taille(long octets) =>
        octets < 1024 ? $"{octets} o" : octets < 1024 * 1024 ? $"{octets / 1024.0:0} Ko" : $"{octets / 1024.0 / 1024.0:0.0} Mo";

    public static string Date(DateOnly? d) => d?.ToString("d MMMM yyyy") ?? "—";

    /// <summary>Abreviation du jour (Lun, Mar...) / Short day name.</summary>
    public static string JourCourt(DayOfWeek j) => Jour(j)[..3];

    /// <summary>Periode d'une semaine (ex. « 2 au 6 novembre 2026 ») / Week span.</summary>
    public static string Periode(DateOnly debut, DateOnly fin) =>
        debut.Month == fin.Month ? $"{debut.Day} au {fin:d MMMM yyyy}" : $"{debut:d MMMM} au {fin:d MMMM yyyy}";

    /// <summary>Classe CSS d'un code (couleur) / CSS class of a code (colour).</summary>
    public static string ClasseCode(string? code) => (code ?? "") switch
    {
        "" => "code-vide",
        "P" or "P 1/2" => "code-p",
        "A" or "A 1/2" => "code-a",
        "F" or "F 1/2" => "code-f",
        "R" or "R 1/2" => "code-r",
        _ => "code-autre",
    };

    /// <summary>Initiales pour l'avatar sans photo / Initials for the avatar without photo.</summary>
    public static string Initiales(string prenom, string nom) =>
        $"{(prenom.Length > 0 ? char.ToUpper(prenom[0]) : ' ')}{(nom.Length > 0 ? char.ToUpper(nom[0]) : ' ')}".Trim();

    /// <summary>Couleur pastel stable selon l'id (classe av-0 a av-5) / Stable pastel colour.</summary>
    public static string ClasseAvatar(int graine) => $"av-{Math.Abs(graine) % 6}";

    public static string Horodatage(DateTime utc) => Temps.VersLocal(utc).ToString("yyyy-MM-dd HH:mm");

    /// <summary>Resume d'un horaire (ex. « Lun–Ven journée ») / Schedule summary.</summary>
    public static string ResumeJour(HoraireType h) =>
        h.Matin && h.Pm ? "journée" : h.Matin ? "matin" : h.Pm ? "après-midi" : "—";
}
