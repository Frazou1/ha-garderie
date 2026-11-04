using System.Security.Cryptography;
using Garderie.Web.Configuration;
using Garderie.Web.Data;
using Garderie.Web.Securite;

namespace Garderie.Web.Services;

/// <summary>
/// Documents de l'enfant : verification du vrai type (signature du fichier), chiffrement sur disque.
/// Child documents: real type check (magic bytes), encrypted on disk.
/// </summary>
public sealed class DocumentsService(GarderieOptions options, Chiffreur chiffreur)
{
    public const long TailleMax = 15 * 1024 * 1024;

    /// <summary>Type MIME reconnu d'apres les premiers octets, sinon null / MIME type from magic bytes, else null.</summary>
    public static string? DetecterType(ReadOnlySpan<byte> d)
    {
        if (d.Length >= 5 && d[..5].SequenceEqual("%PDF-"u8)) return "application/pdf";
        if (d.Length >= 3 && d[0] == 0xFF && d[1] == 0xD8 && d[2] == 0xFF) return "image/jpeg";
        if (d.Length >= 8 && d[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })) return "image/png";
        if (d.Length >= 12 && d.Slice(4, 4).SequenceEqual("ftyp"u8))
        {
            var marque = d.Slice(8, 4);
            if (marque.SequenceEqual("heic"u8) || marque.SequenceEqual("heix"u8) || marque.SequenceEqual("mif1"u8) || marque.SequenceEqual("msf1"u8))
                return "image/heic";
        }
        return null;
    }

    public sealed record Resultat(Document? Document, string? Erreur);

    public async Task<Resultat> EnregistrerAsync(int enfantId, TypeDocument type, IFormFile fichier, string? note)
    {
        if (fichier.Length == 0) return new(null, "Le fichier est vide.");
        if (fichier.Length > TailleMax) return new(null, "Le fichier dépasse 15 Mo.");

        byte[] contenu;
        using (var ms = new MemoryStream())
        {
            await fichier.CopyToAsync(ms);
            contenu = ms.ToArray();
        }
        var mime = DetecterType(contenu);
        if (mime is null) return new(null, "Type de fichier refusé (PDF, JPG, PNG ou HEIC seulement).");
        if (type == TypeDocument.Photo && mime is not ("image/jpeg" or "image/png"))
            return new(null, "La photo doit être en JPG ou PNG.");

        var nom = Guid.NewGuid().ToString("N") + ".bin";
        Directory.CreateDirectory(options.DossierDocuments);
        var chiffre = chiffreur.Chiffrer(contenu, Contextes.Document(nom));
        await File.WriteAllBytesAsync(Path.Combine(options.DossierDocuments, nom), chiffre);

        var nomOriginal = Path.GetFileName(fichier.FileName);
        if (nomOriginal.Length > 200) nomOriginal = nomOriginal[^200..];
        return new(new Document
        {
            EnfantId = enfantId,
            Type = type,
            NomOriginal = nomOriginal,
            TypeContenu = mime,
            Taille = contenu.Length,
            Sha256 = Convert.ToHexStringLower(SHA256.HashData(contenu)),
            Fichier = nom,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            TeleverseLe = DateTime.UtcNow,
        }, null);
    }

    public async Task<byte[]> LireAsync(Document doc)
    {
        var chiffre = await File.ReadAllBytesAsync(Path.Combine(options.DossierDocuments, doc.Fichier));
        return chiffreur.Dechiffrer(chiffre, Contextes.Document(doc.Fichier));
    }

    public void Supprimer(Document doc)
    {
        var chemin = Path.Combine(options.DossierDocuments, doc.Fichier);
        if (File.Exists(chemin)) File.Delete(chemin);
    }
}
