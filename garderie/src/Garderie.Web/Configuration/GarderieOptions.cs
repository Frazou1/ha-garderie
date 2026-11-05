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

    /// <summary>Adresse publique complete des pages des parents (liens par courriel) / Public base URL.</summary>
    public string UrlPublique { get; set; } = "";

    // --- Courriel (SMTP, ex. Gmail + mot de passe d'application) / Email (SMTP) ---
    public string SmtpHote { get; set; } = "smtp.gmail.com";
    public int SmtpPort { get; set; } = 587;
    public string SmtpUtilisateur { get; set; } = "";
    public string SmtpMotDePasse { get; set; } = "";
    /// <summary>Nom affiche de l'expediteur / Sender display name.</summary>
    public string SmtpNomExpediteur { get; set; } = "Garderie";
    /// <summary>
    /// Developpement / tests : ecrit les courriels en fichiers .eml dans ce dossier au lieu de les envoyer.
    /// Development / tests: writes emails as .eml files instead of sending.
    /// </summary>
    public string? DossierCourrielsTest { get; set; }

    public bool CourrielConfigure => DossierCourrielsTest is not null ||
        (!string.IsNullOrWhiteSpace(SmtpHote) && !string.IsNullOrWhiteSpace(SmtpUtilisateur) && !string.IsNullOrWhiteSpace(SmtpMotDePasse));

    public const string PrefixeIngressSimule = "/api/hassio_ingress/dev";

    public string FichierBase => Path.Combine(DataDir, "garderie.db");
    public string DossierDocuments => Path.Combine(DataDir, "docs");
    public string DossierCles => Path.Combine(DataDir, "keys");
    public string FichierModeleFo0310 => Path.Combine(DataDir, "modeles", "FO-0310.pdf");
    public string FichierRegieInterne => Path.Combine(DataDir, "modeles", "regie-interne.pdf");
    /// <summary>Polices du contrat televersees (jamais dans le depot) / Uploaded contract fonts (never in the repo).</summary>
    public string DossierPolices => Path.Combine(DataDir, "modeles", "polices");

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
        string? Lire(string cle) => string.IsNullOrWhiteSpace(config[cle]) ? null : config[cle]!.Trim();
        o.UrlPublique = Lire("url_publique") ?? o.UrlPublique;
        o.SmtpHote = Lire("smtp_hote") ?? o.SmtpHote;
        if (int.TryParse(config["smtp_port"], out var port) && port > 0) o.SmtpPort = port;
        o.SmtpUtilisateur = Lire("smtp_utilisateur") ?? o.SmtpUtilisateur;
        o.SmtpMotDePasse = config["smtp_mot_de_passe"] ?? o.SmtpMotDePasse;
        o.SmtpNomExpediteur = Lire("smtp_nom_expediteur") ?? o.SmtpNomExpediteur;
        o.UrlPublique = o.UrlPublique.TrimEnd('/');
        o.PathBasePublic = "/" + o.PathBasePublic.Trim().Trim('/');
        if (o.PathBasePublic == "/") o.PathBasePublic = "";
        return o;
    }
}
