using System.Text.RegularExpressions;
using Garderie.Web.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Garderie.Tests;

/// <summary>
/// Signature groupee : la RSGE signe plusieurs fiches d'un coup ; un parent recoit un seul lien pour ses enfants.
/// Batch signing: the RSGE signs several sheets at once; a parent gets one link for all their children.
/// </summary>
public class FratrieTests(Appli appli) : IClassFixture<Appli>
{
    private static readonly DateOnly Debut = new(2026, 3, 23);   // periode (ancre 19 oct.) : 23 mars au 5 avril 2026

    private HttpClient Admin()
    {
        var c = appli.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        c.DefaultRequestHeaders.Add("X-Test-Port", "8090");
        c.DefaultRequestHeaders.Add("X-Test-Ip", "172.30.32.2");
        c.DefaultRequestHeaders.Add("X-Remote-User-Name", "rsge");
        return c;
    }

    private HttpClient Parent()
    {
        var c = appli.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        c.DefaultRequestHeaders.Add("X-Test-Port", "8080");
        c.DefaultRequestHeaders.Add("X-Test-Ip", "203.0.113.9");
        return c;
    }

    private static string Jeton(string html) =>
        Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;

    private T Db<T>(Func<GarderieDb, T> f)
    {
        using var scope = appli.Services.CreateScope();
        return f(scope.ServiceProvider.GetRequiredService<GarderieDb>());
    }

    private string[] Courriels() =>
        Directory.Exists(Path.Combine(appli.Dossier, "courriels"))
            ? Directory.GetFiles(Path.Combine(appli.Dossier, "courriels"), "*.eml").Select(File.ReadAllText).ToArray()
            : [];

    /// <summary>Lea et Noah (meme mere), Zoe (autre parent), presences enregistrees / Setup.</summary>
    private (int Lea, int Noah, int Zoe) Preparer()
    {
        var options = appli.Services.GetRequiredService<Garderie.Web.Configuration.GarderieOptions>();
        Directory.CreateDirectory(Path.GetDirectoryName(options.FichierModeleFo0310)!);
        File.WriteAllBytes(options.FichierModeleFo0310, ModeleTest.Creer());
        return Db(db =>
        {
            db.Parametres.Add(new Parametres { Id = 1, NomRsge = "Sonia", CourrielRsge = "rsge@exemple.test" });
            var m = DateTime.UtcNow;
            var mere = new Parent { Prenom = "Julie", Nom = "T", Courriel = "julie@exemple.test", CreeLe = m, ModifieLe = m };
            var autre = new Parent { Prenom = "Marc", Nom = "R", Courriel = "marc@exemple.test", CreeLe = m, ModifieLe = m };
            Enfant Enfant(string prenom, Parent p) => new()
            {
                Prenom = prenom, Nom = p.Nom, DateDebut = new DateOnly(2026, 1, 5), CreeLe = m, ModifieLe = m,
                Parents = [new() { Parent = p, Lien = "Parent", EstPayeur = true }],
            };
            var lea = Enfant("Léa", mere);
            var noah = Enfant("Noah", mere);
            var zoe = Enfant("Zoé", autre);
            db.Enfants.AddRange(lea, noah, zoe);
            db.SaveChanges();
            foreach (var e in new[] { lea, noah, zoe })
                for (var i = 0; i < 14; i++)
                {
                    var d = Debut.AddDays(i);
                    if (d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;
                    foreach (var demi in new[] { DemiJournee.Matin, DemiJournee.Pm })
                        db.Presences.Add(new Presence { EnfantId = e.Id, Date = d, Demi = demi, Code = "P", ModifieLe = m });
                }
            db.SaveChanges();
            return (lea.Id, noah.Id, zoe.Id);
        });
    }

    [Fact]
    public async Task SignatureGroupeeEtUnLienParParent()
    {
        var (lea, noah, zoe) = Preparer();
        var admin = Admin();
        var page = $"/Fiches?periode={Debut:yyyy-MM-dd}";

        // La RSGE signe les 3 fiches d'un coup / RSGE signs the 3 sheets at once
        var html = await admin.GetStringAsync(page);
        await admin.PostAsync(page + "&handler=SignerTout", new FormUrlEncodedContent(new List<KeyValuePair<string, string>>
        {
            new("__RequestVerificationToken", Jeton(html)), new("signature", ModeleTest.Png),
            new("enfants", $"{lea}"), new("enfants", $"{noah}"), new("enfants", $"{zoe}"),
        }));
        Assert.Equal(3, Db(db => db.Fiches.Count(f => f.Debut == Debut && f.Etat == EtatFiche.SigneeRsge)));

        // Envoi : 2 courriels (Julie pour Lea et Noah, Marc pour Zoe) / Sending: 2 emails
        html = await admin.GetStringAsync(page);
        await admin.PostAsync(page + "&handler=EnvoyerTout", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = Jeton(html) }));
        var aJulie = Courriels().Where(t => t.Contains("julie@exemple.test")).ToList();
        Assert.Single(aJulie);
        Assert.Single(Courriels(), t => t.Contains("marc@exemple.test"));
        var corps = aJulie[0].Replace("=\r\n", "").Replace("=\n", "").Replace("=3D", "=");
        var jeton = Regex.Match(corps, @"Signer\?j=([A-Za-z0-9_\-]+)").Groups[1].Value;
        Assert.Equal(2, Db(db => db.LiensFiches.Count(g => g.Lien.EnvoyeA == "julie@exemple.test")));

