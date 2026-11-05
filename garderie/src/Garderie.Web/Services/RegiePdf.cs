using MigraDoc.DocumentObjectModel;
using MigraDoc.Rendering;
using Document = MigraDoc.DocumentObjectModel.Document;

namespace Garderie.Web.Services;

/// <summary>
/// PDF de la regie interne a partir du balisage compose par RegieInterne (Comic Sans, comme le Word de la RSGE).
/// Balisage, une ligne = un paragraphe : « # » titre, « ## » section, « ### » sous-titre gras souligne,
/// « - » point de liste, « > » centre, **gras** ; ligne vide = espace.
/// Internal rules PDF from the markup composed by RegieInterne.
/// </summary>
public static class RegiePdf
{
    static RegiePdf() => PolicesPdf.Installer();

    private static string Texte => PolicesPdf.Famille("ContratTexte");

    // --- Generation / Rendering ---

    public static byte[] Generer(string texte)
    {
        PolicesPdf.Installer();

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
}
