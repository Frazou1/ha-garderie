using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Garderie.Tests;

/// <summary>Logo et groupe Facebook dans Reglages / Logo and Facebook group in Settings.</summary>
public class LogoTests(Appli appli) : IClassFixture<Appli>
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D];

    private HttpClient Client(string port, string ip)
    {
        var c = appli.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        c.DefaultRequestHeaders.Add("X-Test-Port", port);
        c.DefaultRequestHeaders.Add("X-Test-Ip", ip);
        if (port == "8090") c.DefaultRequestHeaders.Add("X-Remote-User-Name", "rsge");
        return c;
    }

    private static string Jeton(string html) =>
        Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;

    private static async Task<HttpResponseMessage> Televerser(HttpClient c, byte[] contenu, string type)
    {
        var form = new MultipartFormDataContent { { new StringContent(Jeton(await c.GetStringAsync("/Parametres"))), "__RequestVerificationToken" } };
        var f = new ByteArrayContent(contenu);
        f.Headers.ContentType = new MediaTypeHeaderValue(type);
        form.Add(f, "logo", "logo.png");
        return await c.PostAsync("/Parametres?handler=Logo", form);
    }

    [Fact]
    public async Task LogoRemplaceLeBateau()
    {
        var admin = Client("8090", "172.30.32.2");
        var parent = Client("8080", "203.0.113.30");
        Assert.Contains("marque-icone", await admin.GetStringAsync("/Presences"));   // bateau par defaut
        Assert.Equal(HttpStatusCode.NotFound, (await parent.GetAsync("/garderie/Public/Logo")).StatusCode);

        await Televerser(admin, "%PDF-1.7"u8.ToArray(), "image/png");   // pas une image : refuse
        Assert.Equal(HttpStatusCode.NotFound, (await parent.GetAsync("/garderie/Public/Logo")).StatusCode);

        await Televerser(admin, Png, "image/png");
        var html = await admin.GetStringAsync("/Presences");
        Assert.Contains("marque-logo", html);
        Assert.DoesNotContain("marque-icone", html);
        var logo = await parent.GetAsync("/garderie/Public/Logo");   // port public : pages des parents
        Assert.Equal("image/png", logo.Content.Headers.ContentType?.MediaType);

        var page = await admin.GetStringAsync("/Parametres");
        await admin.PostAsync("/Parametres?handler=RetirerLogo",
            new FormUrlEncodedContent([new("__RequestVerificationToken", Jeton(page))]));
        Assert.Contains("marque-icone", await admin.GetStringAsync("/Presences"));
    }

    [Fact]
    public async Task LienFacebookValide()
    {
        var admin = Client("8090", "172.30.32.2");
        async Task Enregistrer(string lien) => await admin.PostAsync("/Parametres", new FormUrlEncodedContent(
            [new("Donnees.NomGarderie", "Les petits moussaillons"), new("Donnees.LienFacebook", lien),
             new("__RequestVerificationToken", Jeton(await admin.GetStringAsync("/Parametres")))]));

        await Enregistrer("https://exemple.test/faux");
        Assert.DoesNotContain("exemple.test/faux", WebUtility.HtmlDecode(await admin.GetStringAsync("/Parametres")));
        await Enregistrer("https://www.facebook.com/groups/1537925969808583");
        Assert.Contains("https://www.facebook.com/groups/1537925969808583", await admin.GetStringAsync("/Parametres"));
    }
}
