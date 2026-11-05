using System.Net;
using System.Text.RegularExpressions;
using Garderie.Web.Data;
using Garderie.Web.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace Garderie.Tests;

/// <summary>
/// FO-0310 synthetique : memes noms de champs que le formulaire officiel (non inclus dans le depot public).
/// Synthetic FO-0310: same field names as the official form (not included in the public repo).
/// </summary>
public static class ModeleTest
{
    public static IEnumerable<string> Champs()
    {
        string[] fixes =
        [
            "S1_Nom_Bureau", "S1_Nom_Enfant", "S1_Nom_Parent", "S1_Nom_RSGE", "S1_Date_Fin_Frequentation",
            "S1_Date_Debut_1", "S1_Date_Debut_2", "S1_Date_Debut_3", "S1_Date_Debut_4",
            "S1_Date_Paiement_1", "S1_Date_Paiement_2", "S1_Montant_Paye_1", "S1_Montant_Paye_2",
            "S1_Solde_Paye_1", "S1_Solde_Paye_2", "S1_Date_Signature_1", "S1_Date_Signature_2",
            "S2_Signature_1", "S2_Signature_2",
        ];
        foreach (var f in fixes) yield return f;
        foreach (var j in Fo0310Pdf.Jours)
            foreach (var d in new[] { "Matin", "PM" })
                for (var l = 1; l <= 2; l++) yield return $"S1_{j}_{d}_Ligne{l}";
    }

    public static byte[] Creer(IEnumerable<string>? champs = null)
    {
        var doc = new PdfDocument();
        var page = doc.AddPage();
        page.Width = XUnit.FromPoint(816);
        page.Height = XUnit.FromPoint(1056);
        var annots = new PdfArray(doc);
        var y = 1000.0;
        foreach (var nom in champs ?? Champs())
        {
            var a = new PdfDictionary(doc);
            a.Elements["/Type"] = new PdfName("/Annot");
            a.Elements["/Subtype"] = new PdfName("/Widget");
            a.Elements["/FT"] = new PdfName(nom.StartsWith("S2_") ? "/Sig" : "/Tx");
            a.Elements["/T"] = new PdfString(nom);
            a.Elements["/Rect"] = new PdfRectangle(new XRect(40, y, 120, 16));
            doc.Internals.AddObject(a);
            annots.Elements.Add(a.Reference!);
            y -= 18;
        }
        page.Elements["/Annots"] = annots;
        var acro = new PdfDictionary(doc);
        acro.Elements["/Fields"] = annots.Clone();
        doc.Internals.AddObject(acro);
        doc.Internals.Catalog.Elements["/AcroForm"] = acro.Reference;
        using var ms = new MemoryStream();
        doc.Save(ms);
        return ms.ToArray();
    }

    /// <summary>PNG 1 x 1 valide / Valid 1 x 1 PNG.</summary>
    public const string Png = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";
}

public class FichesUnitairesTests
{
    [Theory]
    [InlineData("2026-11-04", "2026-11-02")]
    [InlineData("2026-10-30", "2026-10-19")]
    [InlineData("2026-11-01", "2026-10-19")]
    [InlineData("2026-11-16", "2026-11-16")]
    [InlineData("2026-10-18", "2026-10-05")]   // avant l'ancre / before the anchor
    public void Periodes(string jour, string debut) =>
        Assert.Equal(DateOnly.Parse(debut), FichesService.DebutPeriode(DateOnly.Parse(jour), FichesService.AncreParDefaut));

    [Fact]
    public void CodesAllerRetour()
    {
        var codes = new Dictionary<(DateOnly, DemiJournee), string>
        {
            [(new DateOnly(2026, 11, 3), DemiJournee.Pm)] = "A 1/2",
            [(new DateOnly(2026, 11, 2), DemiJournee.Matin)] = "P",
        };
        var texte = FichesService.Serialiser(codes);
        Assert.Equal("2026-11-02M=P;2026-11-03P=A 1/2", texte);
        Assert.Equal(codes, FichesService.Lire(texte));
    }

    [Fact]
    public void SignatureValidee()
    {
        Assert.NotNull(FichesService.LireSignature(ModeleTest.Png));
        Assert.Null(FichesService.LireSignature("data:image/png;base64,JVBERi0xLjQ="));   // un PDF deguise
        Assert.Null(FichesService.LireSignature("data:image/svg+xml;base64,PHN2Zz4="));
        Assert.Null(FichesService.LireSignature(null));
    }

    [Fact]
    public void ValideLeModele()
    {
        Assert.Empty(Fo0310Pdf.Valider(ModeleTest.Creer()));
        Assert.Contains("S2_Signature_2", Fo0310Pdf.Valider(ModeleTest.Creer(ModeleTest.Champs().Where(c => c != "S2_Signature_2"))));
        Assert.NotEmpty(Fo0310Pdf.Valider("%PDF-pas un pdf"u8.ToArray()));
    }

