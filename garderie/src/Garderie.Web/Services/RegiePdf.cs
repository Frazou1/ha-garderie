using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Garderie.Web.Securite;
using MigraDoc.DocumentObjectModel;
using MigraDoc.Rendering;
using Document = MigraDoc.DocumentObjectModel.Document;

namespace Garderie.Web.Services;

/// <summary>
/// Regie interne : texte modifiable dans Reglages (balisage simple), importe une fois du Word de la RSGE,
/// puis PDF genere automatiquement (texte en Comic Sans, comme le Word).
/// Balisage, une ligne = un paragraphe : « # » titre, « ## » section, « ### » sous-titre gras souligne,
/// « - » point de liste, « > » centre, **gras** ; ligne vide = espace. {rsge}, {garderie}, {mise_a_jour} sont remplaces.
/// Internal rules: editable text (simple markup) imported once from Word, PDF generated automatically.
/// </summary>
public static class RegiePdf
{
    static RegiePdf() => PolicesPdf.Installer();

    private static string Texte => PolicesPdf.Famille("ContratTexte");

    public const string Aide =
        "Une ligne = un paragraphe. « # » grand titre, « ## » titre de section, « ### » sous-titre (gras souligné), " +
        "« - » point de liste, « > » centré (ex. « > ## Sonia Savard »), **gras** entre doubles astérisques, ligne vide = espace. " +
        "{rsge}, {garderie} et {mise_a_jour} (mois de la dernière modification) sont remplacés.";

    // --- Generation / Rendering ---

    public static byte[] Generer(string texte, string? rsge, string? garderie, DateTime? modifieeLe)
    {
        PolicesPdf.Installer();
        var miseAJour = modifieeLe is { } m
            ? Temps.VersLocal(m).ToString("MMMM yyyy", new System.Globalization.CultureInfo("fr-CA"))
            : "";
        texte = texte.Replace("{rsge}", rsge ?? "").Replace("{garderie}", garderie ?? "").Replace("{mise_a_jour}", miseAJour);

        var doc = new Document();
        doc.Info.Title = "Régie interne";
        var normal = doc.Styles[StyleNames.Normal]!;
        normal.Font.Name = Texte;
        normal.Font.Size = 12;
        normal.ParagraphFormat.SpaceAfter = Unit.FromPoint(0);
        normal.ParagraphFormat.Alignment = ParagraphAlignment.Justify;
        var sec = doc.AddSection();
        sec.PageSetup = doc.DefaultPageSetup.Clone();
        sec.PageSetup.PageFormat = PageFormat.Letter;
        sec.PageSetup.LeftMargin = sec.PageSetup.RightMargin = Unit.FromInch(1.25);   // marges du Word / Word margins
        sec.PageSetup.TopMargin = sec.PageSetup.BottomMargin = Unit.FromInch(1);
        var pied = sec.Footers.Primary.AddParagraph();
        pied.Format.Alignment = ParagraphAlignment.Center;
        pied.Format.Font.Size = 9;
        pied.AddPageField();

        var lignes = texte.Replace("\r\n", "\n").Split('\n');
        for (var n = 0; n < lignes.Length; n++)
        {
            var l = lignes[n].TrimEnd();
            var p = sec.AddParagraph();
            if (l.Trim().Length == 0) continue;   // ligne vide = espace / blank line = space
            l = l.TrimStart();
            if (l.StartsWith("> ") || l == ">")
            {
                p.Format.Alignment = ParagraphAlignment.Center;
                l = l[1..].TrimStart();
            }
            double? taille = null;
            var souligne = false;
            if (l.StartsWith("### ")) { taille = 12; souligne = true; l = l[4..]; }
            else if (l.StartsWith("## ")) { taille = 16; l = l[3..]; }
            else if (l.StartsWith("# ")) { taille = 20; l = l[2..]; }
            else if (l.StartsWith("- ") || l.StartsWith("• "))
            {
                l = l[2..];
                p.Format.ListInfo.ListType = ListType.BulletList1;
                p.Format.ListInfo.NumberPosition = Unit.FromPoint(0);
                p.Format.LeftIndent = Unit.FromCentimeter(0.95);
                p.Format.FirstLineIndent = Unit.FromCentimeter(-0.95);
                p.Format.TabStops.AddTabStop(Unit.FromCentimeter(0.95));
            }
            if (taille is { } t)
            {
                // Titres : gras, gardes avec la suite / Headings: bold, kept with what follows
                p.Format.KeepWithNext = true;
                if (p.Format.Alignment != ParagraphAlignment.Center) p.Format.Alignment = ParagraphAlignment.Left;
                p.AddFormattedText(Ins(l.Replace("**", "")), new Font(Texte, t) { Bold = true, Underline = souligne ? Underline.Single : Underline.None });
                continue;
            }
            Ajouter(p, l);
        }
        var rendu = new PdfDocumentRenderer { Document = doc };
        rendu.RenderDocument();
        using var ms = new MemoryStream();
        rendu.PdfDocument.Save(ms, false);
        return ms.ToArray();
    }

    /// <summary>Texte avec **gras** / Text with **bold**.</summary>
    private static void Ajouter(Paragraph p, string l)
    {
        var morceaux = l.Split("**");
        for (var k = 0; k < morceaux.Length; k++)
        {
            if (morceaux[k].Length == 0) continue;
            var s = Ins(morceaux[k]);
            if (k % 2 == 1) p.AddFormattedText(s, TextFormat.Bold);
            else p.AddText(s);
        }
    }

