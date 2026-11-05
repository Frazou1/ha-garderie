using System.Net;
using System.Text.RegularExpressions;
using Garderie.Web.Data;
using Garderie.Web.Securite;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Garderie.Tests;

/// <summary>Parcours complet d'un contrat, puis sa resiliation / Full contract flow, then termination.</summary>
public class ContratsTests(Appli appli) : IClassFixture<Appli>
{
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
        c.DefaultRequestHeaders.Add("X-Test-Ip", "203.0.113.20");
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
            ? Directory.GetFiles(Path.Combine(appli.Dossier, "courriels"), "*.eml").OrderBy(f => f).Select(File.ReadAllText).ToArray()
            : [];

    private string DernierJeton(string destinataire)
    {
        var eml = Courriels().Last(t => t.Contains(destinataire)).Replace("=\r\n", "").Replace("=\n", "").Replace("=3D", "=");
        return Regex.Match(eml, @"Public/Contrat\?j=([A-Za-z0-9_\-]+)").Groups[1].Value;
    }

    private async Task<HttpResponseMessage> Poster(HttpClient c, string page, string url, IEnumerable<KeyValuePair<string, string>> champs)
    {
        var html = await c.GetStringAsync(page);
        return await c.PostAsync(url, new FormUrlEncodedContent(champs.Append(new("__RequestVerificationToken", Jeton(html)))));
    }