    [Fact]
    public void RemplitEtRetireLesChamps()
    {
        var codes = new Dictionary<(DateOnly, DemiJournee), string> { [(new DateOnly(2026, 11, 2), DemiJournee.Matin)] = "P" };
        var png = FichesService.LireSignature(ModeleTest.Png);
        var c = new ContenuFiche("BC", "Léa Tremblay", "Julie Tremblay", "Sonia", null, new DateOnly(2026, 11, 2), codes,
            new DateOnly(2026, 11, 2), 9.35m, 0m, null, null, null, png, new DateOnly(2026, 11, 16), png, new DateOnly(2026, 11, 17));
        var v = Fo0310Pdf.Valeurs(c);
        Assert.Equal("P", v["S1_Lundi_Matin_Ligne1"]);
        Assert.Equal("", v["S1_Lundi_PM_Ligne1"]);
        Assert.Equal("2026-11-09", v["S1_Date_Debut_2"]);
        Assert.Equal("9,35", v["S1_Montant_Paye_1"]);

        var pdf = Fo0310Pdf.Remplir(ModeleTest.Creer(), c);
        using var doc = PdfReader.Open(new MemoryStream(pdf), PdfDocumentOpenMode.Import);
        Assert.Null(doc.Pages[0].Elements.GetArray("/Annots"));                 // plus de champs / no more fields
        Assert.False(doc.Internals.Catalog.Elements.ContainsKey("/AcroForm"));
    }
}

/// <summary>Parcours complet : RSGE signe, lien par courriel, parent signe / Full flow.</summary>
public class FichesParcoursTests(Appli appli) : IClassFixture<Appli>
{
    private static readonly DateOnly Debut = new(2026, 3, 9);   // une periode (ancre 19 oct.) : 9 au 22 mars 2026

    private HttpClient Admin()
    {
        var c = appli.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        c.DefaultRequestHeaders.Add("X-Test-Port", "8090");
        c.DefaultRequestHeaders.Add("X-Test-Ip", "172.30.32.2");
        c.DefaultRequestHeaders.Add("X-Remote-User-Name", "rsge");
        return c;
    }

    private HttpClient Parent()
    {
        var c = appli.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        c.DefaultRequestHeaders.Add("X-Test-Port", "8080");
        c.DefaultRequestHeaders.Add("X-Test-Ip", "203.0.113.7");
        return c;
    }

    private static string Jeton(string html) =>
        Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;

    private T Db<T>(Func<GarderieDb, T> f)
    {
        using var scope = appli.Services.CreateScope();
        return f(scope.ServiceProvider.GetRequiredService<GarderieDb>());
    }

    private string Courriels => Path.Combine(appli.Dossier, "courriels");

