using System.Net;
using System.Text.RegularExpressions;
using Garderie.Web.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Garderie.Tests;

/// <summary>Dossier archive (date de fin passee) en lecture seule / Archived record is read-only.</summary>
public class ArchiveTests(Appli appli) : IClassFixture<Appli>
{
    private HttpClient Client()
    {
        var c = appli.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        c.DefaultRequestHeaders.Add("X-Test-Port", "8090");
        c.DefaultRequestHeaders.Add("X-Test-Ip", "172.30.32.2");
        c.DefaultRequestHeaders.Add("X-Remote-User-Name", "rsge");
        return c;
    }

    private async Task<(int enfant, int parent)> CreerAsync(DateOnly? fin)
    {
        using var scope = appli.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GarderieDb>();
        var m = DateTime.UtcNow;
        var p = new Parent { Prenom = "Parent", Nom = "A", CreeLe = m, ModifieLe = m };
        var e = new Enfant { Prenom = "Archi", Nom = "A", DateDebut = new DateOnly(2024, 9, 1), DateFin = fin, CreeLe = m, ModifieLe = m };
        e.Parents = [new() { Parent = p, Lien = "Mere" }];
        db.Enfants.Add(e);
        await db.SaveChangesAsync();
        return (e.Id, p.Id);
    }

    private static string Jeton(string html) =>
        Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;

    private static async Task<HttpResponseMessage> Poster(HttpClient c, string pageGet, string url, Dictionary<string, string> champs)
    {
        champs["__RequestVerificationToken"] = Jeton(await c.GetStringAsync(pageGet));
        return await c.PostAsync(url, new FormUrlEncodedContent(champs));
    }

    private T Lire<T>(Func<GarderieDb, T> f)
    {
        using var scope = appli.Services.CreateScope();
        return f(scope.ServiceProvider.GetRequiredService<GarderieDb>());
    }

    [Fact]
    public async Task ModificationsRefuseesSurDossierArchive()
    {
        var (id, parent) = await CreerAsync(new DateOnly(2025, 6, 27));
        var c = Client();
        var dossier = $"/Enfants/Dossier/{id}";

        var html = await c.GetStringAsync(dossier);
        Assert.Contains("Ancien · parti le", html);
        Assert.Contains("handler=Reactiver", html);

        var r = await Poster(c, dossier, $"{dossier}?handler=Contact", new() { ["contact.Nom"] = "Intrus" });
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        await Poster(c, dossier, $"{dossier}?handler=Allergie", new() { ["allergie.Description"] = "X" });
        await Poster(c, dossier, $"/Enfants/Horaire/{id}", new() { ["DateEffective"] = "2026-11-01", ["Cases"] = "1-M" });
        await Poster(c, dossier, $"/Enfants/Edition?id={id}", new() { ["Donnees.Prenom"] = "Change", ["Donnees.Nom"] = "A", ["Donnees.DateDebut"] = "2024-09-01" });
        await Poster(c, dossier, $"/Enfants/Parent?enfantId={id}&parentId={parent}", new() { ["Donnees.Prenom"] = "Change", ["Donnees.Nom"] = "A", ["Donnees.Lien"] = "Mere" });

        Assert.Equal(0, Lire(db => db.Contacts.Count(x => x.EnfantId == id)));
        Assert.Equal(0, Lire(db => db.Allergies.Count(x => x.EnfantId == id)));
        Assert.Equal(0, Lire(db => db.Horaires.Count(x => x.EnfantId == id)));
        Assert.Equal("Archi", Lire(db => db.Enfants.Single(x => x.Id == id).Prenom));
        Assert.Equal("Parent", Lire(db => db.Parents.Single(x => x.Id == parent).Prenom));
    }

    [Fact]
    public async Task NasToujoursConsultable()
    {
        var (id, parent) = await CreerAsync(new DateOnly(2025, 6, 27));
        var c = Client();
        var page = $"/Enfants/Parent?enfantId={id}&parentId={parent}";
        var r = await Poster(c, page, page + "&handler=AfficherNas", new());
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Contains("Dossier archivé : lecture seule", await r.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task BoutonArchiver()
    {
        var (id, _) = await CreerAsync(null);
        var c = Client();
        var dossier = $"/Enfants/Dossier/{id}";
        Assert.Contains("Archiver le dossier", await c.GetStringAsync(dossier));
        await Poster(c, dossier, $"{dossier}?handler=Archiver", new() { ["dernierJour"] = "2024-08-01" });   // avant le debut : refuse
        Assert.Null(Lire(db => db.Enfants.Single(x => x.Id == id).DateFin));
        await Poster(c, dossier, $"{dossier}?handler=Archiver", new() { ["dernierJour"] = "2025-06-27" });
        Assert.Equal(new DateOnly(2025, 6, 27), Lire(db => db.Enfants.Single(x => x.Id == id).DateFin));
        var page = await c.GetStringAsync(dossier);
        Assert.Contains("Dossier archivé", page);   // lecture seule / read-only
        Assert.DoesNotContain("Archiver le dossier", page);
        Assert.True(Lire(db => db.Journal.Any(j => j.Action == "enfant.archiver" && j.EntiteId == id)));
    }

    [Fact]
    public async Task ReactiverPuisModifier()
    {
        var (id, _) = await CreerAsync(new DateOnly(2025, 6, 27));
        var c = Client();
        var dossier = $"/Enfants/Dossier/{id}";
        await Poster(c, dossier, $"{dossier}?handler=Reactiver", new());
        Assert.Null(Lire(db => db.Enfants.Single(x => x.Id == id).DateFin));
        Assert.True(Lire(db => db.Journal.Any(j => j.Action == "enfant.reactiver" && j.EntiteId == id)));

        await Poster(c, dossier, $"{dossier}?handler=Contact", new() { ["contact.Nom"] = "Grand-papa" });
        Assert.Equal(1, Lire(db => db.Contacts.Count(x => x.EnfantId == id)));
    }

    [Fact]
    public async Task DateDeFinFutureResteModifiable()
    {
        var (id, _) = await CreerAsync(DateOnly.FromDateTime(DateTime.Today.AddDays(30)));
        var c = Client();
        var dossier = $"/Enfants/Dossier/{id}";
        Assert.DoesNotContain("Ancien · parti le", await c.GetStringAsync(dossier));
        await Poster(c, dossier, $"{dossier}?handler=Contact", new() { ["contact.Nom"] = "Tante" });
        Assert.Equal(1, Lire(db => db.Contacts.Count(x => x.EnfantId == id)));
    }

    [Fact]
    public void RegleAncien()
    {
        var j = new DateOnly(2026, 11, 4);
        Assert.False(Enfant.EstAncienSelon(null, j));
        Assert.False(Enfant.EstAncienSelon(j, j));               // dernier jour : encore inscrit / last day: still enrolled
        Assert.True(Enfant.EstAncienSelon(j.AddDays(-1), j));
    }
}
