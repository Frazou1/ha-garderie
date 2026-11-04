namespace Garderie.Web.Infrastructure;

/// <summary>
/// En-tetes de securite communs (CSP stricte, sans script en ligne) ; pages jamais mises en cache.
/// Common security headers (strict CSP, no inline script); pages never cached.
/// </summary>
public sealed class EnTetesSecuriteMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext ctx)
    {
        ctx.Response.OnStarting(() =>
        {
            var h = ctx.Response.Headers;
            // L'Ingress affiche l'appli dans un iframe de HA (meme origine) / Ingress shows the app in an HA iframe
            var ancetres = ctx.ZoneDe() == Zone.Ingress ? "'self'" : "'none'";
            h["Content-Security-Policy"] =
                "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data: blob:; " +
                $"font-src 'self'; connect-src 'self'; form-action 'self'; base-uri 'self'; object-src 'none'; frame-ancestors {ancetres}";
            h["X-Content-Type-Options"] = "nosniff";
            h["Referrer-Policy"] = "no-referrer";
            h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
            h["Cross-Origin-Opener-Policy"] = "same-origin";
            if (ctx.ZoneDe() == Zone.Public) h["X-Frame-Options"] = "DENY";

            var type = ctx.Response.ContentType ?? "";
            if (type.StartsWith("text/html", StringComparison.OrdinalIgnoreCase) || !h.ContainsKey("Cache-Control"))
            {
                if (!ctx.Request.Path.StartsWithSegments("/lib") && !ctx.Request.Path.StartsWithSegments("/css") && !ctx.Request.Path.StartsWithSegments("/js"))
                    h["Cache-Control"] = "no-store";
            }
            return Task.CompletedTask;
        });
        return next(ctx);
    }
}