    /// <summary>Enfant inscrit, parent payeur avec courriel, presences enregistrees (sauf jours manquants) / Setup.</summary>
    private int Preparer(string prenom, bool presencesCompletes = true)
    {
        var options = appli.Services.GetRequiredService<Garderie.Web.Configuration.GarderieOptions>();
        Directory.CreateDirectory(Path.GetDirectoryName(options.FichierModeleFo0310)!);
        File.WriteAllBytes(options.FichierModeleFo0310, ModeleTest.Creer());
        return Db(db =>
        {
            if (db.Parametres.Find(1) is null) db.Parametres.Add(new Parametres { Id = 1, NomRsge = "Sonia", CourrielRsge = "rsge@exemple.test" });
            var m = DateTime.UtcNow;
            var e = new Enfant { Prenom = prenom, Nom = "F", DateDebut = new DateOnly(2026, 1, 5), CreeLe = m, ModifieLe = m };
            e.Parents = [new() { Parent = new Parent { Prenom = "Julie", Nom = "F", Courriel = "julie@exemple.test", CreeLe = m, ModifieLe = m }, Lien = "Mère", EstPayeur = true }];
            db.Enfants.Add(e);
            db.SaveChanges();
            for (var i = 0; i < 14; i++)
            {
                var d = Debut.AddDays(i);
                if (d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;
                if (!presencesCompletes && i == 3) continue;
                foreach (var demi in new[] { DemiJournee.Matin, DemiJournee.Pm })
                    db.Presences.Add(new Presence { EnfantId = e.Id, Date = d, Demi = demi, Code = "P", ModifieLe = m });
            }
            db.SaveChanges();
            return e.Id;
        });
    }

    private async Task<HttpResponseMessage> PosterFiche(HttpClient c, int id, string handler, Dictionary<string, string>? champs = null)
    {
        var url = $"/Fiches/Fiche/{id}?debut={Debut:yyyy-MM-dd}";
        champs ??= new();
        champs["__RequestVerificationToken"] = Jeton(await c.GetStringAsync(url));
        return await c.PostAsync($"{url}&handler={handler}", new FormUrlEncodedContent(champs));
    }

    private string DernierLien(string destinataire)
    {
        var eml = Directory.GetFiles(Courriels, "*.eml").Select(File.ReadAllText).Last(t => t.Contains(destinataire));
        // Le corps peut etre en quoted-printable : on recolle les lignes coupees / body may be quoted-printable
        eml = eml.Replace("=\r\n", "").Replace("=\n", "").Replace("=3D", "=");
        return Regex.Match(eml, @"https://exemple\.test/garderie/Public/Signer\?j=([A-Za-z0-9_\-]+)").Groups[1].Value;
    }

    [Fact]
    public async Task ParcoursComplet()
    {
        var id = Preparer("Parcours");
        var admin = Admin();

        await PosterFiche(admin, id, "Paiement", new() { ["Paiement.DatePaiement1"] = "2026-03-13", ["Paiement.MontantPaye1"] = "46.75", ["Paiement.Solde1"] = "0" });
        Assert.Equal(46.75m, Db(db => db.Fiches.Single(f => f.EnfantId == id).MontantPaye1));

        await PosterFiche(admin, id, "SignerRsge", new() { ["signature"] = ModeleTest.Png });
        var fiche = Db(db => db.Fiches.Single(f => f.EnfantId == id));
        Assert.Equal(EtatFiche.SigneeRsge, fiche.Etat);
        Assert.Equal("Julie F", fiche.NomParent);
        Assert.Contains("2026-03-09M=P", fiche.CodesSignes);

        await PosterFiche(admin, id, "Envoyer");
        var jeton = DernierLien("julie@exemple.test");
        Assert.Equal(43, jeton.Length);
        Assert.False(Db(db => db.Liens.Any(l => l.JetonHash == jeton)));   // seul le hachage est stocke

        // Le parent ouvre le lien sur le port public / Parent opens the link on the public port
        var parent = Parent();
        var page = await parent.GetAsync($"/garderie/Public/Signer?j={jeton}");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var html = await page.Content.ReadAsStringAsync();
        Assert.Contains("Fiche d'assiduité de Parcours", html);
        Assert.DoesNotContain("NAS", html);

        var r = await parent.PostAsync($"/garderie/Public/Signer?j={jeton}&handler=Signer", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Jeton(html), ["signature"] = ModeleTest.Png, ["nom"] = "Julie F", ["atteste"] = "true",
        }));
        Assert.Contains("Merci", await r.Content.ReadAsStringAsync());

        fiche = Db(db => db.Fiches.Single(f => f.EnfantId == id));
        Assert.Equal(EtatFiche.SigneeParent, fiche.Etat);
        Assert.Equal("203.0.113.7", fiche.IpParent);
        var doc = Db(db => db.Documents.Single(d => d.Id == fiche.DocumentId));
        Assert.Equal(TypeDocument.FicheAssiduite, doc.Type);
        Assert.Equal(fiche.PdfSha256, doc.Sha256);
        Assert.Contains(Directory.GetFiles(Courriels, "*.eml").Select(File.ReadAllText), t => t.Contains("rsge@exemple.test") && t.Contains("application/pdf"));

