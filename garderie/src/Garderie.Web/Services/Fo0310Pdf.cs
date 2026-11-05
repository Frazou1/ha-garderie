using System.Globalization;
using Garderie.Web.Data;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Annotations;
using PdfSharp.Pdf.IO;

namespace Garderie.Web.Services;

/// <summary>Contenu d'une fiche a ecrire dans le FO-0310 / Content written into the FO-0310.</summary>
public sealed record ContenuFiche(
    string? NomBureau, string NomEnfant, string? NomParent, string? NomRsge, DateOnly? DateFinFrequentation,
    DateOnly Debut, IReadOnlyDictionary<(DateOnly, DemiJournee), string> Codes,
    DateOnly? DatePaiement1, decimal? MontantPaye1, decimal? Solde1,
    DateOnly? DatePaiement2, decimal? MontantPaye2, decimal? Solde2,
    byte[]? SignatureRsge, DateOnly? DateSignatureRsge,
    byte[]? SignatureParent, DateOnly? DateSignatureParent);

/// <summary>
/// Remplit le formulaire officiel FO-0310 (televerse dans Reglages) : chaque valeur est ecrite a l'endroit exact
/// de son champ, les signatures sont apposees dans les rectangles /Sig, puis les champs sont retires
/// (PDF final non modifiable). Le formulaire lui-meme n'est jamais redessine.
/// Fills the official FO-0310: values drawn at each field's position, signatures in the /Sig boxes,
/// then form fields removed (final PDF not editable). The form itself is never redrawn.
/// </summary>
public static class Fo0310Pdf
{
    public static readonly string[] Jours = ["Lundi", "Mardi", "Mercredi", "Jeudi", "Vendredi", "Samedi", "Dimanche"];

    /// <summary>Champs indispensables pour reconnaitre le FO-0310 / Fields required to recognise the FO-0310.</summary>
    public static readonly string[] ChampsRequis =
    [
        "S1_Nom_Enfant", "S1_Nom_Parent", "S1_Nom_RSGE", "S1_Date_Debut_1", "S1_Date_Debut_2",
        "S1_Lundi_Matin_Ligne1", "S1_Dimanche_PM_Ligne2", "S2_Signature_1", "S2_Signature_2",
        "S1_Date_Signature_1", "S1_Date_Signature_2",
    ];

    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-CA");

    static Fo0310Pdf() => PolicesPdf.Installer();

    /// <summary>Champs manquants (vide = c'est bien le FO-0310) / Missing fields (empty = valid FO-0310).</summary>
    public static IReadOnlyList<string> Valider(byte[] pdf)
    {
        try
        {
            using var ms = new MemoryStream(pdf);
            using var doc = PdfReader.Open(ms, PdfDocumentOpenMode.Import);
            var noms = doc.Pages.Cast<PdfPage>().SelectMany(p => LireChamps(p).Keys).ToHashSet();
            return ChampsRequis.Where(c => !noms.Contains(c)).ToList();
        }
        catch (Exception)
        {
            return ["(PDF illisible)"];
        }
    }

    public static byte[] Remplir(byte[] modele, ContenuFiche c)
    {
        using var entree = new MemoryStream(modele);
        using var doc = PdfReader.Open(entree, PdfDocumentOpenMode.Modify);
        var valeurs = Valeurs(c);
        var police = new XFont("Garderie", 9);
        var policeCode = new XFont("Garderie", 8, XFontStyleEx.Bold);

        foreach (PdfPage page in doc.Pages)
        {
            var champs = LireChamps(page);
            var hauteur = page.Height.Point;
            using (var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append))
            {
                foreach (var (nom, r) in champs)
                {
                    // Coordonnees PDF (origine en bas) -> XGraphics (origine en haut) / PDF -> XGraphics coordinates
                    var cadre = new XRect(r.X1, hauteur - r.Y2, r.X2 - r.X1, r.Y2 - r.Y1);
                    if (nom == "S2_Signature_1") Signature(gfx, c.SignatureRsge, cadre);
                    else if (nom == "S2_Signature_2") Signature(gfx, c.SignatureParent, cadre);
                    else if (valeurs.TryGetValue(nom, out var texte) && !string.IsNullOrEmpty(texte))
                    {
                        var estCode = nom.Contains("_Ligne", StringComparison.Ordinal);
                        var interieur = new XRect(cadre.X + 2, cadre.Y, Math.Max(1, cadre.Width - 4), cadre.Height);
                        gfx.DrawString(texte, estCode ? policeCode : police, XBrushes.Black, interieur,
                            estCode ? XStringFormats.Center : XStringFormats.CenterLeft);
                    }
                }
            }
            // Retire les champs : la fiche signee n'est plus modifiable / Remove fields: signed sheet no longer editable
            page.Elements.Remove("/Annots");
        }
        doc.Internals.Catalog.Elements.Remove("/AcroForm");

