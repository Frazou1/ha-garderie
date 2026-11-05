using System.Text;
using Garderie.Web.Data;
using Garderie.Web.Services;

namespace Garderie.Tests;

/// <summary>Regie interne generee : sections, valeurs des Reglages, PDF / Generated internal rules.</summary>
public class RegieTests
{
    private static Parametres Reglages() => new()
    {
        NomRsge = "Sonia (test)", NomGarderie = "Les petits moussaillons",
        HeureOuverture = new TimeOnly(7, 30), HeureFermeture = new TimeOnly(17, 0),
        RegieTarif = "45 $", RegieJourPaiement = "mercredi", RegieFraisRetard = "5 $ pour les 10 premières minutes",
        RegieModifieeLe = new DateTime(2026, 1, 15, 17, 0, 0, DateTimeKind.Utc),
    };

    [Fact]
    public void ComposeAvecLesValeursDesReglages()
    {
        var t = RegieInterne.Composer(Reglages());
        Assert.DoesNotContain("{", t);   // tous les champs remplaces / all placeholders filled
        Assert.StartsWith("# Régie Interne\n\n> ## Sonia (test)\n> ## Mise à jour Janvier 2026\n## Dispositions", t);
        Assert.Contains("de 7h30 à 17h00", t);
        Assert.Contains("45 $ par jour par enfant.", t);
        Assert.Contains("le **mercredi** de chaque semaine", t);
        Assert.Contains("après 17h00.", t);
        Assert.Contains("Les frais de retard sont de 5 $ pour les 10 premières minutes.", t);
        Assert.Contains("**douze (12) journées fériées**", t);
        Assert.Contains("- Veille de Noël (24 décembre)", t);
        Assert.Contains("Office de la protection du consommateur", t);   // mention LPC du contrat
        Assert.Contains("[saut de page]\n## Annexe concernant la Régie interne\n\n### Covid-19\n", t);   // annexe sur sa page
        Assert.EndsWith("- Maux de ventre\n", t);
        Assert.Equal(RegieInterne.Sections.Length, RegieInterne.Sections.Select(s => s.Cle).Distinct().Count());
    }

    [Fact]
    public void ValeursManquantesEtSectionsModifiees()
    {
        var p = new Parametres();
        var saisies = RegieInterne.Sections.ToDictionary(s => s.Cle, s => (string?)s.Texte);
        saisies["covid-19"] = "Voir l'annexe.\r\n";
        saisies["milieu-sans-fumee"] = RegieInterne.Retiree;
        saisies["clientele"] = "";   // vide = texte par defaut / empty = default
        p.RegieSections = RegieInterne.Ecrire(saisies);
        Assert.Equal(["covid-19", "milieu-sans-fumee"], RegieInterne.Lire(p.RegieSections).Keys.Order());

        var t = RegieInterne.Composer(p);
        Assert.Contains("### Covid 19\nVoir l'annexe.\n", t);
        Assert.DoesNotContain("Milieu sans fumée", t);
        Assert.Contains("6 mois à 18 mois", t);
        Assert.Contains("________ par jour par enfant.", t);   // tarif absent : ligne a remplir
        Assert.Null(RegieInterne.Ecrire(RegieInterne.Sections.ToDictionary(s => s.Cle, s => (string?)s.Texte)));
    }

    [Fact]
    public void GenereLePdf()
    {
        var pdf = RegieInterne.Pdf(Reglages());
        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
        if (Environment.GetEnvironmentVariable("GARDERIE_PDF_DIR") is not { } dir) return;
        File.WriteAllBytes(Path.Combine(dir, "regie_test.pdf"), pdf);
        File.WriteAllText(Path.Combine(dir, "regie_composee.txt"), RegieInterne.Composer(Reglages()));
    }
}
