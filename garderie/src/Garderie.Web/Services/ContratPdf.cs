using MigraDoc.DocumentObjectModel;
using MigraDoc.Rendering;
using Document = MigraDoc.DocumentObjectModel.Document;
using Sexe = Garderie.Web.Data.Sexe;

namespace Garderie.Web.Services;

/// <summary>
/// Renseignements et textes d'un contrat, figes a la signature du parent (serialises en JSON, sans NAS).
/// Contract info and texts, frozen when the parent signs (JSON, no SIN).
/// </summary>
public sealed record InfosContrat
{
    public string Garderie { get; init; } = "";
    public string? Rsge { get; init; }
    public string? AdresseGarderie { get; init; }
    public string? TelephoneGarderie { get; init; }
    public string? Heures { get; init; }
    public string EnfantNom { get; init; } = "";
    public string? EnfantAdresse { get; init; }
    public string? EnfantTelephone { get; init; }
    public DateOnly? EnfantNaissance { get; init; }
    public Sexe? EnfantSexe { get; init; }
    public string ParentNom { get; init; } = "";
    public string? ParentAdresse { get; init; }
    public string? ParentTelephone1 { get; init; }
    public string? ParentTelephone2 { get; init; }
    public string? ParentLien { get; init; }
    public List<string> Fermetures { get; init; } = new();
    /// <summary>Fermetures detaillees (feries d'un jour, vacances sur plusieurs jours) / Detailed closures.</summary>
    public List<FermetureContrat> FermeturesDetail { get; init; } = new();
    public string? NoteVacances { get; init; }
    public string? VacancesParent { get; init; }
    public string ObligationsRsge { get; init; } = "";
    public string ObligationsParent { get; init; } = "";
    public string MentionLpc { get; init; } = "";
    public string Attestation { get; init; } = "";
    public string NonRespect { get; init; } = "";
    public string? Ville { get; init; }
    public DateOnly Debut { get; init; }
    public DateOnly Fin { get; init; }
}

/// <summary>Une fermeture du contrat : un jour (ferie) ou une periode (vacances) / A closure: one day or a period.</summary>
public sealed record FermetureContrat(string Motif, DateOnly Debut, DateOnly Fin, string? Demi);

/// <summary>Signatures et NAS (jamais dans le JSON fige) / Signatures and SIN (never in the frozen JSON).</summary>
public sealed record SignaturesContrat(
    string? Nas,
    byte[]? SignatureParent, DateOnly? DateParent, string? NomParent,
    byte[]? SignatureRsge, DateOnly? DateRsge,
    DateOnly? ResilieLe, byte[]? SignatureResiliation, DateOnly? DateResiliation, string? NomResiliation);

/// <summary>
/// PDF de l'entente de service, fidele au modele Word de la RSGE (« Entente service (Contrat) 2018 ») :
/// Elephant Pro 16 gras pour le titre, Comic Sans MS 12 pour le texte, Times New Roman 9 gras justifie pour les
/// clauses, Calibri 11,5 gras souligne pour leurs titres, valeurs soulignees, dates jj/mm/aaaa.
/// Les polices viennent de Reglages (televersees) ; sinon une police de remplacement est utilisee.
/// Service agreement PDF faithful to the RSGE's Word template (fonts uploaded in Settings, else fallbacks).
/// </summary>
public static class ContratPdf
{
    static ContratPdf() => PolicesPdf.Installer();

    // Familles versionnees (voir PolicesPdf.Famille) / Versioned families
    private static string Texte => PolicesPdf.Famille("ContratTexte");
    private static string Titre => PolicesPdf.Famille("ContratTitre");
    private static string Clauses => PolicesPdf.Famille("ContratClauses");
    private static string SousTitre => PolicesPdf.Famille("ContratSousTitre");
    private static readonly Unit Retrait = Unit.FromPoint(106.2);   // retrait des titres du modele / template heading indent

    private static string D(DateOnly? d) => d?.ToString("dd'/'MM'/'yyyy", System.Globalization.CultureInfo.InvariantCulture) ?? "";