    /// <summary>Espaces multiples conserves (MigraDoc les fusionne) / Keep repeated spaces (MigraDoc collapses them).</summary>
    private static string Ins(string s) => s.Replace("  ", "  ");

    // --- Import du Word / Word import ---

    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    /// <summary>
    /// Convertit un .docx en balisage : taille de police -> titres, gras + souligne -> sous-titre, centre -> « > »,
    /// liste -> « - », gras -> **. Leve InvalidDataException si ce n'est pas un Word.
    /// Converts a .docx to markup (font size -> headings, bold+underline -> subheading, centered, lists, bold).
    /// </summary>
    public static string ImporterDocx(byte[] docx)
    {
        XDocument xml;
        try
        {
            using var zip = new ZipArchive(new MemoryStream(docx), ZipArchiveMode.Read);
            var entree = zip.GetEntry("word/document.xml") ?? throw new InvalidDataException("Pas un document Word.");
            if (entree.Length > 20 * 1024 * 1024) throw new InvalidDataException("Document trop volumineux.");
            using var s = entree.Open();
            xml = XDocument.Load(s);
        }
        catch (Exception ex) when (ex is not InvalidDataException)
        {
            throw new InvalidDataException("Pas un document Word (.docx).", ex);
        }

        var sortie = new StringBuilder();
        var corps = xml.Root?.Element(W + "body");
        if (corps is null) return "";
        foreach (var para in corps.Descendants(W + "p"))
        {
            var ppr = para.Element(W + "pPr");
            var centre = ppr?.Element(W + "jc")?.Attribute(W + "val")?.Value == "center";
            var liste = ppr?.Element(W + "numPr") is not null;
            var runs = para.Descendants(W + "r")
                .Select(r => (Texte: TexteRun(r), Gras: Actif(r.Element(W + "rPr")?.Element(W + "b")),
                              Souligne: r.Element(W + "rPr")?.Element(W + "u")?.Attribute(W + "val")?.Value is { } u && u != "none",
                              Taille: int.TryParse(r.Element(W + "rPr")?.Element(W + "sz")?.Attribute(W + "val")?.Value, out var t) ? t : 24))
                .Where(r => r.Texte.Length > 0).ToList();
            var texte = string.Concat(runs.Select(r => r.Texte));
            if (texte.Trim().Length == 0) { sortie.Append('\n'); continue; }

            var visibles = runs.Where(r => r.Texte.Trim().Length > 0).ToList();
            var toutGras = visibles.All(r => r.Gras);
            var taille = visibles.Max(r => r.Taille);   // demi-points / half-points
            string ligne;
            if (taille >= 36) ligne = "# " + texte.Trim();
            else if (taille >= 28 && toutGras) ligne = "## " + texte.Trim();
            else if (toutGras && visibles.All(r => r.Souligne)) ligne = "### " + texte.Trim();
            else
            {
                // Gras par morceaux, en regroupant les runs voisins / Bold spans, merging neighbouring runs
                var b = new StringBuilder();
                foreach (var g in Grouper(runs))
                {
                    if (!g.Gras || g.Texte.Trim().Length == 0) { b.Append(g.Texte); continue; }
                    var debut = g.Texte.Length - g.Texte.TrimStart().Length;
                    var fin = g.Texte.Length - g.Texte.TrimEnd().Length;
                    b.Append(g.Texte[..debut]).Append("**").Append(g.Texte.Trim()).Append("**").Append(g.Texte[^fin..]);
                }
                ligne = b.ToString().Trim();
                if (liste) ligne = "- " + ligne;
            }
            if (centre) ligne = "> " + ligne;
            sortie.Append(ligne).Append('\n');
        }
        // Pas plus de deux lignes vides de suite / At most two blank lines in a row
        var resultat = System.Text.RegularExpressions.Regex.Replace(sortie.ToString(), "\n{4,}", "\n\n\n");
        return resultat.Trim('\n') + "\n";
    }

    private static IEnumerable<(string Texte, bool Gras)> Grouper(IEnumerable<(string Texte, bool Gras, bool Souligne, int Taille)> runs)
    {
        var courant = new StringBuilder();
        bool? gras = null;
        foreach (var r in runs)
        {
            // Un espace seul ne casse pas un passage gras / A lone space does not break a bold span
            var g = r.Texte.Trim().Length == 0 && gras is not null ? gras.Value : r.Gras;
            if (gras is not null && g != gras) { yield return (courant.ToString(), gras.Value); courant.Clear(); }
            gras = g;
            courant.Append(r.Texte);
        }
        if (gras is not null) yield return (courant.ToString(), gras.Value);
    }

    private static bool Actif(XElement? e) =>
        e is not null && (e.Attribute(W + "val")?.Value is null or "1" or "true" or "on");

    private static string TexteRun(XElement r)
    {
        var b = new StringBuilder();
        foreach (var e in r.Elements())
        {
            if (e.Name == W + "t") b.Append(e.Value);
            else if (e.Name == W + "tab" || e.Name == W + "br") b.Append(' ');
            else if (e.Name == W + "noBreakHyphen") b.Append('-');
        }
        return b.ToString();
    }
}