        // Lien a usage unique : un 2e envoi (double toucher) remercie au lieu d'une erreur, sans re-signer
        // One-time link: a 2nd submission thanks instead of an error, without signing again
        var encore = await (await parent.GetAsync($"/garderie/Public/Signer?j={jeton}")).Content.ReadAsStringAsync();
        Assert.Contains("Merci", encore);
        Assert.DoesNotContain("Lien invalide", encore);
        var deuxieme = await parent.PostAsync($"/garderie/Public/Signer?j={jeton}&handler=Signer", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Jeton(html), ["signature"] = ModeleTest.Png, ["nom"] = "Autre", ["atteste"] = "true",
        }));
        Assert.Contains("Merci", await deuxieme.Content.ReadAsStringAsync());
        Assert.Equal("Julie F", Db(db => db.Fiches.Single(f => f.EnfantId == id).SignataireParent));
        Assert.Single(Db(db => db.Documents.Where(d => d.EnfantId == id && d.Type == TypeDocument.FicheAssiduite).ToList()));

        // PDF final telechargeable par la RSGE / Final PDF downloadable
        var pdf = await admin.GetAsync($"/Fiches/Fiche/{id}?debut={Debut:yyyy-MM-dd}&handler=Pdf");
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task SignatureRsgeRefuseeSiPresencesManquantes()
    {
        var id = Preparer("Incomplet", presencesCompletes: false);
        await PosterFiche(Admin(), id, "SignerRsge", new() { ["signature"] = ModeleTest.Png });
        Assert.False(Db(db => db.Fiches.Any(f => f.EnfantId == id && f.Etat != EtatFiche.Brouillon)));
    }

    [Fact]
    public async Task JetonInvalideOuExpire()
    {
        var id = Preparer("Expire");
        var admin = Admin();
        await PosterFiche(admin, id, "SignerRsge", new() { ["signature"] = ModeleTest.Png });
        await PosterFiche(admin, id, "Envoyer");
        var jeton = DernierLien("julie@exemple.test");
        Db(db => db.Liens.Where(l => l.Fiche.EnfantId == id).ExecuteUpdate(s => s.SetProperty(l => l.ExpireLe, DateTime.UtcNow.AddMinutes(-1))));

        var parent = Parent();
        Assert.Contains("Lien invalide ou expiré", await parent.GetStringAsync($"/garderie/Public/Signer?j={jeton}"));
        Assert.Contains("Lien invalide ou expiré", await parent.GetStringAsync("/garderie/Public/Signer?j=nimportequoi"));
    }

    [Fact]
    public async Task ErreurSignaleePuisReprise()
    {
        var id = Preparer("Erreur");
        var admin = Admin();
        await PosterFiche(admin, id, "SignerRsge", new() { ["signature"] = ModeleTest.Png });
        await PosterFiche(admin, id, "Envoyer");
        var jeton = DernierLien("julie@exemple.test");

        var parent = Parent();
        var html = await parent.GetStringAsync($"/garderie/Public/Signer?j={jeton}");
        await parent.PostAsync($"/garderie/Public/Signer?j={jeton}&handler=Erreur", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Jeton(html), ["message"] = "Absent le mardi 3",
        }));
        Assert.Equal("Absent le mardi 3", Db(db => db.Fiches.Single(f => f.EnfantId == id).ErreurSignalee));

        // La RSGE reprend : le lien est annule / RSGE reopens: link cancelled
        await PosterFiche(admin, id, "Reprendre");
        Assert.Equal(EtatFiche.Brouillon, Db(db => db.Fiches.Single(f => f.EnfantId == id).Etat));
        Assert.Contains("Lien invalide ou expiré", await parent.GetStringAsync($"/garderie/Public/Signer?j={jeton}"));
    }

    [Fact]
    public async Task SignatureSurPlace()
    {
        var id = Preparer("SurPlace");
        var admin = Admin();
        await PosterFiche(admin, id, "SignerRsge", new() { ["signature"] = ModeleTest.Png });
        var r = await PosterFiche(admin, id, "SurPlace");
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        var url = r.Headers.Location!.ToString();
        Assert.StartsWith("/Public/Signer?j=", url);

        var html = await admin.GetStringAsync(url);
        await admin.PostAsync(url + "&handler=Signer", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Jeton(html), ["signature"] = ModeleTest.Png, ["nom"] = "Julie F", ["atteste"] = "true",
        }));
        var fiche = Db(db => db.Fiches.Single(f => f.EnfantId == id));
        Assert.Equal(EtatFiche.SigneeParent, fiche.Etat);
        Assert.True(fiche.SigneSurPlace);
    }

    [Fact]
    public async Task SemaineSigneeVerrouilleeDansPresences()
    {
        var id = Preparer("Verrou");
        var admin = Admin();
        await PosterFiche(admin, id, "SignerRsge", new() { ["signature"] = ModeleTest.Png });

        var semaine = $"/Presences?semaine={Debut:yyyy-MM-dd}";
        var html = await admin.GetStringAsync(semaine);
        var carte = html[html.IndexOf(">Verrou <", StringComparison.Ordinal)..];
        carte = carte[..carte.IndexOf("</section>", StringComparison.Ordinal)];
        Assert.Contains("Fiche en attente du parent", carte);
        Assert.DoesNotContain("<select", carte);                    // lecture seule / read-only

        // Une modification forcee est ignoree par le serveur / A forced change is ignored by the server
        await admin.PostAsync(semaine, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Jeton(html),
            ["codes[0].EnfantId"] = $"{id}", ["codes[0].Date"] = $"{Debut:yyyy-MM-dd}", ["codes[0].Demi"] = "Matin", ["codes[0].Code"] = "A",
        }));
        Assert.Equal("P", Db(db => db.Presences.Single(p => p.EnfantId == id && p.Date == Debut && p.Demi == DemiJournee.Matin).Code));

        // Apres « Reprendre », la semaine redevient modifiable / After reopening, editable again
        await PosterFiche(admin, id, "Reprendre");
        html = await admin.GetStringAsync(semaine);
        carte = html[html.IndexOf(">Verrou <", StringComparison.Ordinal)..];
        Assert.Contains("<select", carte[..carte.IndexOf("</section>", StringComparison.Ordinal)]);
    }

    [Fact]
    public async Task PagesFichesInvisiblesSurLePortPublic()
    {
        var r = await Parent().GetAsync("/garderie/Fiches");
        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
    }
}