    public static byte[] Contrat(InfosContrat i, SignaturesContrat s)
    {
        var doc = Nouveau("Entente de service");
        var sec = doc.LastSection;

        var t1 = sec.AddParagraph();
        t1.Format.Alignment = ParagraphAlignment.Center;
        t1.AddFormattedText("Entente de service-", new Font(Titre, 16) { Bold = true });
        var t2 = sec.AddParagraph();
        t2.Format.LeftIndent = Retrait;
        t2.AddFormattedText("Contrat-", new Font(Titre, 16) { Bold = true });
        Vide(sec);

        TitreSection(sec, "Identification de l'enfant", retrait: true);
        Champ(sec, "Nom et prénom: ", i.EnfantNom, 44);
        Champ(sec, "Adresse: ", i.EnfantAdresse, 49);
        Champ(sec, "Téléphone: ", i.EnfantTelephone, 19);
        var n = Champ(sec, "Date de naissance: ", D(i.EnfantNaissance), 15);
        // Espaces insecables : MigraDoc fusionne les espaces ordinaires / Non-breaking spaces (MigraDoc collapses spaces)
        n.AddText(Ins("      Sexe : M "));
        n.AddText(Ins(i.EnfantSexe == Sexe.M ? "( X )" : "(    )"));
        n.AddText(Ins("     F "));
        n.AddText(Ins(i.EnfantSexe == Sexe.F ? "( X )" : "(    )"));
        Vide(sec);
        Vide(sec);

        TitreSection(sec, "Identification du parent ou de la personne responsable de l'enfant", retrait: false);
        Champ(sec, "Nom et prénom: ", i.ParentNom, 44);
        Champ(sec, "Adresse: ", i.ParentAdresse, 49);
        var tel = Champ(sec, "Téléphone1: ", i.ParentTelephone1, 19);
        tel.AddText(" Téléphone 2: ");
        Valeur(tel, i.ParentTelephone2, 18);
        Champ(sec, "Lien avec l'enfant: ", i.ParentLien, 17);
        Champ(sec, "NAS du parent pour le relevé 24 (impôt) : ", s.Nas is { Length: 9 } nas ? $"{nas[..3]} {nas[3..6]} {nas[6..]}" : s.Nas, 26);
        Vide(sec);

        TitreSection(sec, "Identification du milieu familial", retrait: true);
        Champ(sec, "Éducatrice-propriétaire: ", i.Rsge, 30);
        Champ(sec, "Adresse: ", i.AdresseGarderie, 40);
        Champ(sec, "Téléphone: ", i.TelephoneGarderie, 19);
        Champ(sec, "Heure d'ouverture : ", i.Heures, 20);
        Vide(sec);

        TitreSection(sec, "Fermeture prévue du service de garde", retrait: false);
        var feries = i.FermeturesDetail.Where(f => f.Debut == f.Fin).ToList();
        var periodes = i.FermeturesDetail.Where(f => f.Debut != f.Fin).ToList();
        if (feries.Count > 0)
        {
            // Deux colonnes, comme le modele : « Jour de l'an ( X ) 01/01/2026 » / Two columns, like the template
            var table = sec.AddTable();
            table.Format.Font = new Font(Texte, 12);
            table.AddColumn(Unit.FromCentimeter(8.2));
            table.AddColumn(Unit.FromCentimeter(8.2));
            var moitie = (feries.Count + 1) / 2;
            for (var r = 0; r < moitie; r++)
            {
                var ligne = table.AddRow();
                for (var col = 0; col < 2; col++)
                {
                    var k = r + col * moitie;
                    if (k >= feries.Count) continue;
                    var f = feries[k];
                    ligne.Cells[col].AddParagraph($"{f.Motif} ( X ) {D(f.Debut)}{(f.Demi is null ? "" : $" ({f.Demi})")}");
                }
            }
            Vide(sec);
        }
        sec.AddParagraph("Vacance(s) de l'éducatrice, si dates connues : ").Format.Alignment = ParagraphAlignment.Justify;
        foreach (var p in periodes)
            Souligne(sec, $"*** {p.Motif} du {D(p.Debut)} au {D(p.Fin)} inclusivement");
        if (!string.IsNullOrWhiteSpace(i.NoteVacances)) Souligne(sec, "*** " + i.NoteVacances);
        // Anciens contrats figes sans detail : lignes texte / Older frozen contracts without detail
        if (i.FermeturesDetail.Count == 0) foreach (var f in i.Fermetures) Souligne(sec, "*** " + f);
        Vide(sec);
        var vp = sec.AddParagraph("Vacance(s) du parent, si dates connues : ");
        vp.Format.Alignment = ParagraphAlignment.Justify;
        if (!string.IsNullOrWhiteSpace(i.VacancesParent)) vp.AddFormattedText(i.VacancesParent, TextFormat.Underline);
        else vp.AddText("____________________");
        Vide(sec);

        TitreClauses(sec, "Les obligations de l'éducatrice ", SousTitre);
        BlocClauses(sec, i.ObligationsRsge);
        TitreClauses(sec, "Les obligations du parent ou de la personne responsable de l'enfant", Clauses);
        BlocClauses(sec, i.ObligationsParent);
        TitreClauses(sec, "Mention exigée par la loi sur la protection du consommateur ", SousTitre);
        BlocClauses(sec, i.MentionLpc);

        var duree = sec.AddParagraph();
        duree.Format.Alignment = ParagraphAlignment.Center;
        duree.Format.KeepWithNext = true;
        duree.AddFormattedText("Durée de l'entente contractuelle ", new Font(Texte, 12) { Bold = true, Underline = Underline.Single });
        DateGras(sec, "Le présent contrat prend effet le : ", i.Debut);
        DateGras(sec, "Le présent contrat prendra fin le : ", i.Fin);
        Vide(sec);

        var att = Gras(sec, i.Attestation);
        att.Format.KeepWithNext = true;
        Gras(sec, i.NonRespect).Format.KeepWithNext = true;
        Vide(sec).Format.KeepWithNext = true;

        var signe = sec.AddParagraph(Ins("Signé à   "));
        signe.Format.KeepWithNext = true;
        Valeur(signe, i.Ville, 12);
        signe.AddText(Ins("      le  "));
        Valeur(signe, D(s.DateParent), 12);
        var nom = sec.AddParagraph("Nom du parent (lettres moulées) : ");
        nom.Format.KeepWithNext = true;
        Valeur(nom, s.NomParent?.ToUpperInvariant(), 24);
        Signature(sec, "Signature du parent : ", s.SignatureParent).Format.KeepWithNext = true;
        Signature(sec, "Signature de l'éducatrice : ", s.SignatureRsge);
        Vide(sec);

        sec.AddParagraph(new string('-', 78));
        Formule(sec, i, null);
        return Rendre(doc);
    }