        using var sortie = new MemoryStream();
        doc.Save(sortie, false);
        return sortie.ToArray();
    }

    /// <summary>Valeur de chaque champ texte ou liste / Value of each text or list field.</summary>
    public static Dictionary<string, string> Valeurs(ContenuFiche c)
    {
        var v = new Dictionary<string, string>
        {
            ["S1_Nom_Bureau"] = c.NomBureau ?? "",
            ["S1_Nom_Enfant"] = c.NomEnfant,
            ["S1_Nom_Parent"] = c.NomParent ?? "",
            ["S1_Nom_RSGE"] = c.NomRsge ?? "",
            ["S1_Date_Fin_Frequentation"] = Date(c.DateFinFrequentation),
            ["S1_Date_Debut_1"] = Date(c.Debut),
            ["S1_Date_Debut_2"] = Date(c.Debut.AddDays(7)),
            ["S1_Date_Debut_3"] = Date(c.Debut),
            ["S1_Date_Debut_4"] = Date(c.Debut.AddDays(7)),
            ["S1_Date_Paiement_1"] = Date(c.DatePaiement1),
            ["S1_Montant_Paye_1"] = Montant(c.MontantPaye1),
            ["S1_Solde_Paye_1"] = Montant(c.Solde1),
            ["S1_Date_Paiement_2"] = Date(c.DatePaiement2),
            ["S1_Montant_Paye_2"] = Montant(c.MontantPaye2),
            ["S1_Solde_Paye_2"] = Montant(c.Solde2),
            ["S1_Date_Signature_1"] = Date(c.DateSignatureRsge),
            ["S1_Date_Signature_2"] = Date(c.DateSignatureParent),
        };
        for (var ligne = 1; ligne <= 2; ligne++)
        {
            for (var j = 0; j < 7; j++)
            {
                var jour = c.Debut.AddDays((ligne - 1) * 7 + j);
                v[$"S1_{Jours[j]}_Matin_Ligne{ligne}"] = c.Codes.GetValueOrDefault((jour, DemiJournee.Matin), "");
                v[$"S1_{Jours[j]}_PM_Ligne{ligne}"] = c.Codes.GetValueOrDefault((jour, DemiJournee.Pm), "");
            }
        }
        return v;
    }

    private static string Date(DateOnly? d) => d?.ToString("yyyy-MM-dd") ?? "";
    private static string Montant(decimal? m) => m?.ToString("0.00", Fr) ?? "";

    private static void Signature(XGraphics gfx, byte[]? png, XRect cadre)
    {
        if (png is null) return;
        using var ms = new MemoryStream(png);
        var img = XImage.FromStream(ms);
        // Garde les proportions, centre dans le cadre / Keep aspect ratio, centred
        var echelle = Math.Min(cadre.Width / img.PointWidth, cadre.Height / img.PointHeight);
        var l = img.PointWidth * echelle;
        var h = img.PointHeight * echelle;
        gfx.DrawImage(img, cadre.X + (cadre.Width - l) / 2, cadre.Y + (cadre.Height - h) / 2, l, h);
    }

    /// <summary>Nom complet de chaque champ de la page et son rectangle / Each field's full name and rectangle.</summary>
    public static Dictionary<string, PdfRectangle> LireChamps(PdfPage page)
    {
        var champs = new Dictionary<string, PdfRectangle>();
        var annots = page.Elements.GetArray("/Annots");
        if (annots is null) return champs;
        for (var i = 0; i < annots.Elements.Count; i++)
        {
            if (annots.Elements.GetObject(i) is not PdfDictionary a) continue;
            if (a.Elements.GetName("/Subtype") != "/Widget") continue;
            var nom = NomComplet(a);
            if (string.IsNullOrEmpty(nom)) continue;
            champs.TryAdd(nom, a.Elements.GetRectangle("/Rect"));
        }
        return champs;
    }

    private static string NomComplet(PdfDictionary a)
    {
        var parties = new List<string>();
        for (var d = a; d is not null; d = d.Elements.GetDictionary("/Parent"))
        {
            var t = d.Elements.GetString("/T");
            if (!string.IsNullOrEmpty(t)) parties.Insert(0, t);
        }
        return string.Join('.', parties);
    }
}

