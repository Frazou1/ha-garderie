using System.Net;
using System.Text.RegularExpressions;
using Garderie.Web.Configuration;
using Garderie.Web.Data;
using Garderie.Web.Securite;
using Garderie.Web.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Garderie.Tests;

/// <summary>Page des releves 24 : propositions, production, releve modifie, sommaire / RL-24 page flow.</summary>
public class RelevesTests(Appli appli) : IClassFixture<Appli>
{
    [Theory]
    [InlineData("123 rue de l'Exemple, app. 4, Québec (Québec) G1A 1A1", "4", "123", "rue de l'Exemple", "Québec", "QC", "G1A1A1")]
    [InlineData("12-3 boul. Laurier, Lévis, QC g6v 2b3", "3", "12", "boul. Laurier", "Lévis", "QC", "G6V2B3")]
    [InlineData("45 rang Saint-Joseph", null, "45", "rang Saint-Joseph", null, null, null)]
    public void DecoupeUneAdresse(string brute, string? app, string? no, string? rue, string? ville, string? prov, string? cp)
    {
        var a = RelevesService.Decouper(brute);
        Assert.Equal((app, no, rue, ville, prov, cp), (a.Appartement, a.Numero, a.Rue, a.Ville, a.Province, a.CodePostal));
    }

    [Fact]
    public void TarifEtMontants()
    {
        Assert.Equal(45m, RelevesService.Tarif("45 $ par jour"));
        Assert.Equal(47.5m, RelevesService.Tarif("47,50 $"));
        Assert.Null(RelevesService.Tarif(null));
        Assert.Equal(1234.5m, RelevesService.Montant("1 234,50 $"));
        Assert.Null(RelevesService.Montant("-5"));
    }

    private HttpClient Admin()
    {
        var c = appli.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        c.DefaultRequestHeaders.Add("X-Test-Port", "8090");
        c.DefaultRequestHeaders.Add("X-Test-Ip", "172.30.32.2");
        c.DefaultRequestHeaders.Add("X-Remote-User-Name", "rsge");
        return c;
    }

    private static string Jeton(string html) =>
        Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;

    private T Db<T>(Func<GarderieDb, Chiffreur, T> f)
    {
        using var scope = appli.Services.CreateScope();
        return f(scope.ServiceProvider.GetRequiredService<GarderieDb>(), scope.ServiceProvider.GetRequiredService<Chiffreur>());
    }

