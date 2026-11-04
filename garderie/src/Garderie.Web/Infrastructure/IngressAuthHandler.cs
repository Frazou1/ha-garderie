using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Garderie.Web.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Garderie.Web.Infrastructure;

/// <summary>
/// Authentification par l'Ingress de HA : HA a deja connecte l'utilisateur et transmet son identite
/// (X-Remote-User-*). On n'accepte ces en-tetes que sur le port Ingress (voir ZonesMiddleware) et
/// seulement pour les utilisateurs listes dans les options du module.
/// HA Ingress authentication: HA already signed the user in and forwards its identity (X-Remote-User-*).
/// Accepted only on the Ingress port and only for users listed in the add-on options.
/// </summary>
public sealed class IngressAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> opts, ILoggerFactory logger, UrlEncoder encoder, GarderieOptions garderie)
    : AuthenticationHandler<AuthenticationSchemeOptions>(opts, logger, encoder)
{
    public const string Schema = "HaIngress";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Context.ZoneDe() != Zone.Ingress)
            return Task.FromResult(AuthenticateResult.NoResult());

        var nom = Request.Headers["X-Remote-User-Name"].ToString();
        var id = Request.Headers["X-Remote-User-Id"].ToString();
        if (string.IsNullOrWhiteSpace(nom) && string.IsNullOrWhiteSpace(id))
            return Task.FromResult(AuthenticateResult.Fail("Aucun utilisateur HA"));
        if (!garderie.EstUtilisateurAutorise(nom, id))
            return Task.FromResult(AuthenticateResult.Fail("Utilisateur HA non autorise"));

        var affichage = Request.Headers["X-Remote-User-Display-Name"].ToString();
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, string.IsNullOrWhiteSpace(nom) ? id : nom),
            new(ClaimTypes.NameIdentifier, string.IsNullOrWhiteSpace(id) ? nom : id),
            new("nom_affiche", string.IsNullOrWhiteSpace(affichage) ? nom : affichage),
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Schema));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Schema)));
    }

    /// <summary>
    /// Page 403 qui dit quel nom ajouter aux options / 403 page telling which name to add to the options.
    /// </summary>
    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = Context.ZoneDe() == Zone.Ingress ? StatusCodes.Status403Forbidden : StatusCodes.Status404NotFound;
        if (Context.ZoneDe() != Zone.Ingress) return;

        var nom = WebUtility.HtmlEncode(Request.Headers["X-Remote-User-Name"].ToString());
        Response.ContentType = "text/html; charset=utf-8";
        await Response.WriteAsync($"""
            <!doctype html><html lang="fr"><head><meta charset="utf-8"><title>Accès refusé</title></head>
            <body><h1>Accès refusé</h1>
            <p>Votre compte Home Assistant (<strong>{nom}</strong>) n'est pas autorisé à ouvrir la garderie.</p>
            <p>Un administrateur HA doit ajouter ce nom dans <em>Paramètres → Modules complémentaires → Garderie →
            Configuration → Utilisateurs HA autorisés</em>, puis redémarrer le module.</p>
            </body></html>
            """);
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties) => HandleChallengeAsync(properties);
}
