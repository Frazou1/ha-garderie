using Garderie.Web.Services;
using Champ = Garderie.Web.Services.FormulaireProtege.Champ;

namespace Garderie.Tests;

/// <summary>
/// Remplissage des formulaires officiels proteges (RL-24). Les PDF de Revenu Quebec ne sont pas dans le depot :
/// test local avec GARDERIE_RL24_DIR (rl24.pdf, rl24s.pdf) et GARDERIE_PDF_DIR pour la sortie.
/// Protected official forms filling; Revenu Quebec PDFs are not in the repo (local test only).
/// </summary>
public class FormulaireProtegeTests
{
    [Fact]
    public void RemplitLeRelevé24Fictif()
    {
        if (Environment.GetEnvironmentVariable("GARDERIE_RL24_DIR") is not { } dir) return;
        var modele = File.ReadAllBytes(Path.Combine(dir, "rl24.pdf"));
        Assert.Equal("024000023", FormulaireProtege.LireValeurs(modele)["numSeq"]);
        var v = new Dictionary<string, Champ>
        {
            ["annee"] = new("2026", " 2026"), ["codeReleve"] = new("1", "      R"), ["numSeq"] = new("024000034"),
            ["nom.1"] = new("Tremblay"), ["prenom.1"] = new("Léa"), ["date.1"] = new("20230314", "      2023         03       14"),
            ["nbJours.1"] = new("212"), ["fraisTot.1"] = new("9540.00", "9,540.00"), ["fraisProv.1"] = new("0.00", "0.00"), ["fraisFed.1"] = new("9540.00", "9,540.00"),
            ["nomPayeur"] = new("Tremblay"), ["prenomPayeur"] = new("Julie"), ["noPayeur"] = new("123"), ["ruePayeur"] = new("rue de l'Exemple"),
            ["villePayeur"] = new("Québec"), ["provincePayeur"] = new("QC"), ["codePPayeur"] = new("G1A1A1", "   G1A       1A1"),
            ["nas"] = new("130692544", "     130            692            544"), ["neqOuNas"] = new("1234567890"),
            ["nomService"] = new("Les petits moussaillons"), ["nom2Service"] = new("Sonia (test)"), ["noService"] = new("456"),
            ["rueService"] = new("rue Test"), ["villeService"] = new("Québec"), ["provinceService"] = new("QC"), ["codePService"] = new("G2B2B2", "   G2B       2B2"),
            ["rep_annee"] = new("2026"), ["rep_codeReleve"] = new("R"), ["rep_nom.1"] = new("Tremblay"), ["rep_prenom.1"] = new("Léa"),
            ["rep_date.1"] = new("20230314", "      2023         03       14"), ["rep_nbJours.1"] = new("212"),
            ["rep_fraisTot.1"] = new("9540.00", "9,540.00"), ["rep_fraisProv.1"] = new("0.00", "0.00"), ["rep_fraisFed.1"] = new("9540.00", "9,540.00"),
            ["rep_nas"] = new("130692544"), ["rep_neqOuNas"] = new("1234567890"),
            ["rep_payeur"] = new("Tremblay Julie\n123 rue de l'Exemple\nQuébec QC G1A 1A1"),
            ["rep_service"] = new("Les petits moussaillons\nSonia (test)\n456 rue Test\nQuébec QC G2B 2B2"),
        };
        var rempli = FormulaireProtege.Remplir(modele, v);
        Assert.Equal(modele, rempli.Take(modele.Length).ToArray());   // original intact (mise a jour incrementale)
        var ajout = System.Text.Encoding.Latin1.GetString(rempli, modele.Length, rempli.Length - modele.Length);
        Assert.Contains("/Type /XRef", ajout);
        Assert.DoesNotContain("Tremblay", ajout);   // valeurs chiffrees / values encrypted
        Assert.EndsWith("%%EOF\n", ajout);
        // Verifie a l'exterieur avec qpdf, pypdf et PDFium (PDFsharp ne relit pas les mises a jour de PDF chiffres)
        // Checked externally with qpdf, pypdf and PDFium (PDFsharp cannot re-read updates of encrypted PDFs)
        if (Environment.GetEnvironmentVariable("GARDERIE_PDF_DIR") is { } sortie)
            File.WriteAllBytes(Path.Combine(sortie, "rl24_rempli.pdf"), rempli);
    }

    [Fact]
    public void RemplitLeSommaire24Fictif()
    {
        if (Environment.GetEnvironmentVariable("GARDERIE_RL24_DIR") is not { } dir) return;
        var rempli = FormulaireProtege.Remplir(File.ReadAllBytes(Path.Combine(dir, "rl24s.pdf")), new Dictionary<string, Champ>
        {
            ["annee"] = new("2026"), ["nom"] = new("Les petits moussaillons"), ["adresse_1"] = new("456 rue Test, Québec (Québec)"),
            ["cp"] = new("G2B2B2"), ["neq"] = new("1234567890"), ["nombre"] = new("1"), ["nb4"] = new("1"), ["nb5"] = new("212"),
            ["case_e"] = new("9540.00", "9,540.00"), ["titre"] = new("SONIA (TEST), RESPONSABLE"), ["date"] = new("20270215"),
            ["telephone"] = new("4185550000"),
        });
        if (Environment.GetEnvironmentVariable("GARDERIE_PDF_DIR") is { } sortie)
            File.WriteAllBytes(Path.Combine(sortie, "rl24s_rempli.pdf"), rempli);
    }

    [Fact]
    public void RefuseUnPdfNonProtege()
    {
        Assert.Throws<InvalidDataException>(() => FormulaireProtege.Remplir(ModeleTest.Creer(), new Dictionary<string, Champ>()));
    }
}
