using System.Net;
using System.Text.RegularExpressions;
using Garderie.Web.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Garderie.Tests;

/// <summary>Suppression complete d'un dossier d'enfant / Full child record deletion.</summary>
public class SuppressionTests(Appli appli) : IClassFixture<Appli>
{
    private HttpClient Client()
    {
        var c = appli.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        c.DefaultRequestHeaders.Add("X-Test-Port", "8090");
        c.DefaultRequestHeaders.Add("X-Test-Ip", "172.30.32.2");
        c.DefaultRequestHeaders.Add("X-Remote-User-Name", "rsge");
        return c;
    }

    /// <summary>Deux enfants, un parent partage, un parent seul, un document / Two children, shared and lone parent, one document.</summary>
    private async Task<(int enfant, int frere, int parentSeul, int parentPartage, string fichier)> CreerAsync()
    {
        using var scope = appli.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GarderieDb>();
        var maintenant = DateTime.UtcNow;
        var partage = new Parent { Prenom = "Partage", Nom = "T", CreeLe = maintenant, ModifieLe = maintenant };
        var seul = new Parent { Prenom = "Seul", Nom = "T", NasChiffre = [1, 2, 3], CreeLe = maintenant, ModifieLe = maintenant };
        var fichier = Guid.NewGuid().ToString("N") + ".bin";
        var e = new Enfant
        {
            Prenom = "Testo", Nom = "T", DateDebut = new DateOnly(2026, 9, 1), CreeLe = maintenant, ModifieLe = maintenant,
            Contacts = [new() { Nom = "Grand-maman" }],
            Allergies = [new() { Description = "Arachides" }],
            Medicaments = [new() { Nom = "Acetaminophene" }],
            Horaires = [new() { DateEffective = new DateOnly(2026, 9, 1), Jour = DayOfWeek.Monday, Matin = true, Pm = true }],
            Documents = [new() { NomOriginal = "a.pdf", TypeContenu = "application/pdf", Fichier = fichier, TeleverseLe = maintenant }],
        };
        var frere = new Enfant { Prenom = "Frere", Nom = "T", DateDebut = new DateOnly(2026, 9, 1), CreeLe = maintenant, ModifieLe = maintenant };
        e.Parents = [new() { Parent = partage, Lien = "Mere" }, new() { Parent = seul, Lien = "Pere" }];
        frere.Parents = [new() { Parent = partage, Lien = "Mere" }];
        db.AddRange(e, frere);
        await db.SaveChangesAsync();
        db.Presences.Add(new Presence { EnfantId = e.Id, Date = new DateOnly(2026, 11, 2), Code = "P", ModifieLe = maintenant });
        await db.SaveChangesAsync();

        var docs = Path.Combine(appli.Dossier, "docs");
        Directory.CreateDirectory(docs);
        await File.WriteAllBytesAsync(Path.Combine(docs, fichier), [9, 9, 9]);
        return (e.Id, frere.Id, seul.Id, partage.Id, fichier);
    }

    private static async Task<HttpResponseMessage> PosterAsync(HttpClient c, int id, string? confirmation)
    {
        var page = await c.GetStringAsync($"/Enfants/Dossier/{id}");
        var jeton = Regex.Match(page, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        var champs = new Dictionary<string, string> { ["__RequestVerificationToken"] = jeton };
        if (confirmation is not null) champs["confirmation"] = confirmation;
        return await c.PostAsync($"/Enfants/Dossier/{id}?handler=SupprimerEnfant", new FormUrlEncodedContent(champs));
    }

    [Fact]
    public async Task SupprimeToutLeDossierEtLesParentsOrphelins()
    {
        var (id, frere, seul, partage, fichier) = await CreerAsync();
        var r = await PosterAsync(Client(), id, " testo ");
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);

        using var scope = appli.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GarderieDb>();
        Assert.False(await db.Enfants.AnyAsync(x => x.Id == id));
        Assert.False(await db.Contacts.AnyAsync(x => x.EnfantId == id));
        Assert.False(await db.Allergies.AnyAsync(x => x.EnfantId == id));
        Assert.False(await db.Medicaments.AnyAsync(x => x.EnfantId == id));
        Assert.False(await db.Horaires.AnyAsync(x => x.EnfantId == id));
        Assert.False(await db.Documents.AnyAsync(x => x.EnfantId == id));
        Assert.False(await db.Presences.AnyAsync(x => x.EnfantId == id));
        Assert.False(await db.Parents.AnyAsync(x => x.Id == seul));      // orphelin : supprime / orphan: deleted
        Assert.True(await db.Parents.AnyAsync(x => x.Id == partage));    // garde pour le frere / kept for sibling
        Assert.True(await db.Enfants.AnyAsync(x => x.Id == frere));
        Assert.False(File.Exists(Path.Combine(appli.Dossier, "docs", fichier)));
        Assert.True(await db.Journal.AnyAsync(j => j.Action == "enfant.supprimer" && j.EntiteId == id && j.Utilisateur == "rsge"));
    }

    [Theory]
    [InlineData("Autre")]
    [InlineData("")]
    [InlineData(null)]
    public async Task RefuseSansLeBonPrenom(string? confirmation)
    {
        var (id, _, _, _, fichier) = await CreerAsync();
        await PosterAsync(Client(), id, confirmation);

        using var scope = appli.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GarderieDb>();
        Assert.True(await db.Enfants.AnyAsync(x => x.Id == id));
        Assert.True(File.Exists(Path.Combine(appli.Dossier, "docs", fichier)));
    }

    [Fact]
    public async Task InaccessibleSurLePortPublic()
    {
        var c = appli.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var req = new HttpRequestMessage(HttpMethod.Post, "/garderie/Enfants/Dossier/1?handler=SupprimerEnfant");
        req.Headers.Add("X-Test-Port", "8080");
        req.Headers.Add("X-Remote-User-Name", "rsge");
        Assert.Equal(HttpStatusCode.NotFound, (await c.SendAsync(req)).StatusCode);
    }
}