/// <summary>
/// Polices du PDF. Fiche FO-0310 : DejaVu Sans dans le conteneur (paquet fonts-dejavu-core), Arial sous Windows.
/// Contrat et regie interne : polices libres incluses dans le module (dossier Polices, licence SIL OFL) :
/// Comic Neue (comme Comic Sans MS), Tinos (mesures de Times New Roman), Carlito (mesures de Calibri).
/// PDF fonts: DejaVu/Arial for FO-0310; bundled free fonts (OFL) for the contract and internal rules.
/// </summary>
public sealed class PolicesPdf : IFontResolver
{
    private static readonly object Verrou = new();

    /// <summary>Installe le resolveur une seule fois (PDFsharp et MigraDoc) / Installs the resolver once.</summary>
    public static void Installer()
    {
        lock (Verrou) GlobalFontSettings.FontResolver ??= new PolicesPdf();
    }

    private static readonly string[] Normale =
    [
        "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
        "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf",
        @"C:\Windows\Fonts\arial.ttf",
    ];
    private static readonly string[] Grasse =
    [
        "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
        "/usr/share/fonts/truetype/liberation/LiberationSans-Bold.ttf",
        @"C:\Windows\Fonts\arialbd.ttf",
    ];

    /// <summary>Dossier des polices incluses (copie a la publication) / Bundled fonts folder (copied on publish).</summary>
    public static string DossierInclus => Path.Combine(AppContext.BaseDirectory, "Polices");

    /// <summary>Famille du contrat -> fichier inclus, sinon null / Contract family -> bundled file, else null.</summary>
    private static string? FichierContrat(string famille, bool gras) => famille switch
    {
        "ContratTexte" => gras ? "ComicNeue-Bold.ttf" : "ComicNeue-Regular.ttf",
        "ContratTitre" => "ComicNeue-Bold.ttf",
        "ContratClauses" => "Tinos-Bold.ttf",
        "ContratSousTitre" => "Carlito-Bold.ttf",
        _ => null,
    };

    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        if (FichierContrat(familyName, isBold) is { } f && File.Exists(Path.Combine(DossierInclus, f)))
            return new FontResolverInfo("Inclus-" + f);
        var gras = isBold || familyName is "ContratTitre" or "ContratClauses" or "ContratSousTitre";
        return new FontResolverInfo(gras ? "Garderie-Gras" : "Garderie-Normal");
    }

    public byte[] GetFont(string faceName)
    {
        if (faceName.StartsWith("Inclus-", StringComparison.Ordinal))
            return File.ReadAllBytes(Path.Combine(DossierInclus, faceName[7..]));
        var liste = faceName == "Garderie-Gras" ? Grasse : Normale;
        var chemin = liste.FirstOrDefault(File.Exists)
            ?? throw new InvalidOperationException("Aucune police trouvée pour le PDF (installer fonts-dejavu-core).");
        return File.ReadAllBytes(chemin);
    }
}
