using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Garderie.Web.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Garderie.Tests;

/// <summary>Presences a la semaine et photos / Weekly attendance and photos.</summary>
public class SemaineEtPhotosTests(Appli appli) : IClassFixture<Appli>
{
    // Plus petit JPEG reconnu (signature seulement) / Smallest recognised JPEG (signature only)
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, (byte)'J', (byte)'F', (byte)'I', (byte)'F', 0, 1];

    private HttpClient Client()
    {
        var c = appli.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        c.DefaultRequestHeaders.Add("X-Test-Port", "8090");
        c.DefaultRequestHeaders.Add("X-Test-Ip", "172.30.32.2");
        c.DefaultRequestHeaders.Add("X-Remote-User-Name", "rsge");
        return c;
    }

    private static string Jeton(string html) =>
        Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;

    private T Db<T>(Func<GarderieDb, T> f)
    {
        using var scope = appli.Services.CreateScope();
        return f(scope.ServiceProvider.GetRequiredService<GarderieDb>());
    }

    private int CreerEnfant(DateOnly debut, DateOnly? fin = null) => Db(db =>
    {
        var m = DateTime.UtcNow;
        var p = new Parent { Prenom = "Maman", Nom = "S", CreeLe = m, ModifieLe = m };
        var e = new Enfant { Prenom = "Semaine", Nom = "S", DateDebut = debut, DateFin = fin, CreeLe = m, ModifieLe = m,
            Contacts = [new() { Nom = "Grand-papa S" }] };
        e.Parents = [new() { Parent = p, Lien = "Mere" }];
        db.Enfants.Add(e);
        db.SaveChanges();
        return e.Id;
    });

    [Fact]
    public async Task SemaineProposePEtFermeture()
    {
        // Semaine fixe dans le passe : 2 au 6 mars 2026, fermeture le jeudi 5 / Fixed past week, closed Thursday
        var id = CreerEnfant(new DateOnly(2026, 3, 3));   // inscrit a partir du mardi / enrolled from Tuesday
        Db(db => { db.Fermetures.Add(new JourFermeture { Date = new DateOnly(2026, 3, 5) }); return db.SaveChanges(); });

        var html = await Client().GetStringAsync("/Presences?semaine=2026-03-04");
        Assert.Contains("2 au 6 mars 2026", html);
        var ligne = html[html.IndexOf($"value=\"{id}\"", StringComparison.Ordinal)..];
        // Mardi, mercredi, vendredi = P propose (matin et PM) ; jeudi = F ; lundi : pas de case (pas encore inscrit)
        Assert.Matches("value=\"2026-03-03\" />\\s*<input[^>]*value=\"Matin\" />\\s*<select[^>]*class=\"code code-p propose\"", ligne);
        Assert.Matches("value=\"2026-03-03\" />\\s*<input[^>]*value=\"Pm\" />\\s*<select[^>]*class=\"code code-p propose\"", ligne);
        Assert.Matches("value=\"2026-03-05\" />\\s*<input[^>]*value=\"Pm\" />\\s*<select[^>]*class=\"code code-f propose\"", ligne);
        Assert.DoesNotContain("value=\"2026-03-02\"", ligne[..ligne.IndexOf("</section>", StringComparison.Ordinal)]);
    }

    [Fact]
    public async Task EnregistrerLaSemaine()
    {
        var id = CreerEnfant(new DateOnly(2026, 4, 7));    // inscrit a partir du mardi 7 avril
        var c = Client();
        var jeton = Jeton(await c.GetStringAsync("/Presences?semaine=2026-04-06"));
        var champs = new List<KeyValuePair<string, string>>
        {
            new("__RequestVerificationToken", jeton),
            new("codes[0].EnfantId", $"{id}"), new("codes[0].Date", "2026-04-07"), new("codes[0].Demi", "Matin"), new("codes[0].Code", "P"),
            new("codes[1].EnfantId", $"{id}"), new("codes[1].Date", "2026-04-07"), new("codes[1].Demi", "Pm"), new("codes[1].Code", "P"),
            new("codes[2].EnfantId", $"{id}"), new("codes[2].Date", "2026-04-08"), new("codes[2].Demi", "Pm"), new("codes[2].Code", "A 1/2"),
            new("codes[3].EnfantId", $"{id}"), new("codes[3].Date", "2026-04-06"), new("codes[3].Demi", "Matin"), new("codes[3].Code", "P"),   // pas inscrit : ignore
            new("codes[4].EnfantId", $"{id}"), new("codes[4].Date", "2026-04-14"), new("codes[4].Demi", "Matin"), new("codes[4].Code", "P"),   // autre semaine : ignore
            new("codes[5].EnfantId", $"{id}"), new("codes[5].Date", "2026-04-09"), new("codes[5].Demi", "Matin"), new("codes[5].Code", "X"),   // code invalide : ignore
        };
        var r = await c.PostAsync("/Presences?semaine=2026-04-06", new FormUrlEncodedContent(champs));
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);

        var codes = Db(db => db.Presences.Where(p => p.EnfantId == id).OrderBy(p => p.Date).ThenBy(p => p.Demi)
            .AsEnumerable().Select(p => $"{p.Date:MM-dd} {p.Demi}={p.Code}").ToList());
        Assert.Equal(["04-07 Matin=P", "04-07 Pm=P", "04-08 Pm=A 1/2"], codes);

        var html = await c.GetStringAsync("/Presences?semaine=2026-04-06");
        Assert.Contains("class=\"code code-a enregistre\"", html);
    }

    private async Task<HttpResponseMessage> EnvoyerPhoto(HttpClient c, int id, string handler, string? cle, int? valeur, byte[] contenu)
    {
        var jeton = Jeton(await c.GetStringAsync($"/Enfants/Dossier/{id}"));
        var form = new MultipartFormDataContent { { new StringContent(jeton), "__RequestVerificationToken" } };
        if (cle is not null) form.Add(new StringContent($"{valeur}"), cle);
        var fichier = new ByteArrayContent(contenu);
        fichier.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(fichier, "photo", "photo.jpg");
        return await c.PostAsync($"/Enfants/Dossier/{id}?handler={handler}", form);
    }

    [Fact]
    public async Task PhotosEnfantParentContact()
    {
        var id = CreerEnfant(new DateOnly(2026, 1, 5));
        var (parent, contact) = Db(db => (db.EnfantParents.Single(x => x.EnfantId == id).ParentId, db.Contacts.Single(x => x.EnfantId == id).Id));
        var c = Client();

        await EnvoyerPhoto(c, id, "Photo", null, null, Jpeg);
        await EnvoyerPhoto(c, id, "PhotoParent", "parentId", parent, Jpeg);
        await EnvoyerPhoto(c, id, "PhotoContact", "contactId", contact, Jpeg);

        var enfant = Db(db => db.Enfants.Single(x => x.Id == id));
        Assert.NotNull(enfant.PhotoDocumentId);
        var p = Db(db => db.Parents.Single(x => x.Id == parent).PhotoFichier);
        Assert.NotNull(p);
        Assert.NotNull(Db(db => db.Contacts.Single(x => x.Id == contact).PhotoFichier));

        // Chiffree sur disque, identique a la lecture / Encrypted on disk, identical when read
        var surDisque = await File.ReadAllBytesAsync(Path.Combine(appli.Dossier, "docs", p!));
        Assert.NotEqual(Jpeg, surDisque);
        var lu = await c.GetAsync($"/Enfants/Dossier/{id}?handler=PhotoParent&parentId={parent}");
        Assert.Equal("image/jpeg", lu.Content.Headers.ContentType?.MediaType);
        Assert.Equal(Jpeg, await lu.Content.ReadAsByteArrayAsync());

        // Remplacer la photo efface l'ancien fichier / Replacing deletes the old file
        await EnvoyerPhoto(c, id, "PhotoParent", "parentId", parent, Jpeg);
        Assert.False(File.Exists(Path.Combine(appli.Dossier, "docs", p!)));
        Assert.Single(Db(db => db.Documents.Where(d => d.EnfantId == id && d.Type == TypeDocument.Photo).ToList()));
    }

    [Fact]
    public async Task PhotoRefuseeSiPasUneImage()
    {
        var id = CreerEnfant(new DateOnly(2026, 1, 5));
        var parent = Db(db => db.EnfantParents.Single(x => x.EnfantId == id).ParentId);
        await EnvoyerPhoto(Client(), id, "PhotoParent", "parentId", parent, "%PDF-1.4 pas une photo"u8.ToArray());
        Assert.Null(Db(db => db.Parents.Single(x => x.Id == parent).PhotoFichier));
    }

    [Fact]
    public async Task PhotoDUnAutreEnfantInaccessible()
    {
        var a = CreerEnfant(new DateOnly(2026, 1, 5));
        var b = CreerEnfant(new DateOnly(2026, 1, 5));
        var parentDeB = Db(db => db.EnfantParents.Single(x => x.EnfantId == b).ParentId);
        var r = await EnvoyerPhoto(Client(), a, "PhotoParent", "parentId", parentDeB, Jpeg);
        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
    }

    [Fact]
    public async Task PhotoBloqueeSurDossierArchive()
    {
        var id = CreerEnfant(new DateOnly(2024, 1, 8), new DateOnly(2025, 6, 27));
        await EnvoyerPhoto(Client(), id, "Photo", null, null, Jpeg);
        Assert.Null(Db(db => db.Enfants.Single(x => x.Id == id).PhotoDocumentId));
    }
}
