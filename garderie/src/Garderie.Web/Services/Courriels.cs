using Garderie.Web.Configuration;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Garderie.Web.Services;

/// <summary>
/// Envoi des courriels par SMTP (ex. Gmail + mot de passe d'application, saisi dans les options du module).
/// En developpement / tests : fichiers .eml dans un dossier au lieu d'un envoi.
/// Sends emails over SMTP; in development / tests writes .eml files instead.
/// </summary>
public sealed class Courriels(GarderieOptions options, ILogger<Courriels> log)
{
    public bool EstConfigure => options.CourrielConfigure;

    public async Task EnvoyerAsync(string destinataire, string sujet, string texte, IEnumerable<(string Nom, byte[] Contenu)>? piecesJointes = null)
    {
        var m = new MimeMessage();
        var expediteur = string.IsNullOrWhiteSpace(options.SmtpUtilisateur) ? "garderie@exemple.test" : options.SmtpUtilisateur;
        m.From.Add(new MailboxAddress(options.SmtpNomExpediteur, expediteur));
        m.To.Add(MailboxAddress.Parse(destinataire));
        m.Subject = sujet;
        var corps = new BodyBuilder { TextBody = texte };
        foreach (var (nom, contenu) in piecesJointes ?? []) corps.Attachments.Add(nom, contenu, new ContentType("application", "pdf"));
        m.Body = corps.ToMessageBody();

        if (options.DossierCourrielsTest is { } dossier)
        {
            Directory.CreateDirectory(dossier);
            await m.WriteToAsync(Path.Combine(dossier, $"{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}.eml"));
            return;
        }

        using var smtp = new SmtpClient();
        await smtp.ConnectAsync(options.SmtpHote, options.SmtpPort, SecureSocketOptions.Auto);
        await smtp.AuthenticateAsync(options.SmtpUtilisateur, options.SmtpMotDePasse);
        await smtp.SendAsync(m);
        await smtp.DisconnectAsync(true);
        // Sans sujet ni destinataire : les journaux du module sont visibles dans HA (aucune donnee d'enfant)
        // No subject or recipient: add-on logs are visible in HA (no child data)
        log.LogInformation("Courriel envoye");
    }
}
