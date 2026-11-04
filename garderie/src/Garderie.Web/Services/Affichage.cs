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

    public static string Horodatage(DateTime utc) => Temps.VersLocal(utc).ToString("yyyy-MM-dd HH:mm");

    /// <summary>Resume d'un horaire (ex. « Lun–Ven journée ») / Schedule summary.</summary>
    public static string ResumeJour(HoraireType h) =>
        h.Matin && h.Pm ? "journée" : h.Matin ? "matin" : h.Pm ? "après-midi" : "—";
}