    /// <summary>Formule de resiliation remplie et signee / Filled and signed termination form.</summary>
    public static byte[] Resiliation(InfosContrat i, SignaturesContrat s)
    {
        var doc = Nouveau("Résiliation du contrat");
        var sec = doc.LastSection;
        var t1 = sec.AddParagraph();
        t1.Format.Alignment = ParagraphAlignment.Center;
        t1.AddFormattedText("Entente de service-", new Font(Titre, 16) { Bold = true });
        Vide(sec);
        Champ(sec, "Enfant: ", i.EnfantNom, 30);
        Champ(sec, "Parent: ", i.ParentNom, 30);
        var c = Champ(sec, "Contrat du ", D(i.Debut), 10);
        c.AddText(" au ");
        Valeur(c, D(i.Fin), 10);
        Vide(sec);
        Formule(sec, i, s);
        return Rendre(doc);
    }

    private static void Formule(Section sec, InfosContrat i, SignaturesContrat? s)
    {
        var t = sec.AddParagraph();
        t.Format.Alignment = ParagraphAlignment.Center;
        t.Format.KeepWithNext = true;
        t.AddFormattedText("Formule de résiliation ", new Font(Texte, 12) { Bold = true, Underline = Underline.Single });
        Vide(sec).Format.KeepWithNext = true;
        var p = sec.AddParagraph();
        p.Format.Font.Bold = true;
        p.Format.Alignment = ParagraphAlignment.Justify;
        p.Format.KeepWithNext = true;
        p.AddText("À ");
        Valeur(p, i.Rsge, 16);
        p.AddText(", éducatrice-propriétaire du service de garde, ci-nommé au contrat, je résilie le contrat conclu le ");
        Valeur(p, s is null ? null : D(s.DateParent), 12);
        if (s?.ResilieLe is { } r) { p.AddText(", à compter du "); Valeur(p, D(r), 10); }
        Vide(sec).Format.KeepWithNext = true;
        var signe = sec.AddParagraph(Ins("Signé à   "));
        signe.Format.KeepWithNext = true;
        Valeur(signe, s is null ? null : i.Ville, 12);
        signe.AddText(Ins("      le  "));
        Valeur(signe, s is null ? null : D(s.DateResiliation), 12);
        var nom = sec.AddParagraph("Nom du parent (lettres moulées) : ");
        nom.Format.KeepWithNext = true;
        Valeur(nom, s?.NomResiliation?.ToUpperInvariant(), 24);
        Signature(sec, "Signature du parent : ", s?.SignatureResiliation);
    }

    // --- Mise en page du modele / Template layout ---