    [Fact]
    public async Task ParcoursContratPuisResiliation()
    {
        var id = Db(db =>
        {
            db.Parametres.Add(new Parametres { Id = 1, NomGarderie = "Les petits moussaillons", NomRsge = "Sonia", CourrielRsge = "rsge@exemple.test", VilleSignature = "Québec",
                LienFacebook = "https://www.facebook.com/groups/123456" });
            var m = DateTime.UtcNow;
            var e = new Enfant { Prenom = "Léa", Nom = "C", DateDebut = new DateOnly(2026, 9, 1), CreeLe = m, ModifieLe = m };
            e.Parents = [new() { Parent = new Parent { Prenom = "Julie", Nom = "C", Courriel = "julie@exemple.test", CreeLe = m, ModifieLe = m }, Lien = "Mère", EstPayeur = true }];
            db.Enfants.Add(e);
            db.SaveChanges();
            return e.Id;
        });
        var admin = Admin();

        // Feries 2027 (une seule fois) / 2027 holidays (once)
        await Poster(admin, "/Fermetures", "/Fermetures?handler=Feries", [new("annee", "2027")]);
        await Poster(admin, "/Fermetures", "/Fermetures?handler=Feries", [new("annee", "2027")]);
        Assert.Equal(12, Db(db => db.Fermetures.Count(f => f.Date.Year == 2027)));

        // Creation et envoi / Creation and sending
        var r = await Poster(admin, "/Contrats", "/Contrats?handler=Creer",
            [new("enfantId", $"{id}"), new("debut", "2026-09-01"), new("fin", "2027-08-31")]);
        var contratId = int.Parse(Regex.Match(r.Headers.Location!.ToString(), @"Contrat/(\d+)").Groups[1].Value);
        // Regie generee : une section modifiee et le tarif dans Reglages / Generated rules: one edited section and the rate
        await Poster(admin, "/Parametres", "/Parametres?handler=Regie",
            [new("RegieTarif", "45 $"), new("RegieJourPaiement", "mercredi"), new("Regie[covid-19]", "Voir l'annexe.")]);
        Assert.Contains("covid-19", Db(db => db.Parametres.Find(1)!.RegieSections)!);
        Assert.Equal("45 $", Db(db => db.Parametres.Find(1)!.RegieTarif));
        Assert.Equal("application/pdf", (await admin.GetAsync("/Parametres?handler=RegiePdf")).Content.Headers.ContentType?.MediaType);
        Assert.Equal("application/pdf", (await admin.GetAsync("/Parametres?handler=ContratModele")).Content.Headers.ContentType?.MediaType);

        await Poster(admin, $"/Contrats/Contrat/{contratId}", $"/Contrats/Contrat/{contratId}?handler=Envoyer", []);
        var jeton = DernierJeton("julie@exemple.test");
        Assert.Contains(Courriels(), t => t.Contains("julie@exemple.test") && t.Contains("application/pdf"));   // regie jointe
        Assert.Contains(Courriels(), t => t.Contains("julie@exemple.test") && t.Replace("=\r\n", "").Replace("=\n", "").Contains("facebook.com/groups/123456"));

        // Le parent voit l'entente et les feries ; NAS invalide refuse / Parent sees the agreement; invalid SIN refused
        var parent = Parent();
        var url = $"/garderie/Public/Contrat?j={jeton}";
        var page = WebUtility.HtmlDecode(await parent.GetStringAsync(url));
        Assert.Contains("Entente de service", page);
        Assert.Contains("Lundi de Pâques", page);
        Assert.Contains("Les petits moussaillons appartenant à Sonia", page);
        Assert.Contains("Lire la régie interne", page);
        var regie = await parent.GetAsync($"/garderie/Public/Contrat?j={jeton}&handler=Regie");
        Assert.Equal("application/pdf", regie.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith("inline", regie.Content.Headers.ContentDisposition?.ToString());

        KeyValuePair<string, string>[] Saisie(string nas) =>
        [
            new("Saisie.EnfantAdresse", "1 rue Test"), new("Saisie.ParentAdresse", "1 rue Test"), new("Saisie.ParentTelephone1", "418-555-0101"),
            new("Saisie.Lien", "Mère"), new("Saisie.Nas", nas), new("Saisie.VacancesParent", "Semaine du 19 juillet"),
            new("signature", ModeleTest.Png), new("nom", "Julie C"), new("atteste", "true"), new("Saisie.ConsentementRl24", "true"),
        ];
        var refus = WebUtility.HtmlDecode(await (await Poster(parent, url, url + "&handler=Signer", Saisie("123 456 789"))).Content.ReadAsStringAsync());
        Assert.Contains("Le NAS n'est pas valide", refus);
        Assert.Equal(EtatContrat.Brouillon, Db(db => db.Contrats.Single(c => c.Id == contratId).Etat));

        var ok = await Poster(parent, url, url + "&handler=Signer", Saisie("130 692 544"));
        Assert.Contains("Merci", await ok.Content.ReadAsStringAsync());
        var contrat = Db(db => db.Contrats.Single(c => c.Id == contratId));
        Assert.Equal(EtatContrat.SigneParent, contrat.Etat);
        Assert.NotNull(contrat.Fige);
        Assert.Equal("203.0.113.20", contrat.IpParent);
        var dossierParent = Db(db => db.Parents.Single(p => p.Courriel == "julie@exemple.test"));
        Assert.Equal("1 rue Test", dossierParent.Adresse);
        Assert.NotNull(dossierParent.ConsentRl24Le);   // case cochee a la signature / box checked when signing
        Assert.Equal("Signature du contrat en ligne", dossierParent.ConsentRl24Source);
        var chiffreur = appli.Services.GetRequiredService<Chiffreur>();
        Assert.Equal("130692544", chiffreur.DechiffrerTexte(dossierParent.NasChiffre, Contextes.NasParent));
        Assert.Equal("130692544", chiffreur.DechiffrerTexte(contrat.NasChiffre, Contextes.NasContrat));

        // Contresignature groupee / Batch countersignature
        await Poster(admin, "/Contrats", "/Contrats?handler=Contresigner", [new("contratIds", $"{contratId}"), new("signature", ModeleTest.Png)]);
        contrat = Db(db => db.Contrats.Single(c => c.Id == contratId));
        Assert.Equal(EtatContrat.Signe, contrat.Etat);
        Assert.Equal(TypeDocument.Contrat, Db(db => db.Documents.Single(d => d.Id == contrat.DocumentId).Type));
        Assert.Contains(Courriels(), t => t.Contains("julie@exemple.test") && t.Contains("application/pdf"));   // copie au parent (LPC)
        Assert.Contains(Courriels(), t => t.Contains("rsge@exemple.test") && t.Contains("application/pdf"));

        // L'archive signee ne se supprime pas seule depuis le dossier / The signed archive cannot be deleted alone
        Assert.Contains("archive signée", await admin.GetStringAsync($"/Enfants/Dossier/{id}"));
        await Poster(admin, $"/Enfants/Dossier/{id}", $"/Enfants/Dossier/{id}?handler=SupprimerDocument", [new("documentId", $"{contrat.DocumentId}")]);
        Assert.True(Db(db => db.Documents.Any(d => d.Id == contrat.DocumentId)));

        // Chaque formulaire POST de la page porte son jeton antiforgery / Every POST form carries its token
        JetonsPartout(await admin.GetStringAsync($"/Contrats/Contrat/{contratId}"));
        JetonsPartout(await admin.GetStringAsync("/Contrats"));
        JetonsPartout(await admin.GetStringAsync($"/Enfants/Dossier/{id}"));

        // Afficher (inline, sans CSP de page) et telecharger / Show inline (no page CSP) and download
        var vue = await admin.GetAsync($"/Contrats/Contrat/{contratId}?handler=Pdf&afficher=true");
        Assert.Equal("application/pdf", vue.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith("inline", vue.Content.Headers.ContentDisposition?.ToString());
        Assert.False(vue.Headers.Contains("Content-Security-Policy"));
        var tele = await admin.GetAsync($"/Contrats/Contrat/{contratId}?handler=Pdf");
        Assert.StartsWith("attachment", tele.Content.Headers.ContentDisposition?.ToString());

        // Renvoyer une copie au parent / Re-send a copy to the parent
        var avant = Courriels().Count(t => t.Contains("julie@exemple.test"));
        await Poster(admin, $"/Contrats/Contrat/{contratId}", $"/Contrats/Contrat/{contratId}?handler=Copie", [new("pour", "parent")]);
        Assert.Equal(avant + 1, Courriels().Count(t => t.Contains("julie@exemple.test")));
        Assert.Contains(Courriels(), t => t.Contains("Copie : entente de service") && t.Contains("application/pdf"));

        // Resiliation : preparee, envoyee, signee / Termination: prepared, sent, signed
        await Poster(admin, $"/Contrats/Contrat/{contratId}", $"/Contrats/Contrat/{contratId}?handler=Resilier", [new("date", "2027-01-15")]);
        await Poster(admin, $"/Contrats/Contrat/{contratId}", $"/Contrats/Contrat/{contratId}?handler=Envoyer&but=Resiliation", []);
        var jetonR = DernierJeton("julie@exemple.test");
        Assert.NotEqual(jeton, jetonR);
        var urlR = $"/garderie/Public/Contrat?j={jetonR}";
        Assert.Contains("Formule de résiliation", WebUtility.HtmlDecode(await parent.GetStringAsync(urlR)));
        await Poster(parent, urlR, urlR + "&handler=Signer", [new("signature", ModeleTest.Png), new("nom", "Julie C"), new("atteste", "true")]);

        contrat = Db(db => db.Contrats.Single(c => c.Id == contratId));
        Assert.Equal(EtatContrat.Resilie, contrat.Etat);
        Assert.Equal(new DateOnly(2027, 1, 15), Db(db => db.Enfants.Single(e => e.Id == id).DateFin));
        Assert.Equal(TypeDocument.Resiliation, Db(db => db.Documents.Single(d => d.Id == contrat.DocumentResiliationId).Type));

        // Le lien de signature deja utilise remercie / Used link thanks
        Assert.Contains("Merci", await parent.GetStringAsync(url));
    }

    /// <summary>Aucun formulaire POST sans jeton (sinon refus 400 en vrai) / No POST form without a token.</summary>
    public static void JetonsPartout(string html)
    {
        var formulaires = Regex.Matches(html, "<form[^>]*method=\"post\"[^>]*>(.*?)</form>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        Assert.NotEmpty(formulaires);
        foreach (Match f in formulaires)
            Assert.True(f.Groups[1].Value.Contains("__RequestVerificationToken"), "Formulaire sans jeton : " + f.Value[..Math.Min(160, f.Value.Length)]);
    }

    [Fact]
    public async Task ContratsInvisiblesSurLePortPublic() =>
        Assert.Equal(HttpStatusCode.NotFound, (await Parent().GetAsync("/garderie/Contrats")).StatusCode);
}
