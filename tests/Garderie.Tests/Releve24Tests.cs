using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Garderie.Web.Data;
using Garderie.Web.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Garderie.Tests;

/// <summary>Releve 24 : numerotation, masques, Reglages / RL-24: numbering, masks, settings.</summary>
public class Releve24Tests(Appli appli) : IClassFixture<Appli>
{
    [Theory]
    [InlineData("024000023", "024000034")]   // formulaire 2024-10 : 02400003 % 7 = 4
    [InlineData("024000034", "024000045")]
    [InlineData("024005995", "024006006")]   // 600 : encore permis
    [InlineData("024006006", "024000012")]   // apres 599 : retour a 1
    public void NumeroSuivantCommeLeFormulaire(string actuel, string attendu)
    {
        Assert.Equal(attendu, Releve24.Suivant(actuel));
        Assert.True(Releve24.Valide(attendu));
    }

    [Fact]
    public void ProchainNumero()
    {
        Assert.Equal("024000034", Releve24.Prochain("024000023", null));
        Assert.Equal("024000045", Releve24.Prochain("024000023", "024000034"));   // meme formulaire : on continue
        Assert.Equal("031000012", Releve24.Prochain("031000001", "024000034"));   // nouveau formulaire : on repart
        Assert.False(Releve24.Valide("024000035"));
    }

    [Fact]
    public void MasquesDuFormulaire()
    {
        Assert.Equal("      2023         03       14", Releve24.Masque(Releve24.MasqueDate, "20230314"));
        Assert.Equal("     130            692            544", Releve24.Masque(Releve24.MasqueNas, "130692544"));
        Assert.Equal("   G1A       1A1", Releve24.Masque(Releve24.MasqueCodePostal, "g1a1a1"));
        Assert.Equal("QC", Releve24.Masque(">*", "qc"));
        Assert.Equal("9,540.00", Releve24.Montant(9540m));
        Assert.Equal("0.00", Releve24.Montant(0m));
    }

    private HttpClient Admin()
    {
        var c = appli.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        c.DefaultRequestHeaders.Add("X-Test-Port", "8090");
        c.DefaultRequestHeaders.Add("X-Test-Ip", "172.30.32.2");
        c.DefaultRequestHeaders.Add("X-Remote-User-Name", "rsge");
        return c;
    }

    private static string Jeton(string html) =>
        Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;

    private T Db<T>(Func<GarderieDb, T> f)
    {
        using var scope = appli.Services.CreateScope();
        return f(scope.ServiceProvider.GetRequiredService<GarderieDb>());
    }

    [Fact]
    public async Task ReglagesDuReleve24()
    {
        var admin = Admin();
        async Task Enregistrer(string id) => await admin.PostAsync("/Parametres?handler=Rl24", new FormUrlEncodedContent(
        [
            new("Rl24.Identifiant", id), new("Rl24.Signataire", "Sonia (test), responsable"),
            new("__RequestVerificationToken", Jeton(await admin.GetStringAsync("/Parametres"))),
        ]));

        // Adresse de « La garderie », decoupee ; la ligne complete sert au contrat / Split address, full line for the contract
        await admin.PostAsync("/Parametres", new FormUrlEncodedContent(
        [
            new("Donnees.NomGarderie", "Les petits moussaillons"), new("Donnees.Rl24Numero", "456"), new("Donnees.Rl24Rue", "rue Test"),
            new("Donnees.Rl24Ville", "Québec"), new("Donnees.Rl24Province", "qc"), new("Donnees.Rl24CodePostal", "g2b 2b2"),
            new("__RequestVerificationToken", Jeton(await admin.GetStringAsync("/Parametres"))),
        ]));
        await Enregistrer("130 692 545");   // NAS invalide (cle de Luhn)
        Assert.Null(Db(db => db.Parametres.Find(1)?.Rl24IdentifiantChiffre));
        await Enregistrer("130 692 544");
        var p = Db(db => db.Parametres.Find(1)!);
        Assert.NotNull(p.Rl24IdentifiantChiffre);
        Assert.DoesNotContain("130692544", System.Text.Encoding.Latin1.GetString(p.Rl24IdentifiantChiffre!));   // chiffre
        Assert.Equal(("QC", "G2B2B2", "rue Test"), (p.Rl24Province, p.Rl24CodePostal, p.Rl24Rue));
        Assert.Equal("456 rue Test, Québec QC G2B 2B2", p.Adresse);
        var page = await admin.GetStringAsync("/Parametres");
        Assert.DoesNotContain("130692544", page);   // jamais affiche en clair
        Assert.Contains("2544", page);

        // Un PDF qui n'est pas le formulaire de Revenu Quebec est refuse / A non-RQ PDF is refused
        var form = new MultipartFormDataContent { { new StringContent(Jeton(page)), "__RequestVerificationToken" }, { new StringContent("releve"), "quel" } };
        var f = new ByteArrayContent(ModeleTest.Creer());
        f.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(f, "fichier", "rl24.pdf");
        await admin.PostAsync("/Parametres?handler=FormulaireRl24", form);
        var options = appli.Services.GetRequiredService<Garderie.Web.Configuration.GarderieOptions>();
        Assert.False(File.Exists(options.FichierRl24));

        // Le vrai formulaire (local seulement) est accepte / The real form (local only) is accepted
        if (Environment.GetEnvironmentVariable("GARDERIE_RL24_DIR") is not { } dir) return;
        form = new MultipartFormDataContent { { new StringContent(Jeton(await admin.GetStringAsync("/Parametres"))), "__RequestVerificationToken" }, { new StringContent("releve"), "quel" } };
        f = new ByteArrayContent(File.ReadAllBytes(Path.Combine(dir, "rl24.pdf")));
        f.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(f, "fichier", "rl24.pdf");
        await admin.PostAsync("/Parametres?handler=FormulaireRl24", form);
        Assert.True(File.Exists(options.FichierRl24));
        Assert.Contains("024000034", await admin.GetStringAsync("/Parametres"));
        form = new MultipartFormDataContent { { new StringContent(Jeton(await admin.GetStringAsync("/Parametres"))), "__RequestVerificationToken" }, { new StringContent("sommaire"), "quel" } };
        f = new ByteArrayContent(File.ReadAllBytes(Path.Combine(dir, "rl24s.pdf")));
        f.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(f, "fichier", "rl24s.pdf");
        await admin.PostAsync("/Parametres?handler=FormulaireRl24", form);

        // Apercus : PDF remplis, aucun numero reserve / Previews: filled PDFs, no number reserved
        foreach (var quel in new[] { "releve", "sommaire" })
        {
            var r = await admin.GetAsync($"/Parametres?handler=ApercuRl24&quel={quel}");
            Assert.Equal("application/pdf", r.Content.Headers.ContentType?.MediaType);
            if (Environment.GetEnvironmentVariable("GARDERIE_PDF_DIR") is { } sortie)
                File.WriteAllBytes(Path.Combine(sortie, $"apercu_{quel}.pdf"), await r.Content.ReadAsByteArrayAsync());
        }
        Assert.Null(Db(db => db.Parametres.Find(1)!.Rl24DernierNumero));
    }
}
