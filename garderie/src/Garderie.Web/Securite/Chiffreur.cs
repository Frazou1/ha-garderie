using System.Security.Cryptography;
using System.Text;
using Garderie.Web.Configuration;

namespace Garderie.Web.Securite;

/// <summary>
/// Chiffrement au repos AES-256-GCM (NAS, NAM, documents). Cle maitresse aleatoire creee au premier
/// demarrage dans /data/keys/master.key, a part de la base. Format : [version 1][nonce 12][tag 16][chiffre].
/// Le « contexte » (ex. "nas:parent") est lie au chiffre (donnees associees) : un chiffre copie dans un
/// autre champ ne se dechiffre pas.
/// Encryption at rest AES-256-GCM. Random master key created on first start, kept apart from the db.
/// The context string is bound as associated data.
/// </summary>
public sealed class Chiffreur
{
    private const byte Version = 1;
    private const int TailleNonce = 12, TailleTag = 16, TailleCle = 32;
    private readonly byte[] _cle;

    public Chiffreur(GarderieOptions options) : this(ChargerOuCreerCle(options.DossierCles)) { }

    public Chiffreur(byte[] cle)
    {
        if (cle.Length != TailleCle) throw new ArgumentException("Cle AES-256 attendue / AES-256 key expected");
        _cle = cle;
    }

    public byte[] Chiffrer(ReadOnlySpan<byte> clair, string contexte)
    {
        var sortie = new byte[1 + TailleNonce + TailleTag + clair.Length];
        sortie[0] = Version;
        var nonce = sortie.AsSpan(1, TailleNonce);
        var tag = sortie.AsSpan(1 + TailleNonce, TailleTag);
        var chiffre = sortie.AsSpan(1 + TailleNonce + TailleTag);
        RandomNumberGenerator.Fill(nonce);
        using var aes = new AesGcm(_cle, TailleTag);
        aes.Encrypt(nonce, clair, chiffre, tag, Encoding.UTF8.GetBytes(contexte));
        return sortie;
    }

    public byte[] Dechiffrer(ReadOnlySpan<byte> donnees, string contexte)
    {
        if (donnees.Length < 1 + TailleNonce + TailleTag || donnees[0] != Version)
            throw new CryptographicException("Format chiffre inconnu / Unknown ciphertext format");
        var nonce = donnees.Slice(1, TailleNonce);
        var tag = donnees.Slice(1 + TailleNonce, TailleTag);
        var chiffre = donnees[(1 + TailleNonce + TailleTag)..];
        var clair = new byte[chiffre.Length];
        using var aes = new AesGcm(_cle, TailleTag);
        aes.Decrypt(nonce, chiffre, tag, clair, Encoding.UTF8.GetBytes(contexte));
        return clair;
    }

    public byte[]? ChiffrerTexte(string? texte, string contexte) =>
        string.IsNullOrWhiteSpace(texte) ? null : Chiffrer(Encoding.UTF8.GetBytes(texte.Trim()), contexte);

    public string? DechiffrerTexte(byte[]? donnees, string contexte) =>
        donnees is null ? null : Encoding.UTF8.GetString(Dechiffrer(donnees, contexte));

    private static byte[] ChargerOuCreerCle(string dossier)
    {
        Directory.CreateDirectory(dossier);
        var fichier = Path.Combine(dossier, "master.key");
        if (File.Exists(fichier))
        {
            var cle = Convert.FromBase64String(File.ReadAllText(fichier).Trim());
            if (cle.Length != TailleCle) throw new InvalidOperationException("master.key invalide / invalid master.key");
            return cle;
        }
        var nouvelle = RandomNumberGenerator.GetBytes(TailleCle);
        File.WriteAllText(fichier, Convert.ToBase64String(nouvelle));
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(fichier, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        return nouvelle;
    }
}

/// <summary>Contextes de chiffrement / Encryption contexts.</summary>
public static class Contextes
{
    public const string NasParent = "nas:parent";
    public const string NamEnfant = "nam:enfant";
    public const string NasContrat = "nas:contrat";
    public const string IdentifiantRl24 = "id:rl24";
    public static string Document(string fichier) => "doc:" + fichier;
}
