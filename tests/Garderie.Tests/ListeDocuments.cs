using System.Text.RegularExpressions;
using Garderie.Web.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Garderie.Tests;

/// <summary>Liste simple des documents et renommage / Simple document list and renaming.</summary>
public class ListeDocumentsTests(Appli appli) : IClassFixture<Appli>
{
    [Fact]
    public async Task RenommerEtNote()
    {
        int id, doc;
        using (var scope = appli.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GarderieDb>();
            var m = DateTime.UtcNow;
            var e = new Enfant { Prenom = "Doc", Nom = "Test", DateDebut = new DateOnly(2025, 9, 1), CreeLe = m, ModifieLe = m };
            db.Enfants.Add(e);
            await db.SaveChangesAsync();
            var d = new Document { EnfantId = e.Id, Type = TypeDocument.Contrat, NomOriginal = "Contrat 2026 - Doc.pdf", TypeContenu = "application/pdf", Fichier = "x.bin", Note = "885 Ko · vieux", TeleverseLe = m };
            db.Documents.Add(d);
            await db.SaveChangesAsync();
            (id, doc) = (e.Id, d.Id);
        }
        var c = appli.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        c.DefaultRequestHeaders.Add("X-Test-Port", "8090");
        c.DefaultRequestHeaders.Add("X-Test-Ip", "172.30.32.2");
        c.DefaultRequestHeaders.Add("X-Remote-User-Name", "rsge");
        var html = await c.GetStringAsync($"/Enfants/Dossier/{id}");
        Assert.Contains("class=\"doc-nom\"", html);
        Assert.Contains("+ Ajouter un document", html);
        var jeton = Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        await c.PostAsync($"/Enfants/Dossier/{id}?handler=ModifierDocument", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = jeton, ["documentId"] = $"{doc}", ["nom"] = "Contrat 2025 / Doc", ["note"] = "Renouvellement",
        }));
        using var s = appli.Services.CreateScope();
        var d2 = s.ServiceProvider.GetRequiredService<GarderieDb>().Documents.Single(x => x.Id == doc);
        Assert.StartsWith("Contrat 2025", d2.NomOriginal);   // « / » retire, extension gardee / slash removed, extension kept
        Assert.EndsWith("Doc.pdf", d2.NomOriginal);
        Assert.DoesNotContain("/", d2.NomOriginal);
        Assert.Equal("Renouvellement", d2.Note);
    }
}
