using System.Text.RegularExpressions;
using Garderie.Web.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Garderie.Tests;

/// <summary>Heures d'arrivee et de depart dans l'horaire / Arrival and departure hours in the schedule.</summary>
public class HoraireHeuresTests(Appli appli) : IClassFixture<Appli>
{
    [Fact]
    public async Task HeuresEnregistrees()
    {
        int id;
        using (var scope = appli.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GarderieDb>();
            var m = DateTime.UtcNow;
            var e = new Enfant { Prenom = "Heure", Nom = "Test", DateDebut = new DateOnly(2025, 9, 1), CreeLe = m, ModifieLe = m };
            db.Enfants.Add(e);
            await db.SaveChangesAsync();
            id = e.Id;
        }
        var c = appli.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        c.DefaultRequestHeaders.Add("X-Test-Port", "8090");
        c.DefaultRequestHeaders.Add("X-Test-Ip", "172.30.32.2");
        c.DefaultRequestHeaders.Add("X-Remote-User-Name", "rsge");
        var html = await c.GetStringAsync($"/Enfants/Horaire/{id}");
        Assert.Contains("name=\"Arrivees[1]\"", html);
        var jeton = Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        await c.PostAsync($"/Enfants/Horaire/{id}", new FormUrlEncodedContent(new List<KeyValuePair<string, string>>
        {
            new("__RequestVerificationToken", jeton), new("DateEffective", "2026-01-01"),
            new("Cases", "1-M"), new("Cases", "1-P"), new("Cases", "2-M"),
            new("Arrivees[1]", "08:00"), new("Departs[1]", "16:15"),
            new("Arrivees[2]", "10:00"), new("Departs[2]", "09:00"),   // incoherent : ignore
            new("Arrivees[6]", "08:00"), new("Departs[6]", "12:00"),   // jour non coche : ignore
        }));
        using var s = appli.Services.CreateScope();
        var h = s.ServiceProvider.GetRequiredService<GarderieDb>().Horaires.Where(x => x.EnfantId == id).ToList();
        Assert.Equal((new TimeOnly(8, 0), new TimeOnly(16, 15)), h.Where(x => x.Jour == DayOfWeek.Monday).Select(x => (x.Arrivee, x.Depart)).Single());
        Assert.All(h.Where(x => x.Jour != DayOfWeek.Monday), x => Assert.Null(x.Arrivee));
        Assert.Contains("value=\"08:00\"", await c.GetStringAsync($"/Enfants/Horaire/{id}?version=2026-01-01"));
    }
}
