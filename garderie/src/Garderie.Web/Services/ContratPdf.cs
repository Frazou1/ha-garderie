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

/// <summary>Signatures et NAS (jamais dans le JSON fige) / Signatures and SIN (never in the frozen JSON).</summary>
public sealed record SignaturesContrat(
    string? Nas,
    byte[]? SignatureParent, DateOnly? DateParent, string? NomParent,
    byte[]? SignatureRsge, DateOnly? DateRsge,
    DateOnly? ResilieLe, byte[]? SignatureResiliation, DateOnly? DateResiliation, string? NomResiliation);

/// <summary>
/// PDF de l'entente de service (mise en page du modele de la RSGE) et de la formule de resiliation, avec MigraDoc.
/// Service agreement PDF (RSGE's template layout) and termination form, with MigraDoc.
/// </summary>
public static class ContratPdf
{
    static ContratPdf() => PolicesPdf.Installer();

    private const string Police = "Garderie";

    public static byte[] Contrat(InfosContrat i, SignaturesContrat s)
    {
        var doc = Nouveau("Entente de service");
        var sec = doc.LastSection;

        var titre = sec.AddParagraph("Entente de service — Contrat");
        titre.Format.Font.Size = 16; titre.Format.Font.Bold = true; titre.Format.Alignment = ParagraphAlignment.Center;
        titre.Format.SpaceAfter = Unit.FromPoint(4);
        var sous = sec.AddParagraph(i.Garderie);
        sous.Format.Alignment = ParagraphAlignment.Center; sous.Format.SpaceAfter = Unit.FromPoint(8);

        Titre(sec, "Identification de l'enfant");
        Ligne(sec, "Nom et prénom", i.EnfantNom);
        Ligne(sec, "Adresse", i.EnfantAdresse);
        Ligne(sec, "Téléphone", i.EnfantTelephone);
        Ligne(sec, "Date de naissance", i.EnfantNaissance?.ToString("yyyy-MM-dd"));
        Ligne(sec, "Sexe", Affichage.Sexe(i.EnfantSexe));

        Titre(sec, "Identification du parent ou de la personne responsable de l'enfant");
        Ligne(sec, "Nom et prénom", i.ParentNom);
        Ligne(sec, "Adresse", i.ParentAdresse);
        Ligne(sec, "Téléphone 1", i.ParentTelephone1);
        Ligne(sec, "Téléphone 2", i.ParentTelephone2);
        Ligne(sec, "Lien avec l'enfant", i.ParentLien);
        Ligne(sec, "NAS du parent pour le relevé 24 (impôt)", s.Nas is { Length: 9 } n ? $"{n[..3]} {n[3..6]} {n[6..]}" : s.Nas);

        Titre(sec, "Identification du milieu familial");
        Ligne(sec, "Éducatrice-propriétaire", i.Rsge);
        Ligne(sec, "Adresse", i.AdresseGarderie);
        Ligne(sec, "Téléphone", i.TelephoneGarderie);
        Ligne(sec, "Heures d'ouverture", i.Heures);

        Titre(sec, "Fermetures prévues du service de garde");
        if (i.Fermetures.Count == 0) Texte(sec, "Aucune fermeture inscrite pour la période du contrat.");
        foreach (var f in i.Fermetures) Puce(sec, f);
        if (!string.IsNullOrWhiteSpace(i.NoteVacances)) Texte(sec, i.NoteVacances);
        Ligne(sec, "Vacances du parent, si dates connues", string.IsNullOrWhiteSpace(i.VacancesParent) ? "—" : i.VacancesParent);

        Titre(sec, "Les obligations de l'éducatrice");
        Liste(sec, i.ObligationsRsge);
        Titre(sec, "Les obligations du parent ou de la personne responsable de l'enfant");
        Liste(sec, i.ObligationsParent);
        Titre(sec, "Mention exigée par la Loi sur la protection du consommateur");
        Liste(sec, i.MentionLpc);

        Titre(sec, "Durée de l'entente contractuelle");
        Ligne(sec, "Le présent contrat prend effet le", i.Debut.ToString("yyyy-MM-dd"));
        Ligne(sec, "Le présent contrat prendra fin le", i.Fin.ToString("yyyy-MM-dd"));

        var bloc = Texte(sec, i.Attestation);
        bloc.Format.SpaceBefore = Unit.FromPoint(10);
        bloc.Format.KeepWithNext = true;
        Texte(sec, i.NonRespect).Format.KeepWithNext = true;

        var signe = Texte(sec, $"Signé à {i.Ville ?? "            "}, le {s.DateParent?.ToString("yyyy-MM-dd") ?? "            "}");
        signe.Format.SpaceBefore = Unit.FromPoint(8); signe.Format.KeepWithNext = true;
        Ligne(sec, "Nom du parent (lettres moulées)", s.NomParent?.ToUpperInvariant()).Format.KeepWithNext = true;
        Signature(sec, "Signature du parent", s.SignatureParent, s.DateParent).Format.KeepWithNext = true;
        Signature(sec, "Signature de l'éducatrice", s.SignatureRsge, s.DateRsge);

        // Formule de resiliation (vide, comme sur le modele papier) / Termination form (blank, as on paper)
        var trait = sec.AddParagraph();
        trait.Format.SpaceBefore = Unit.FromPoint(16);
        trait.Format.Borders.Top.Width = Unit.FromPoint(0.75);
        trait.Format.Borders.Top.Style = MigraDoc.DocumentObjectModel.BorderStyle.DashLargeGap;
        Formule(sec, i, null);
        return Rendre(doc);
    }

