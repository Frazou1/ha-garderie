using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Garderie.Tests;

/// <summary>Onglet Aide et sa video (Reglages) / Help tab and its video.</summary>
public class AideTests(Appli appli) : IClassFixture<Appli>
{
    private static readonly byte[] Mp4 = [0, 0, 0, 0x18, (byte)'f', (byte)'t', (byte)'y', (byte)'p', (byte)'i', (byte)'s', (byte)'o', (byte)'m', 0, 0, 2, 0];

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

    private static async Task Televerser(HttpClient c, byte[] contenu)
    {
        var form = new MultipartFormDataContent { { new StringContent(Jeton(await c.GetStringAsync("/Parametres"))), "__RequestVerificationToken" } };
        var f = new ByteArrayContent(contenu);
        f.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");
        form.Add(f, "video", "aide.mp4");
        await c.PostAsync("/Parametres?handler=VideoAide", form);
    }

    [Fact]
    public async Task FeuilleAuxParentsDansAide()
    {
        var admin = Client("8090", "172.30.32.2");
        Assert.Contains("Aucune feuille", WebUtility.HtmlDecode(await admin.GetStringAsync("/Aide")));
        var form = new MultipartFormDataContent { { new StringContent(Jeton(await admin.GetStringAsync("/Parametres"))), "__RequestVerificationToken" } };
        var f = new ByteArrayContent("%PDF-1.4 feuille"u8.ToArray());
        f.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(f, "feuille", "feuille.pdf");
        await admin.PostAsync("/Parametres?handler=FeuilleParents", form);
        Assert.Contains("handler=Feuille", await admin.GetStringAsync("/Aide"));
        var r = await admin.GetAsync("/Aide?handler=Feuille");
        Assert.Equal("application/pdf", r.Content.Headers.ContentType?.MediaType);
        Assert.Contains("aide aux parents", Uri.UnescapeDataString(r.Content.Headers.ContentDisposition?.ToString() ?? ""));
        await admin.PostAsync("/Parametres?handler=RetirerFeuilleParents",
            new FormUrlEncodedContent([new("__RequestVerificationToken", Jeton(await admin.GetStringAsync("/Parametres")))]));
        Assert.Contains("Aucune feuille", WebUtility.HtmlDecode(await admin.GetStringAsync("/Aide")));
    }

    [Fact]
    public async Task VideoDansAide()
    {
        var admin = Client("8090", "172.30.32.2");
        Assert.Contains("href=\"/Aide\"", await admin.GetStringAsync("/Presences"));   // onglet
        Assert.Contains("Aucune vidéo", await admin.GetStringAsync("/Aide"));

        await Televerser(admin, "%PDF-1.7 pas une video"u8.ToArray());   // refuse
        Assert.Contains("Aucune vidéo", await admin.GetStringAsync("/Aide"));

        await Televerser(admin, Mp4);
        var html = WebUtility.HtmlDecode(await admin.GetStringAsync("/Aide"));
        Assert.Contains("<video", html);
        var url = Regex.Match(html, "src=\"(/Aide[^\"]*handler=Video[^\"]*)\"").Groups[1].Value;
        Assert.NotEmpty(url);
        var plage = new HttpRequestMessage(HttpMethod.Get, url);
        plage.Headers.Range = new RangeHeaderValue(4, 7);
        var r = await admin.SendAsync(plage);
        Assert.Equal(HttpStatusCode.PartialContent, r.StatusCode);   // lecture par plages (iPad)
        Assert.Equal("ftyp"u8.ToArray(), await r.Content.ReadAsByteArrayAsync());

        var parent = Client("8080", "203.0.113.30");   // pas sur le port public
        Assert.NotEqual(HttpStatusCode.OK, (await parent.GetAsync("/garderie" + url)).StatusCode);

        await admin.PostAsync("/Parametres?handler=RetirerVideoAide",
            new FormUrlEncodedContent([new("__RequestVerificationToken", Jeton(await admin.GetStringAsync("/Parametres")))]));
        Assert.Contains("Aucune vidéo", await admin.GetStringAsync("/Aide"));
    }
}
