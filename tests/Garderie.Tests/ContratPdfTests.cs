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
        FermeturesDetail = [.. Feries.Calculer(2027).Where(f => f.Date >= new DateOnly(2026, 9, 1) && f.Date <= new DateOnly(2027, 8, 31)).Select(f => new FermetureContrat(f.Nom, f.Date, f.Date, null)),
            new FermetureContrat("Relâche", new DateOnly(2027, 3, 1), new DateOnly(2027, 3, 5), null)],
        ObligationsRsge = TextesContrat.ObligationsRsge, ObligationsParent = TextesContrat.ObligationsParent, MentionLpc = TextesContrat.MentionLpc,
        Attestation = TextesContrat.Remplir(TextesContrat.Attestation, "Les petits moussaillons", "Sonia (test)"), NonRespect = TextesContrat.NonRespect,
        Ville = "Québec", Debut = new DateOnly(2026, 9, 1), Fin = new DateOnly(2027, 8, 31),
        // Modele du bureau coordonnateur / Coordinating office template
        RsgeCourriel = "rsge@exemple.test", ParentCourriel = "julie@exemple.test", EnfantPortail = "123456",
        Services = TextesContrat.Services, HeureCollationMatin = "9 h 00", HeureCollationPm = "15 h 00", HeureDiner = "11 h 00",
        Frequentation = ["Lundi", "Mardi", "Mercredi", "Jeudi", "Vendredi (matin)"], DebutFrequentation = new DateOnly(2026, 9, 1),
        Tarif = 45m, JourPaiement = "mercredi", ModesPaiement = TextesContrat.ModesPaiement, Versement = 202.5m, Total = 10_440m,
        FraisRetard = "5 $ pour les 10 premières minutes et ensuite 1 $ la minute", ClausesFermeture = TextesContrat.ClausesFermeture,
        ResiliationRsge = TextesContrat.ResiliationRsge, ResiliationParent = TextesContrat.ResiliationParent,
        EnfantPrenom = "Léa", EnfantNomFamille = "Tremblay", ParentPrenom = "Julie", ParentNomFamille = "Tremblay",
        ParentTelephoneAutre = "418-555-0199", HeureOuverture = "7 h 30", HeureFermeture = "17 h", FraisNsf = "25,00",
    };

    [Fact]
    public void ChampsDuModeleBc()
    {
        Assert.Equal(("9165 rue Drolet, Québec (Québec)", "G2K 1J4"), ContratBcPdf.Adresse("9165 rue Drolet, Québec (Québec) g2k1j4"));
        Assert.Equal(("9040 rue Chauvet", null), ContratBcPdf.Adresse("9040 rue Chauvet"));
        Assert.Equal(("Sonia", "Savard"), ContratBcPdf.Couper("Sonia Savard"));
        // Reglages vides : montant et minutes lus dans le texte de la regie / Parsed from the rules text
        Assert.Equal(("5", "10"), ContratBcPdf.Retard(Exemple()));
        Assert.Equal(("5,00", "5"), ContratBcPdf.Retard(Exemple() with { RetardMontant = "5,00 $", RetardMinutes = "5" }));
    }

    [Fact]
    public void FrequentationEtJoursPrevus()
    {
        var lundi = new DateOnly(2026, 11, 2);
        // Sans horaire : lundi au vendredi / No schedule: Monday to Friday
        var (jours, parSemaine) = ContratsService.Frequentation([], lundi);
        Assert.Equal(["Lundi", "Mardi", "Mercredi", "Jeudi", "Vendredi"], jours);
        Assert.Equal(5m, parSemaine);
        Assert.Equal(10m, ContratsService.JoursPrevus([], lundi, lundi.AddDays(13)));
        // Horaire : lundi complet, mercredi matin / Schedule: full Monday, Wednesday morning
        List<HoraireType> h =
        [
            new() { DateEffective = lundi, Jour = DayOfWeek.Monday, Matin = true, Pm = true },
            new() { DateEffective = lundi, Jour = DayOfWeek.Wednesday, Matin = true },
        ];
        (jours, parSemaine) = ContratsService.Frequentation(h, lundi);
        Assert.Equal(["Lundi", "Mercredi (matin)"], jours);
        Assert.Equal(1.5m, parSemaine);
        Assert.Equal(3m, ContratsService.JoursPrevus(h, lundi, lundi.AddDays(13)));
    }

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
            // Rendu local pour verification visuelle / Local render for visual check
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
        Assert.Equal("2026-05-18", f["Fête des patriotes"]);
        Assert.Equal("2026-09-07", f["Fête du travail"]);
        Assert.Equal("2026-10-12", f["Action de grâce"]);
    }
}
