using Garderie.Web.Configuration;
using Garderie.Web.Data;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using MimeKit;

namespace Garderie.Web.Services;

/// <summary>
/// Envoi des courriels par SMTP (ex. Gmail + mot de passe d'application, saisi dans les options du module).
/// En developpement / tests : fichiers .eml dans un dossier au lieu d'un envoi.
/// Sends emails over SMTP; in development / tests writes .eml files instead.
/// </summary>
public sealed class Courriels(GarderieOptions options, IServiceScopeFactory portees, ILogger<Courriels> log)
{
    public bool EstConfigure => options.CourrielConfigure;

    public async Task EnvoyerAsync(string destinataire, string sujet, string texte, IEnumerable<(string Nom, byte[] Contenu)>? piecesJointes = null)
    {
        var m = new MimeMessage();
        var expediteur = string.IsNullOrWhiteSpace(options.SmtpUtilisateur) ? "garderie@exemple.test" : options.SmtpUtilisateur;
        m.From.Add(new MailboxAddress(options.SmtpNomExpediteur, expediteur));
        m.To.Add(MailboxAddress.Parse(destinataire));
        // Les reponses des parents vont au courriel de la RSGE (Reglages), pas au compte d'envoi
        // Parents' replies go to the RSGE's email (Settings), not to the sending account
        if (await CourrielRsgeAsync() is { } rsge && !rsge.Equals(expediteur, StringComparison.OrdinalIgnoreCase)
            && MailboxAddress.TryParse(rsge, out var reponse))
            m.ReplyTo.Add(new MailboxAddress(options.SmtpNomExpediteur, reponse.Address));
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

    private async Task<string?> CourrielRsgeAsync()
    {
        using var portee = portees.CreateScope();
        var db = portee.ServiceProvider.GetRequiredService<GarderieDb>();
        var c = (await db.Parametres.AsNoTracking().FirstOrDefaultAsync())?.CourrielRsge;
        return string.IsNullOrWhiteSpace(c) ? null : c.Trim();
    }
}
