using System.Net;

namespace Garderie.Web.Configuration;

/// <summary>
/// Reglages de l'appli : section "Garderie" (appsettings / variables d'env.) + options du module HA
/// (/data/options.json, cles snake_case a la racine).
/// App settings: "Garderie" section (appsettings / env vars) + HA add-on options (/data/options.json).
/// </summary>
public sealed class GarderieOptions
{
    /// <summary>Dossier des donnees (base, documents, cles) / Data folder (db, documents, keys).</summary>
    public string DataDir { get; set; } = "/data";

    /// <summary>Port public (pages des parents) / Public port (parent pages).</summary>
    public int PortPublic { get; set; } = 8080;

    /// <summary>Port Ingress de HA (administration) / HA Ingress port (admin).</summary>
    public int PortIngress { get; set; } = 8090;

    /// <summary>IP du Supervisor HA qui relaie l'Ingress / HA Supervisor IP relaying Ingress.</summary>
    public string IpIngress { get; set; } = "172.30.32.2";

    /// <summary>
    /// Developpement seulement : simule le proxy Ingress de HA sur le port Ingress local.
    /// Development only: simulates the HA Ingress proxy on the local Ingress port.
    /// </summary>
    public bool SimulerIngress { get; set; }

    /// <summary>Utilisateur HA simule en developpement / Simulated HA user in development.</summary>
    public string UtilisateurSimule { get; set; } = "dev";

    // --- Options du module HA / HA add-on options ---

    /// <summary>Noms d'utilisateur HA autorises (vide = personne) / Allowed HA usernames (empty = nobody).</summary>
    public List<string> UtilisateursAutorises { get; set; } = new();

    /// <summary>IP de Nginx Proxy Manager / Nginx Proxy Manager IPs.</summary>
    public List<string> ProxyDeConfiance { get; set; } = new();

    /// <summary>Chemin public (ex. /garderie) / Public path base.</summary>
    public string PathBasePublic { get; set; } = "/garderie";

    public const string PrefixeIngressSimule = "/api/hassio_ingress/dev";

    public string FichierBase => Path.Combine(DataDir, "garderie.db");
    public string DossierDocuments => Path.Combine(DataDir, "docs");
    public string DossierCles => Path.Combine(DataDir, "keys");

    public bool EstProxyDeConfiance(IPAddress? ip) =>
        ip is not null && ProxyDeConfiance.Any(p => IPAddress.TryParse(p, out var a) && Normaliser(a).Equals(Normaliser(ip)));

    public bool EstUtilisateurAutorise(string? nom, string? id) =>
        UtilisateursAutorises.Any(u =>
            (!string.IsNullOrWhiteSpace(nom) && string.Equals(u.Trim(), nom, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(id) && string.Equals(u.Trim(), id, StringComparison.OrdinalIgnoreCase)));

    public static IPAddress Normaliser(IPAddress ip) => ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip;

    /// <summary>
    /// Lit la section "Garderie" puis surcharge avec les cles du module HA.
    /// Reads the "Garderie" section then overrides with the HA add-on keys.
    /// </summary>
    public static GarderieOptions Charger(IConfiguration config)
    {
        var o = config.GetSection("Garderie").Get<GarderieOptions>() ?? new GarderieOptions();
        var users = config.GetSection("utilisateurs_autorises").Get<List<string>>();
        if (users is not null) o.UtilisateursAutorises = users;
        var proxies = config.GetSection("proxy_de_confiance").Get<List<string>>();
        if (proxies is not null) o.ProxyDeConfiance = proxies;
        var pb = config["path_base_public"];
        if (!string.IsNullOrWhiteSpace(pb)) o.PathBasePublic = pb;
        o.PathBasePublic = "/" + o.PathBasePublic.Trim().Trim('/');
        if (o.PathBasePublic == "/") o.PathBasePublic = "";
        return o;
    }
}