    /// <summary>Formule de resiliation remplie et signee / Filled and signed termination form.</summary>
    public static byte[] Resiliation(InfosContrat i, SignaturesContrat s)
    {
        var doc = Nouveau("Résiliation du contrat");
        var sec = doc.LastSection;
        var titre = sec.AddParagraph("Résiliation de l'entente de service");
        titre.Format.Font.Size = 16; titre.Format.Font.Bold = true; titre.Format.Alignment = ParagraphAlignment.Center;
        titre.Format.SpaceAfter = Unit.FromPoint(10);
        Ligne(sec, "Enfant", i.EnfantNom);
        Ligne(sec, "Contrat", $"du {i.Debut:yyyy-MM-dd} au {i.Fin:yyyy-MM-dd}");
        Ligne(sec, "Date de résiliation", s.ResilieLe?.ToString("yyyy-MM-dd"));
        Formule(sec, i, s);
        return Rendre(doc);
    }

    private static void Formule(Section sec, InfosContrat i, SignaturesContrat? s)
    {
        Titre(sec, "Formule de résiliation");
        const string blanc = "______________";
        Texte(sec, $"À {i.Rsge ?? blanc}, éducatrice-propriétaire du service de garde, ci-nommée au contrat, " +
                   $"je résilie le contrat conclu le {s?.DateParent?.ToString("yyyy-MM-dd") ?? blanc}" +
                   (s?.ResilieLe is { } r ? $", à compter du {r:yyyy-MM-dd}." : "."));
        Texte(sec, $"Signé à {(s is null ? blanc : i.Ville ?? blanc)}, le {s?.DateResiliation?.ToString("yyyy-MM-dd") ?? blanc}");
        Ligne(sec, "Nom du parent (lettres moulées)", s?.NomResiliation?.ToUpperInvariant());
        Signature(sec, "Signature du parent", s?.SignatureResiliation, s?.DateResiliation);
    }

    // --- Mise en page / Layout ---

    private static Document Nouveau(string titre)
    {
        var doc = new Document();
        doc.Info.Title = titre;
        var normal = doc.Styles[StyleNames.Normal]!;
        normal.Font.Name = Police;
        normal.Font.Size = 9.5;
        normal.ParagraphFormat.SpaceAfter = Unit.FromPoint(3);
        var sec = doc.AddSection();
        sec.PageSetup = doc.DefaultPageSetup.Clone();
        sec.PageSetup.PageFormat = PageFormat.Letter;
        sec.PageSetup.LeftMargin = sec.PageSetup.RightMargin = Unit.FromCentimeter(2.2);
        sec.PageSetup.TopMargin = Unit.FromCentimeter(1.8);
        sec.PageSetup.BottomMargin = Unit.FromCentimeter(1.8);
        var pied = sec.Footers.Primary.AddParagraph();
        pied.Format.Alignment = ParagraphAlignment.Right;
        pied.Format.Font.Size = 8;
        pied.AddText("Page "); pied.AddPageField(); pied.AddText(" de "); pied.AddNumPagesField();
        return doc;
    }

    private static void Titre(Section sec, string texte)
    {
        var p = sec.AddParagraph(texte);
        p.Format.Font.Bold = true;
        p.Format.Font.Size = 11;
        p.Format.SpaceBefore = Unit.FromPoint(10);
        p.Format.SpaceAfter = Unit.FromPoint(4);
        p.Format.KeepWithNext = true;
        p.Format.Borders.Bottom.Width = Unit.FromPoint(0.75);
        p.Format.Borders.Bottom.Color = Colors.Gray;
    }

    private static Paragraph Ligne(Section sec, string libelle, string? valeur)
    {
        var p = sec.AddParagraph();
        p.AddFormattedText(libelle + " : ", TextFormat.Bold);
        p.AddText(string.IsNullOrWhiteSpace(valeur) ? "____________________" : valeur);
        return p;
    }

    private static Paragraph Texte(Section sec, string texte)
    {
        var p = sec.AddParagraph(texte);
        p.Format.Alignment = ParagraphAlignment.Justify;
        return p;
    }

    private static void Puce(Section sec, string texte)
    {
        var p = sec.AddParagraph("•\t" + texte);
        p.Format.LeftIndent = Unit.FromCentimeter(0.6);
        p.Format.FirstLineIndent = Unit.FromCentimeter(-0.4);
        p.Format.TabStops.AddTabStop(Unit.FromCentimeter(0.6));
        p.Format.Alignment = ParagraphAlignment.Justify;
    }

    /// <summary>Lignes « - » en puces, les autres en paragraphes / "-" lines as bullets, others as paragraphs.</summary>
    private static void Liste(Section sec, string texte)
    {
        foreach (var brut in texte.Split('\n'))
        {
            var l = brut.Trim();
            if (l.Length == 0) continue;
            if (l.StartsWith('-')) Puce(sec, l.TrimStart('-', ' ', '–'));
            else Texte(sec, l);
        }
    }

    private static Paragraph Signature(Section sec, string libelle, byte[]? png, DateOnly? date)
    {
        var p = sec.AddParagraph();
        p.Format.SpaceBefore = Unit.FromPoint(6);
        p.Format.KeepTogether = true;
        p.AddFormattedText(libelle + " : ", TextFormat.Bold);
        if (png is not null)
        {
            var img = p.AddImage("base64:" + Convert.ToBase64String(png));
            img.Height = Unit.FromCentimeter(1.3);
            img.LockAspectRatio = true;
            p.AddText($"    Date : {date:yyyy-MM-dd}");
        }
        else
        {
            p.AddText("__________________________________    Date : ____________");
        }
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