    [Fact]
    public async Task ParcoursDesReleves()
    {
        var (enfantId, parentId) = Db((db, ch) =>
        {
            db.Parametres.Add(new Parametres
            {
                Id = 1, NomGarderie = "Les petits moussaillons", NomRsge = "Sonia (test)", RegieTarif = "45 $", Telephone = "418-555-0000",
                Rl24IdentifiantChiffre = ch.ChiffrerTexte("1234567890", Contextes.IdentifiantRl24), Rl24Numero = "456", Rl24Rue = "rue Test",
                Rl24Ville = "Québec", Rl24Province = "QC", Rl24CodePostal = "G2B2B2", Rl24Signataire = "Sonia (test), responsable",
            });
            var m = DateTime.UtcNow;
            var e = new Enfant { Prenom = "Léa", Nom = "Tremblay", DateNaissance = new DateOnly(2023, 3, 14), DateDebut = new DateOnly(2025, 12, 1), CreeLe = m, ModifieLe = m };
            var parent = new Parent
            {
                Prenom = "Julie", Nom = "Tremblay", Courriel = "julie@exemple.test", CreeLe = m, ModifieLe = m,
                Adresse = "123 rue de l'Exemple, app. 4, Québec (Québec) G1A 1A1", NasChiffre = ch.ChiffrerTexte("130692544", Contextes.NasParent),
            };
            e.Parents = [new() { Parent = parent, Lien = "Mère", EstPayeur = true }];
            db.Enfants.Add(e);
            db.SaveChanges();
            // 23 jours ouvrables en decembre 2025 : une suspension (non payee), une demi-journee
            // 23 weekdays in Dec 2025: one suspension (unpaid), one half day
            foreach (var demi in new[] { DemiJournee.Matin, DemiJournee.Pm })
            {
                db.Presences.Add(new Presence { EnfantId = e.Id, Date = new DateOnly(2025, 12, 2), Demi = demi, Code = "A", ModifieLe = m });
                db.Presences.Add(new Presence { EnfantId = e.Id, Date = new DateOnly(2025, 12, 3), Demi = demi, Code = "S", ModifieLe = m });
            }
            db.Presences.Add(new Presence { EnfantId = e.Id, Date = new DateOnly(2025, 12, 4), Demi = DemiJournee.Matin, Code = "P 1/2", ModifieLe = m });
            db.SaveChanges();
            return (e.Id, parent.Id);
        });
        var admin = Admin();

        var page = WebUtility.HtmlDecode(await admin.GetStringAsync("/Releves?annee=2025"));
        Assert.Contains("Julie Tremblay", page);
        Assert.Contains("Léa : 22 j · 967,50 $", page);   // 21 jours × 45 $ + ½ × 45 $
        Assert.Contains("à produire", page);

        var prep = WebUtility.HtmlDecode(await admin.GetStringAsync($"/Releves/Preparer?annee=2025&parentId={parentId}"));
        Assert.Contains("value=\"rue de l'Exemple\"", prep);
        Assert.Contains("value=\"G1A 1A1\"", prep);
        Assert.DoesNotContain("130692544", prep);   // NAS jamais affiche

        if (Environment.GetEnvironmentVariable("GARDERIE_RL24_DIR") is not { } dir)
        {
            Assert.Contains("le formulaire RL-24", page);   // sans formulaires : rien a produire
            return;
        }
        var options = appli.Services.GetRequiredService<GarderieOptions>();
        Directory.CreateDirectory(Path.GetDirectoryName(options.FichierRl24)!);
        File.Copy(Path.Combine(dir, "rl24.pdf"), options.FichierRl24, true);
        File.Copy(Path.Combine(dir, "rl24s.pdf"), options.FichierRl24S, true);

        KeyValuePair<string, string>[] Champs(string total, string jeton) =>
        [
            new("Saisie.Nom", "Tremblay"), new("Saisie.Prenom", "Julie"), new("Saisie.Appartement", "4"), new("Saisie.Numero", "123"),
            new("Saisie.Rue", "rue de l'Exemple"), new("Saisie.Ville", "Québec"), new("Saisie.Province", "QC"), new("Saisie.CodePostal", "G1A 1A1"),
            new("Saisie.Enfants[0].EnfantId", $"{enfantId}"), new("Saisie.Enfants[0].Jours", "22"), new("Saisie.Enfants[0].Total", total),
            new("Saisie.Enfants[0].NonAdmissible", "10"), new("__RequestVerificationToken", jeton),
        ];
        var url = $"/Releves/Preparer?annee=2025&parentId={parentId}";
        var jeton = Jeton(await admin.GetStringAsync(url));

        var apercu = await admin.PostAsync(url + "&handler=Apercu", new FormUrlEncodedContent(Champs("967,50", jeton)));
        Assert.Equal("application/pdf", apercu.Content.Headers.ContentType?.MediaType);
        Assert.Equal(0, Db((db, _) => db.Releves.Count()));   // l'apercu ne produit rien

        var r = await admin.PostAsync(url + "&handler=Generer", new FormUrlEncodedContent(Champs("967,50", jeton)));
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        var premier = Db((db, _) => db.Releves.Single());
        Assert.Equal(("024000034", "R", enfantId), (premier.Numero, premier.Code, premier.EnfantId));
        Assert.DoesNotContain("130692544", premier.Contenu);   // NAS hors du JSON
        Assert.Equal("024000034", Db((db, _) => db.Parametres.Find(1)!.Rl24DernierNumero));
        Assert.Equal(1, Db((db, _) => db.Documents.Count(d => d.Type == TypeDocument.Releve24 && d.EnfantId == enfantId)));

        // Releve modifie : nouveau numero, code A, remplace le premier / Amended slip
        jeton = Jeton(await admin.GetStringAsync(url));
        await admin.PostAsync(url + "&handler=Generer", new FormUrlEncodedContent(Champs("990.00", jeton)));
        var modifie = Db((db, _) => db.Releves.OrderBy(x => x.Id).Last());
        Assert.Equal(("024000045", "A", "024000034"), (modifie.Numero, modifie.Code, modifie.NumeroOriginal));
        Assert.Equal(modifie.Id, Db((db, _) => db.Releves.Find(premier.Id)!.RemplaceParId));
        var totaux = Db((db, _) => (db.Releves.Count(x => x.RemplaceParId == null)));
        Assert.Equal(1, totaux);

        var pdf = await admin.GetAsync($"/Releves?handler=Pdf&id={modifie.Id}");
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        var sommaire = await admin.GetAsync("/Releves?handler=Sommaire&annee=2025");
        Assert.Equal("application/pdf", sommaire.Content.Headers.ContentType?.MediaType);
        Assert.Contains("980,00 $", WebUtility.HtmlDecode(await admin.GetStringAsync("/Releves?annee=2025")));   // E = 990 − 10
        if (Environment.GetEnvironmentVariable("GARDERIE_PDF_DIR") is { } sortie)
        {
            File.WriteAllBytes(Path.Combine(sortie, "releve_modifie.pdf"), await pdf.Content.ReadAsByteArrayAsync());
            File.WriteAllBytes(Path.Combine(sortie, "sommaire_2025.pdf"), await sommaire.Content.ReadAsByteArrayAsync());
        }
    }
}