    private static Document Nouveau(string titre)
    {
        var doc = new Document();
        doc.Info.Title = titre;
        var normal = doc.Styles[StyleNames.Normal]!;
        normal.Font.Name = Texte;
        normal.Font.Size = 12;
        normal.ParagraphFormat.SpaceAfter = Unit.FromPoint(0);
        normal.ParagraphFormat.LineSpacingRule = LineSpacingRule.Multiple;
        normal.ParagraphFormat.LineSpacing = 1.15;
        var sec = doc.AddSection();
        sec.PageSetup = doc.DefaultPageSetup.Clone();
        sec.PageSetup.PageFormat = PageFormat.Letter;
        sec.PageSetup.LeftMargin = sec.PageSetup.RightMargin = Unit.FromInch(1.25);
        sec.PageSetup.TopMargin = sec.PageSetup.BottomMargin = Unit.FromInch(1);
        return doc;
    }

    private static Paragraph Vide(Section sec) => sec.AddParagraph();

    /// <summary>Espaces -> espaces insecables (conserves a l'affichage) / Spaces -> non-breaking spaces.</summary>
    private static string Ins(string s) => s.Replace(' ', '\u00A0');

    private static void TitreSection(Section sec, string texte, bool retrait)
    {
        var p = sec.AddParagraph();
        if (retrait) p.Format.LeftIndent = Retrait;
        p.Format.KeepWithNext = true;
        p.AddFormattedText(texte, TextFormat.Bold);
    }

    /// <summary>« Libelle: » puis valeur soulignee, ou une ligne a remplir / Label then underlined value or a blank line.</summary>
    private static Paragraph Champ(Section sec, string libelle, string? valeur, int blancs)
    {
        var p = sec.AddParagraph(libelle);
        Valeur(p, valeur, blancs);
        return p;
    }

    private static void Valeur(Paragraph p, string? valeur, int blancs)
    {
        if (string.IsNullOrWhiteSpace(valeur)) p.AddText(new string('_', blancs));
        else p.AddFormattedText(valeur, TextFormat.Underline);
    }

    private static void Souligne(Section sec, string texte)
    {
        var p = sec.AddParagraph();
        p.Format.Alignment = ParagraphAlignment.Justify;
        p.AddFormattedText(texte, TextFormat.Underline);
    }

    private static void TitreClauses(Section sec, string texte, string police)
    {
        var p = sec.AddParagraph();
        p.Format.Alignment = ParagraphAlignment.Center;
        p.Format.KeepWithNext = true;
        p.AddFormattedText(texte, new Font(police, 11.5) { Bold = true, Underline = Underline.Single });
    }

    /// <summary>
    /// Clauses en un seul paragraphe justifie, Times 9 gras, chaque point precede d'un « - » (comme le modele).
    /// Clauses as one justified paragraph, Times 9 bold, each point preceded by "-" (as in the template).
    /// </summary>
    private static void BlocClauses(Section sec, string texte)
    {
        var morceaux = texte.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0)
            .Select(l => l.StartsWith('-') ? "-" + l.TrimStart('-', ' ', '–') : l);
        var p = sec.AddParagraph();
        p.Format.Alignment = ParagraphAlignment.Justify;
        p.Format.SpaceAfter = Unit.FromPoint(10);
        p.AddFormattedText(string.Join(" ", morceaux), new Font(Clauses, 9) { Bold = true });
    }

    private static void DateGras(Section sec, string libelle, DateOnly date)
    {
        var p = sec.AddParagraph();
        p.AddFormattedText(libelle, TextFormat.Bold);
        p.AddFormattedText(D(date), TextFormat.Underline);
    }

    private static Paragraph Gras(Section sec, string texte)
    {
        var p = sec.AddParagraph();
        p.Format.Alignment = ParagraphAlignment.Justify;
        p.Format.SpaceAfter = Unit.FromPoint(10);
        p.AddFormattedText(texte, TextFormat.Bold);
        return p;
    }

    private static Paragraph Signature(Section sec, string libelle, byte[]? png)
    {
        var p = sec.AddParagraph(libelle);
        p.Format.SpaceBefore = Unit.FromPoint(4);
        p.Format.KeepTogether = true;
        if (png is not null)
        {
            var img = p.AddImage("base64:" + Convert.ToBase64String(png));
            img.Height = Unit.FromCentimeter(1.3);
            img.LockAspectRatio = true;
        }
        else p.AddText(new string('_', 28));
        return p;
    }

    private static byte[] Rendre(Document doc)
    {
        var rendu = new PdfDocumentRenderer { Document = doc };
        rendu.RenderDocument();
        using var ms = new MemoryStream();
        rendu.PdfDocument.Save(ms, false);
        return ms.ToArray();
    }
}