        // Le parent voit les 2 enfants, sans section paiement (vide) / Parent sees both children, no empty payment
        var parent = Parent();
        var vue = System.Net.WebUtility.HtmlDecode(await parent.GetStringAsync($"/garderie/Public/Signer?j={jeton}"));
        Assert.Contains("Fiches d'assiduité de Léa et Noah", vue);
        Assert.DoesNotContain("Zoé", vue);
        Assert.DoesNotContain("Contribution réduite", vue);

        // Une seule signature pour les 2 / One signature for both
        await parent.PostAsync($"/garderie/Public/Signer?j={jeton}&handler=Signer", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Jeton(vue), ["signature"] = ModeleTest.Png, ["nom"] = "Julie T", ["atteste"] = "true",
        }));
        Assert.Equal(EtatFiche.SigneeParent, Db(db => db.Fiches.Single(f => f.EnfantId == lea && f.Debut == Debut).Etat));
        Assert.Equal(EtatFiche.SigneeParent, Db(db => db.Fiches.Single(f => f.EnfantId == noah && f.Debut == Debut).Etat));
        Assert.Equal(EtatFiche.SigneeRsge, Db(db => db.Fiches.Single(f => f.EnfantId == zoe && f.Debut == Debut).Etat));
        Assert.Equal(2, Db(db => db.Documents.Count(d => (d.EnfantId == lea || d.EnfantId == noah) && d.Type == TypeDocument.FicheAssiduite)));

        // Un courriel a la RSGE avec les 2 PDF / One email to the RSGE with both PDFs
        var aRsge = Courriels().Single(t => t.Contains("\nTo: rsge@exemple.test"));
        Assert.Equal(2, Regex.Matches(aRsge, "Content-Type: application/pdf").Count);

        // « Envoyer aux parents » ne renvoie plus rien a Julie / Nothing more to send to Julie
        html = await admin.GetStringAsync(page);
        Assert.DoesNotContain("handler=EnvoyerTout", html);    // Zoe a deja un lien actif / Zoe already has an active link
    }

    [Fact]
    public void PaiementAfficheSeulementSiSaisi()
    {
        Assert.False(Garderie.Web.Pages.Public.SignerModel.PaiementSaisi(new FicheAssiduite()));
        Assert.True(Garderie.Web.Pages.Public.SignerModel.PaiementSaisi(new FicheAssiduite { MontantPaye2 = 1m }));
        Assert.True(Garderie.Web.Pages.Public.SignerModel.PaiementSaisi(new FicheAssiduite { DatePaiement1 = new DateOnly(2026, 3, 27) }));
    }
}
