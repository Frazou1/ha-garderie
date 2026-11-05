using System.IO.Compression;
using System.Text;
using Garderie.Web.Services;

namespace Garderie.Tests;

/// <summary>Regie interne : import du Word et PDF genere / Internal rules: Word import and generated PDF.</summary>
public class RegieTests
{
    private const string Ns = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    /// <summary>Petit .docx synthetique / Small synthetic .docx.</summary>
    private static byte[] Docx(string corps)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            using var s = zip.CreateEntry("word/document.xml").Open();
            var xml = $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><w:document xmlns:w=\"{Ns}\"><w:body>{corps}</w:body></w:document>";
            s.Write(Encoding.UTF8.GetBytes(xml));
        }
        return ms.ToArray();
    }

    private static string R(string texte, bool gras = false, bool souligne = false, int taille = 24) =>
        $"<w:r><w:rPr>{(gras ? "<w:b/>" : "")}{(souligne ? "<w:u w:val=\"single\"/>" : "")}<w:sz w:val=\"{taille}\"/></w:rPr><w:t xml:space=\"preserve\">{texte}</w:t></w:r>";

    private static string P(string runs, string? jc = null, bool liste = false) =>
        $"<w:p><w:pPr>{(liste ? "<w:numPr><w:ilvl w:val=\"0\"/><w:numId w:val=\"1\"/></w:numPr>" : "")}{(jc is null ? "" : $"<w:jc w:val=\"{jc}\"/>")}</w:pPr>{runs}</w:p>";

    [Fact]
    public void ImporteLeWord()
    {
        var docx = Docx(
            P(R("Régie Interne", gras: true, taille: 40)) +
            P("") +
            P(R("Sonia Savard", gras: true, taille: 32), jc: "center") +
            P(R("Dispositions générales", gras: true, taille: 30)) +
            P(R("Heures d’ouvertures", gras: true, souligne: true), jc: "both") +
            P(R("Payables", gras: true) + R(" : les journées ") + R("d’absences", gras: true) + R(".")) +
            P(R("Jour de l’An"), liste: true) +
            P(R("Le parent doit fournir :", gras: true), jc: "center"));
        var texte = RegiePdf.ImporterDocx(docx);
        Assert.Equal(
            "# Régie Interne\n\n> ## Sonia Savard\n## Dispositions générales\n### Heures d’ouvertures\n" +
            "**Payables** : les journées **d’absences**.\n- Jour de l’An\n> **Le parent doit fournir :**\n", texte);
        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(RegiePdf.Generer(texte, "Sonia", "Garderie", DateTime.UtcNow), 0, 4));
    }

    [Fact]
    public void RefuseCeQuiNEstPasUnWord()
    {
        Assert.Throws<InvalidDataException>(() => RegiePdf.ImporterDocx(Encoding.ASCII.GetBytes("%PDF-1.7 pas un Word")));
        Assert.Throws<InvalidDataException>(() => RegiePdf.ImporterDocx(Docx("").Take(30).ToArray()));
    }

    [Fact]
    public void RemplaceLesChamps()
    {
        // {rsge} et {mise_a_jour} : le PDF se genere sans erreur / Placeholders: renders fine
        var pdf = RegiePdf.Generer("# Régie\n> {rsge}\n> Mise à jour {mise_a_jour}\n\nTexte  avec  espaces et **gras", "Sonia", null, new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc));
        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
    }

    [Fact]
    public void RenduLocalDuVraiWord()
    {
        // Rendu local seulement (le Word de la RSGE n'est jamais dans le depot) / Local render only (never in the repo)
        if (Environment.GetEnvironmentVariable("GARDERIE_REGIE_DOCX") is not { } docx || Environment.GetEnvironmentVariable("GARDERIE_PDF_DIR") is not { } dir) return;
        if (Environment.GetEnvironmentVariable("GARDERIE_POLICES") is { } polices) PolicesPdf.DossierPolices = polices;
        var texte = RegiePdf.ImporterDocx(File.ReadAllBytes(docx));
        File.WriteAllText(Path.Combine(dir, "regie_importee.txt"), texte);
        File.WriteAllBytes(Path.Combine(dir, "regie_test.pdf"), RegiePdf.Generer(texte, "Sonia Savard", "Les petits moussaillons", DateTime.UtcNow));
    }
}
