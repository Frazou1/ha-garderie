using Garderie.Web.Data;
using Garderie.Web.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Garderie.Tests;

/// <summary>Periodes signees sur papier : aucune fiche electronique / Paper-signed periods.</summary>
public class FichesPapierTests(Appli appli) : IClassFixture<Appli>
{
    [Fact]
    public async Task PeriodesAvantLaDateSontPapier()
    {
        using (var scope = appli.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GarderieDb>();
            var p = await db.Parametres.FirstOrDefaultAsync();
            if (p is null) { p = new Parametres(); db.Parametres.Add(p); }
            p.FichesPapierJusquau = new DateOnly(2026, 11, 1);
            var m = DateTime.UtcNow;
            db.Enfants.Add(new Enfant { Prenom = "Papier", Nom = "Test", DateDebut = new DateOnly(2025, 9, 1), CreeLe = m, ModifieLe = m });
            await db.SaveChangesAsync();
        }
        var c = appli.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        c.DefaultRequestHeaders.Add("X-Test-Port", "8090");
        c.DefaultRequestHeaders.Add("X-Test-Ip", "172.30.32.2");
        c.DefaultRequestHeaders.Add("X-Remote-User-Name", "rsge");
        var avant = System.Net.WebUtility.HtmlDecode(await c.GetStringAsync("/Fiches?periode=2026-10-19"));
        Assert.Contains("Période signée sur papier", avant);
        Assert.DoesNotContain("Papier Test", avant);
        Assert.DoesNotContain("Période signée sur papier", System.Net.WebUtility.HtmlDecode(await c.GetStringAsync("/Fiches?periode=2026-11-02")));

        using var s = appli.Services.CreateScope();
        var fiches = s.ServiceProvider.GetRequiredService<FichesService>();
        var id = s.ServiceProvider.GetRequiredService<GarderieDb>().Enfants.First(e => e.Prenom == "Papier").Id;
        var erreur = await fiches.SignerRsgeAsync(id, new DateOnly(2026, 10, 19), FichesService.LireSignature(ModeleTest.Png)!, "rsge");
        Assert.Contains("papier", erreur ?? "");
    }
}
