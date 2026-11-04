using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Garderie.Tests;

/// <summary>
/// Simule les deux ports et l'IP de l'appelant (TestServer n'a pas de vrai port).
/// Simulates both ports and the caller IP (TestServer has no real port).
/// </summary>
public sealed class Appli : WebApplicationFactory<Program>
{
    public string Dossier { get; } = Path.Combine(Path.GetTempPath(), "garderie-tests-" + Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.UseSetting("Garderie:DataDir", Dossier);
        builder.UseSetting("Garderie:PortPublic", "8080");
        builder.UseSetting("Garderie:PortIngress", "8090");
        builder.UseSetting("utilisateurs_autorises:0", "rsge");
        builder.UseSetting("path_base_public", "/garderie");
        builder.UseSetting("url_publique", "https://exemple.test/garderie");
        builder.UseSetting("Garderie:DossierCourrielsTest", Path.Combine(Dossier, "courriels"));
        builder.ConfigureServices(s => s.AddTransient<IStartupFilter, FiltreConnexion>());
    }

    private sealed class FiltreConnexion : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (ctx, suite) =>
            {
                ctx.Connection.LocalPort = int.Parse(ctx.Request.Headers["X-Test-Port"].FirstOrDefault() ?? "0");
                ctx.Connection.RemoteIpAddress = IPAddress.Parse(ctx.Request.Headers["X-Test-Ip"].FirstOrDefault() ?? "192.168.2.50");
                await suite();
            });
            next(app);
        };
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try { Directory.Delete(Dossier, true); } catch { /* SQLite parfois encore ouvert / sometimes still open */ }
    }
}

public class ZonesTests(Appli appli) : IClassFixture<Appli>
{
    private const string Supervisor = "172.30.32.2";

    private async Task<HttpResponseMessage> Get(string url, int port, string ip = "192.168.2.50", string? user = null, string? ingress = null)
    {
        var client = appli.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Add("X-Test-Port", port.ToString());
        req.Headers.Add("X-Test-Ip", ip);
        if (user is not null) req.Headers.Add("X-Remote-User-Name", user);
        if (ingress is not null) req.Headers.Add("X-Ingress-Path", ingress);
        return await client.SendAsync(req);
    }

    [Theory]
    [InlineData("/garderie/")]
    [InlineData("/garderie/sante")]
    [InlineData("/garderie/lib/bootstrap/dist/css/bootstrap.min.css")]
    public async Task PublicAccessible(string url) =>
        Assert.Equal(HttpStatusCode.OK, (await Get(url, 8080)).StatusCode);

    [Theory]
    [InlineData("/garderie/Enfants")]
    [InlineData("/garderie/Presences")]
    [InlineData("/garderie/Journal")]
    [InlineData("/Enfants")]
    [InlineData("/garderie/Enfants/Dossier/1?handler=Document&documentId=1")]
    public async Task AdminInvisibleSurLePortPublic(string url)
    {
        // Meme avec des en-tetes HA forges / Even with forged HA headers
        var r = await Get(url, 8080, user: "rsge", ingress: "/api/hassio_ingress/x");
        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
    }

    [Fact]
    public async Task IngressRefuseHorsSupervisor() =>
        Assert.Equal(HttpStatusCode.Forbidden, (await Get("/Enfants", 8090, ip: "172.30.32.9", user: "rsge")).StatusCode);

    [Fact]
    public async Task IngressRefuseUtilisateurNonAutorise()
    {
        var r = await Get("/Enfants", 8090, ip: Supervisor, user: "intrus");
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
        Assert.Contains("intrus", await r.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task IngressRefuseSansUtilisateur() =>
        Assert.Equal(HttpStatusCode.Forbidden, (await Get("/Enfants", 8090, ip: Supervisor)).StatusCode);

    [Fact]
    public async Task IngressAutoriseAvecPathBase()
    {
        var r = await Get("/Enfants", 8090, ip: Supervisor, user: "RSGE", ingress: "/api/hassio_ingress/abc123");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var html = await r.Content.ReadAsStringAsync();
        Assert.Contains("href=\"/api/hassio_ingress/abc123/Presences\"", html);
        Assert.Contains("frame-ancestors 'self'", r.Headers.GetValues("Content-Security-Policy").Single());
    }

    [Fact]
    public async Task CheminIngressInvalideIgnore()
    {
        var r = await Get("/Enfants", 8090, ip: Supervisor, user: "rsge", ingress: "https://pirate.example/x");
        var html = await r.Content.ReadAsStringAsync();
        Assert.DoesNotContain("pirate", html);
        Assert.Contains("href=\"/Presences\"", html);
    }

    [Fact]
    public async Task PortInconnu() =>
        Assert.Equal(HttpStatusCode.NotFound, (await Get("/Enfants", 9999, ip: Supervisor, user: "rsge")).StatusCode);

    [Fact]
    public async Task PagesNonMisesEnCache()
    {
        var r = await Get("/Presences", 8090, ip: Supervisor, user: "rsge");
        Assert.Equal("no-store", r.Headers.CacheControl?.ToString());
    }
}
