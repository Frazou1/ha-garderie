using System.Security.Cryptography;
using Garderie.Web.Data;
using Garderie.Web.Securite;
using Garderie.Web.Services;

namespace Garderie.Tests;

public class IdentifiantsTests
{
    [Theory]
    [InlineData("130 692 544", "130692544")]
    [InlineData("130-692-544", "130692544")]
    [InlineData("123 456 789", null)]   // cle de Luhn invalide / invalid check digit
    [InlineData("046 454 286", null)]   // commence par 0 : jamais attribue / starts with 0
    [InlineData("13069254", null)]
    [InlineData("", null)]
    public void Nas(string saisie, string? attendu) => Assert.Equal(attendu, Identifiants.NormaliserNas(saisie));

    [Theory]
    [InlineData("tesx 2305 1099", "TESX23051099")]
    [InlineData("TESX2305109", null)]
    [InlineData("12345678ABCD", null)]
    public void Nam(string saisie, string? attendu) => Assert.Equal(attendu, Identifiants.NormaliserNam(saisie));

    [Fact]
    public void Masques()
    {
        Assert.Equal("*** *** 544", Identifiants.MasquerNas("130692544"));
        Assert.Equal("TESX **** **99", Identifiants.MasquerNam("TESX23051099"));
    }
}

public class ChiffreurTests
{
    private readonly Chiffreur _c = new(RandomNumberGenerator.GetBytes(32));

    [Fact]
    public void AllerRetour()
    {
        var chiffre = _c.ChiffrerTexte("130692544", Contextes.NasParent)!;
        Assert.DoesNotContain("130692544", System.Text.Encoding.Latin1.GetString(chiffre));
        Assert.Equal("130692544", _c.DechiffrerTexte(chiffre, Contextes.NasParent));
    }

    [Fact]
    public void NonceDifferentAChaqueFois() =>
        Assert.NotEqual(_c.ChiffrerTexte("x", "c"), _c.ChiffrerTexte("x", "c"));

    [Fact]
    public void MauvaisContexteRefuse()
    {
        var chiffre = _c.ChiffrerTexte("130692544", Contextes.NasParent)!;
        Assert.ThrowsAny<CryptographicException>(() => _c.DechiffrerTexte(chiffre, Contextes.NamEnfant));
    }

    [Fact]
    public void AlterationDetectee()
    {
        var chiffre = _c.Chiffrer("bonjour"u8, "c");
        chiffre[^1] ^= 1;
        Assert.ThrowsAny<CryptographicException>(() => _c.Dechiffrer(chiffre, "c"));
    }

    [Fact]
    public void MauvaiseCleRefusee()
    {
        var chiffre = _c.Chiffrer("bonjour"u8, "c");
        var autre = new Chiffreur(RandomNumberGenerator.GetBytes(32));
        Assert.ThrowsAny<CryptographicException>(() => autre.Dechiffrer(chiffre, "c"));
    }
}

public class DocumentsTests
{
    [Fact]
    public void DetecteLesTypes()
    {
        Assert.Equal("application/pdf", DocumentsService.DetecterType("%PDF-1.7\n"u8));
        Assert.Equal("image/jpeg", DocumentsService.DetecterType(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }));
        Assert.Equal("image/png", DocumentsService.DetecterType(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0 }));
        Assert.Equal("image/heic", DocumentsService.DetecterType(new byte[] { 0, 0, 0, 0x18, (byte)'f', (byte)'t', (byte)'y', (byte)'p', (byte)'h', (byte)'e', (byte)'i', (byte)'c' }));
        Assert.Null(DocumentsService.DetecterType(new byte[] { (byte)'M', (byte)'Z', 0x90, 0 }));
        Assert.Null(DocumentsService.DetecterType("<html>"u8));
    }
}

public class PresencesTests
{
    private static readonly JourFermeture[] Aucune = [];

    [Fact]
    public void ProposePTousLesJoursOuvrables()
    {
        var mercredi = new DateOnly(2026, 11, 4);
        Assert.Equal("P", PresencesService.Proposer(mercredi, DemiJournee.Matin, Aucune));
        Assert.Equal("P", PresencesService.Proposer(mercredi, DemiJournee.Pm, Aucune));
        Assert.Equal("", PresencesService.Proposer(new DateOnly(2026, 11, 7), DemiJournee.Matin, Aucune));   // samedi
        Assert.Equal("", PresencesService.Proposer(new DateOnly(2026, 11, 8), DemiJournee.Pm, Aucune));      // dimanche
    }

    [Fact]
    public void ProposeFermeture()
    {
        var jour = new DateOnly(2026, 11, 5);
        JourFermeture[] journee = [new() { Date = jour, Demi = null }];
        JourFermeture[] pm = [new() { Date = jour, Demi = DemiJournee.Pm }];
        Assert.Equal("F", PresencesService.Proposer(jour, DemiJournee.Matin, journee));
        Assert.Equal("F", PresencesService.Proposer(jour, DemiJournee.Pm, journee));
        Assert.Equal("P 1/2", PresencesService.Proposer(jour, DemiJournee.Matin, pm));
        Assert.Equal("F 1/2", PresencesService.Proposer(jour, DemiJournee.Pm, pm));
        Assert.Equal("P", PresencesService.Proposer(jour.AddDays(1), DemiJournee.Matin, journee));
    }

    [Theory]
    [InlineData("2026-11-02", "2026-11-02")]  // lundi
    [InlineData("2026-11-04", "2026-11-02")]  // mercredi
    [InlineData("2026-11-08", "2026-11-02")]  // dimanche : meme semaine / Sunday: same week
    public void LundiDeLaSemaine(string jour, string lundi) =>
        Assert.Equal(DateOnly.Parse(lundi), PresencesService.Lundi(DateOnly.Parse(jour)));

    [Fact]
    public void HoraireVersionne()
    {
        var h = new List<HoraireType>
        {
            new() { DateEffective = new DateOnly(2026, 9, 1), Jour = DayOfWeek.Wednesday, Matin = true, Pm = true },
            new() { DateEffective = new DateOnly(2026, 11, 2), Jour = DayOfWeek.Wednesday, Matin = true, Pm = false },
            new() { DateEffective = new DateOnly(2026, 12, 1), Jour = DayOfWeek.Wednesday, Matin = false, Pm = false },
        };
        Assert.Equal((true, true), PresencesService.HorairePrevu(h, new DateOnly(2026, 10, 28)));
        Assert.Equal((true, false), PresencesService.HorairePrevu(h, new DateOnly(2026, 11, 4)));
        Assert.Equal((false, false), PresencesService.HorairePrevu(h, new DateOnly(2026, 8, 26)));
        // Jour absent de la version en vigueur = non prevu / Day missing from current version = not planned
        Assert.Equal((false, false), PresencesService.HorairePrevu(h, new DateOnly(2026, 11, 5)));
    }

    [Fact]
    public void CodesDuFormulaire()
    {
        Assert.Equal(13, CodesPresence.Tous.Count);
        Assert.True(CodesPresence.EstValide("S 1/2"));
        Assert.True(CodesPresence.EstValide(""));
        Assert.False(CodesPresence.EstValide("X"));
    }
}
