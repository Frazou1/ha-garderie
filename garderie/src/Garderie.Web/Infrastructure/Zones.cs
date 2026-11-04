using System.Net;
using System.Text.RegularExpressions;
using Garderie.Web.Configuration;

namespace Garderie.Web.Infrastructure;

/// <summary>Zone d'une requete selon le port recu / Request zone based on the receiving port.</summary>
public enum Zone { Inconnue, Ingress, Public }

public static class ZoneHttp
{
    private const string CleZone = "garderie.zone";
    private const string CleIp = "garderie.ip";

    public static Zone ZoneDe(this HttpContext ctx) =>
        ctx.Items.TryGetValue(CleZone, out var z) && z is Zone zone ? zone : Zone.Inconnue;

    /// <summary>IP reelle du client (apres proxy) / Real client IP (after proxy).</summary>
    public static string IpClient(this HttpContext ctx) =>
        ctx.Items.TryGetValue(CleIp, out var ip) && ip is string s ? s : ctx.Connection.RemoteIpAddress?.ToString() ?? "?";

    internal static void Definir(HttpContext ctx, Zone zone, string ip)
    {
        ctx.Items[CleZone] = zone;
        ctx.Items[CleIp] = ip;
    }
}

/// <summary>
/// Separe les deux portes d'entree de l'appli :
///  - port Ingress : seulement le Supervisor HA ; PathBase = en-tete X-Ingress-Path ; toute l'administration.
///  - port public : derriere NPM ; PathBase fixe (/garderie) ; seulement /Public, fichiers statiques et /sante.
/// Splits the app's two entry points (Ingress = admin via HA Supervisor, public = parent pages via NPM).
/// </summary>
public sealed partial class ZonesMiddleware(RequestDelegate next, GarderieOptions options, IHostEnvironment env, ILogger<ZonesMiddleware> log)
{
    [GeneratedRegex(@"^/api/hassio_ingress/[A-Za-z0-9_\-]+$")]
    private static partial Regex CheminIngressValide();

    private static readonly string[] PrefixesPublics = ["/public", "/css", "/js", "/lib", "/favicon.ico", "/sante"];

    public async Task InvokeAsync(HttpContext ctx)
    {
        var port = ctx.Connection.LocalPort;
        if (port == options.PortIngress)
        {
            if (!PreparerIngress(ctx)) return;
        }
        else if (port == options.PortPublic)
        {
            if (!PreparerPublic(ctx)) return;
        }
        else
        {
            ctx.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }
        await next(ctx);
    }

    private bool PreparerIngress(HttpContext ctx)
    {
        var remote = ctx.Connection.RemoteIpAddress is { } r ? GarderieOptions.Normaliser(r) : null;
        var simulation = options.SimulerIngress && env.IsDevelopment() && remote is not null && IPAddress.IsLoopback(remote);

        if (simulation)
        {
            // Imite HA : retire le prefixe, ajoute les en-tetes / Mimic HA: strip prefix, add headers
            if (ctx.Request.Path.StartsWithSegments(GarderieOptions.PrefixeIngressSimule, out var reste))
                ctx.Request.Path = reste.HasValue ? reste : "/";
            ctx.Request.Headers["X-Ingress-Path"] = GarderieOptions.PrefixeIngressSimule;
            if (!ctx.Request.Headers.ContainsKey("X-Remote-User-Name"))
            {
                ctx.Request.Headers["X-Remote-User-Name"] = options.UtilisateurSimule;
                ctx.Request.Headers["X-Remote-User-Id"] = "id-" + options.UtilisateurSimule;
            }
        }
        else if (remote is null || !IPAddress.TryParse(options.IpIngress, out var sup) || !remote.Equals(sup))
        {
            // Seul le Supervisor peut parler au port Ingress / Only the Supervisor may reach the Ingress port
            log.LogWarning("Requete Ingress refusee depuis {Ip}", remote);
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            return false;
        }

        var prefixe = ctx.Request.Headers["X-Ingress-Path"].ToString();
        if (CheminIngressValide().IsMatch(prefixe))
            ctx.Request.PathBase = prefixe;

        // IP du navigateur relayee par le Supervisor / Browser IP relayed by the Supervisor
        var xff = ctx.Request.Headers["X-Forwarded-For"].ToString();
        var ip = xff.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
                 ?? remote?.ToString() ?? "?";
        ZoneHttp.Definir(ctx, Zone.Ingress, ip);
        return true;
    }

    private bool PreparerPublic(HttpContext ctx)
    {
        var remote = ctx.Connection.RemoteIpAddress is { } r ? GarderieOptions.Normaliser(r) : null;
        var ip = remote?.ToString() ?? "?";
        if (options.EstProxyDeConfiance(remote))
        {
            // Derniere entree = ajoutee par NPM / Last entry = added by NPM
            var xff = ctx.Request.Headers["X-Forwarded-For"].ToString()
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (xff.Length > 0) ip = xff[^1];
            if (string.Equals(ctx.Request.Headers["X-Forwarded-Proto"], "https", StringComparison.OrdinalIgnoreCase))
                ctx.Request.Scheme = "https";
        }
        // Les en-tetes de l'Ingress n'ont aucun sens ici / Ingress headers are meaningless here
        ctx.Request.Headers.Remove("X-Remote-User-Name");
        ctx.Request.Headers.Remove("X-Remote-User-Id");
        ctx.Request.Headers.Remove("X-Remote-User-Display-Name");
        ctx.Request.Headers.Remove("X-Ingress-Path");

        if (options.PathBasePublic.Length > 0 && ctx.Request.Path.StartsWithSegments(options.PathBasePublic, out var reste))
        {
            ctx.Request.PathBase = options.PathBasePublic;
            ctx.Request.Path = reste.HasValue ? reste : "/";
        }

        var chemin = ctx.Request.Path.Value ?? "/";
        if (chemin == "/")
        {
            ctx.Request.Path = "/Public";
            chemin = "/Public";
        }
        if (!PrefixesPublics.Any(p => ctx.Request.Path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase)))
        {
            ctx.Response.StatusCode = StatusCodes.Status404NotFound;
            return false;
        }
        ZoneHttp.Definir(ctx, Zone.Public, ip);
        return true;
    }
}
