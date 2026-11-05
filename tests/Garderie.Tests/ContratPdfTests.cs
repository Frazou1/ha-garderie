using Garderie.Web.Data;
using Garderie.Web.Services;

namespace Garderie.Tests;

public class ContratPdfTests
{
    public static InfosContrat Exemple() => new()
    {
        Garderie = "Les petits moussaillons", Rsge = "Sonia (test)", AdresseGarderie = "123 rue Exemple, Québec", TelephoneGarderie = "418-555-0000",
        Heures = "de 7 h 30 à 17 h", EnfantNom = "Léa Tremblay", EnfantAdresse = "1 rue Test", EnfantTelephone = "418-555-0110",
        EnfantNaissance = new DateOnly(2023, 3, 14), EnfantSexe = Sexe.F, ParentNom = "Julie Tremblay", ParentAdresse = "1 rue Test",
        ParentTelephone1 = "418-555-0110", ParentLien = "Mère",
        Fermetures = ["Jour de l'an : 2027-01-01", "Relâche : du 2027-03-01 au 2027-03-05"], NoteVacances = "Semaine des fêtes à confirmer.",
        ObligationsRsge = TextesContrat.ObligationsRsge, ObligationsParent = TextesContrat.ObligationsParent, MentionLpc = TextesContrat.MentionLpc,
        Attestation = TextesContrat.Remplir(TextesContrat.Attestation, "Les petits moussaillons", "Sonia (test)"), NonRespect = TextesContrat.NonRespect,
        Ville = "Québec", Debut = new DateOnly(2026, 9, 1), Fin = new DateOnly(2027, 8, 31),
    };

    [Fact]
    public void GenereContratEtResiliation()
    {
        var png = FichesService.LireSignature(ModeleTest.Png);
        var s = new SignaturesContrat("130692544", png, new DateOnly(2026, 8, 20), "Julie Tremblay", png, new DateOnly(2026, 8, 21),
            new DateOnly(2027, 1, 15), png, new DateOnly(2027, 1, 10), "Julie Tremblay");
        var contrat = ContratPdf.Contrat(Exemple(), s);
        var resiliation = ContratPdf.Resiliation(Exemple(), s);
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(contrat, 0, 4));
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(resiliation, 0, 4));
        if (Environment.GetEnvironmentVariable("GARDERIE_PDF_DIR") is { } dir)
        {
            File.WriteAllBytes(Path.Combine(dir, "contrat_test.pdf"), contrat);
            File.WriteAllBytes(Path.Combine(dir, "resiliation_test.pdf"), resiliation);
        }
    }

    [Theory]
    [InlineData(2026, "2026-04-05")]
    [InlineData(2027, "2027-03-28")]
    public void Paques(int annee, string attendu) => Assert.Equal(DateOnly.Parse(attendu), Feries.Paques(annee));

    [Fact]
    public void FeriesDuModele2026()
    {
        var f = Feries.Calculer(2026).ToDictionary(x => x.Nom, x => x.Date.ToString("yyyy-MM-dd"));
        Assert.Equal("2026-04-03", f["Vendredi saint"]);
        Assert.Equal("2026-05-18", f["Journée nationale des patriotes"]);
        Assert.Equal("2026-09-07", f["Fête du Travail"]);
        Assert.Equal("2026-10-12", f["Action de grâce"]);
    }
}
