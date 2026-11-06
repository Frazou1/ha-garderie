using System.Text.RegularExpressions;
using Garderie.Web.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Garderie.Tests;

/// <summary>Cellulaire de l'autre parent (papa) dans la fiche du parent / Other parent's cell phone.</summary>
public class AutreParentTests(Appli appli) : IClassFixture<Appli>
{
    [Fact]
    public async Task CellulairePapaEnregistreEtAffiche()
    {
        int enfant, parent;
        using (var scope = appli.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GarderieDb>();
            var m = DateTime.UtcNow;
            var p = new Parent { Prenom = "Mama", Nom = "Test", CreeLe = m, ModifieLe = m };
            var e = new Enfant { Prenom = "Bebe", Nom = "Test", DateDebut = new DateOnly(2025, 9, 1), CreeLe = m, ModifieLe = m };
            e.Parents = [new() { Parent = p, Lien = "Mère" }];
            db.Enfants.Add(e);
            await db.SaveChangesAsync();
            (enfant, parent) = (e.Id, p.Id);
        }
        var c = appli.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        c.DefaultRequestHeaders.Add("X-Test-Port", "8090");
        c.DefaultRequestHeaders.Add("X-Test-Ip", "172.30.32.2");
        c.DefaultRequestHeaders.Add("X-Remote-User-Name", "rsge");
        var page = $"/Enfants/Parent?enfantId={enfant}&parentId={parent}";
        var html = await c.GetStringAsync(page);
        Assert.Contains("Cellulaire papa", html);
        var jeton = Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        await c.PostAsync(page, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = jeton, ["Donnees.Prenom"] = "Mama", ["Donnees.Nom"] = "Test", ["Donnees.Lien"] = "Mère",
            ["Donnees.TelephoneAutreParent"] = "418-555-0199",
        }));
        using (var scope = appli.Services.CreateScope())
            Assert.Equal("418-555-0199", scope.ServiceProvider.GetRequiredService<GarderieDb>().Parents.Single(x => x.Id == parent).TelephoneAutreParent);
        Assert.Contains("papa : <a href=\"tel:418-555-0199\"", await c.GetStringAsync($"/Enfants/Dossier/{enfant}"));
    }
}
