using System.Text.RegularExpressions;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using Document = MigraDoc.DocumentObjectModel.Document;
using Sexe = Garderie.Web.Data.Sexe;

namespace Garderie.Web.Services;

/// <summary>
/// Entente de services de garde educatifs, mise en page du modele du bureau coordonnateur (formulaire papier) :
/// identification sur deux colonnes, encadre de l'enfant, tableaux d'horaire, cases des feries, paiement,
/// mention LPC et signatures date / lieu. Les valeurs remplies sont en bleu, comme a la main.
/// Service agreement in the coordinating office's paper layout (filled values in blue).
/// </summary>
public static partial class ContratBcPdf
{
    static ContratBcPdf() => PolicesPdf.Installer();

    private const string Police = "ContratBc";          // DejaVu Sans (conteneur) / Arial (Windows)
    private static readonly Color Encre = new(0x1F, 0x3A, 0x93);
    private static readonly Unit Largeur = Unit.FromCentimeter(18.6);
    private const string Coche = "[X]";
    private const string Vide = "[  ]";

    /// <summary>Feries du modele, dans l'ordre des colonnes du formulaire / Template holidays, in form column order.</summary>
    private static readonly (string Libelle, string Motif)[] FeriesModele =
    [
        ("Le jour de l'An", "Jour de l'an"), ("Le lendemain du jour de l'An", "Le 2 janvier"),
        ("Le Vendredi saint", "Vendredi saint"), ("Le lundi de Pâques", "Lundi de Pâques"),
        ("La Journée nationale des patriotes", "Fête des patriotes"),
        ("La fête nationale du Québec", "Fête nationale"), ("La fête du Canada", "Fête du Canada"),
        ("La fête du Travail", "Fête du travail"), ("L'Action de grâces", "Action de grâce"), ("Le jour de Noël", "Jour de Noël"),
        ("Le lendemain du jour de Noël", "Lendemain de Noël"),
    ];

    public static byte[] Contrat(InfosContrat i, SignaturesContrat s)
    {
        var doc = Nouveau();
        var sec = doc.LastSection;

        var titre = sec.AddParagraph("ENTENTE DE SERVICES DE GARDE ÉDUCATIFS");
        titre.Format.Alignment = ParagraphAlignment.Center;
        titre.Format.Font.Size = 15;
        titre.Format.SpaceAfter = Unit.FromPoint(12);

        Identification(sec, i, s);
        Enfant(sec, i);

        Titre(sec, "Description des services fournis");
        sec.AddParagraph("Pendant la durée de la présente entente, la prestataire de services s'engage à fournir à l'enfant ce qui suit :");
        foreach (var l in Lignes(i.Services ?? "")) Puce(sec).AddText(l.TrimStart('-', '–', ' '));
        var col = Puce(sec);
        col.AddText("Les collations sont servies vers ");
        Valeur(col, i.HeureCollationMatin, 10);
        col.AddText(" le matin et vers ");
        Valeur(col, i.HeureCollationPm, 10);
        col.AddText(" l'après-midi.");
        var repas = Puce(sec);
        repas.AddText("Le repas du midi est servi vers ");
        Valeur(repas, i.HeureDiner, 10);
        repas.AddText(".");

        Titre(sec, "Horaire du service de garde");
        Puce(sec).AddText("Le service de garde sera ouvert selon l'horaire suivant :");
        string[] ouvert = ["Lundi", "Mardi", "Mercredi", "Jeudi", "Vendredi"];
        Semaine(sec, j => ouvert.Contains(j) ? (i.HeureOuverture, i.HeureFermeture) : ("—", "—"));

        Fermetures(sec, i);

        Titre(sec, "Fréquentation de l'enfant");
        Puce(sec).AddText("Le parent retient les services de garde pour son enfant selon l'horaire suivant :");
        var freq = i.Frequentation ?? [];
        Semaine(sec, j =>
        {
            var hj = i.HeuresFrequentation?.FirstOrDefault(x => x.Jour == j);
            var (ouv, ferm) = (hj?.De ?? i.HeureOuverture, hj?.A ?? i.HeureFermeture);
            if (hj is not null) return (ouv, ferm);
            if (freq.Contains(j)) return (i.HeureOuverture, i.HeureFermeture);
            if (freq.Contains(j + " (matin)")) return (i.HeureOuverture, "12 h");
            if (freq.Contains(j + " (après-midi)")) return ("12 h", i.HeureFermeture);
            return ("X", "X");
        });
        var deb = Puce(sec);
        deb.AddText("Date de début de fréquentation : ");
        Valeur(deb, LongueDate(i.DebutFrequentation), 20);

        Duree(sec, i);
        Paiement(sec, i);

        Titre(sec, "Frais de retards");
        var r = Puce(sec);
        var (montant, minutes) = Retard(i);
        r.AddText("Un montant de ");
        Valeur(r, montant, 10);
        r.AddText(" $ pour chaque tranche de ");
        Valeur(r, minutes, 6);
        r.AddText(" minutes sera exigé si le parent arrive après l'heure de fermeture du service de garde.");

        Titre(sec, "Résiliation de l'entente par le Prestataire");
        foreach (var l in Lignes(i.ResiliationRsge ?? "")) Justifie(sec, l);
        Titre(sec, "Résiliation de l'entente par le Parent");
        foreach (var l in Lignes(i.ResiliationParent ?? "")) Justifie(sec, l);

        var lpc = sec.AddParagraph();
        lpc.Format.Alignment = ParagraphAlignment.Center;
        lpc.Format.SpaceBefore = Unit.FromPoint(14);
        lpc.Format.SpaceAfter = Unit.FromPoint(6);
        lpc.Format.KeepWithNext = true;
        lpc.AddFormattedText("MENTION EXIGÉE PAR LA LOI SUR LA PROTECTION DU CONSOMMATEUR", new Font(Police, 10) { Underline = Underline.Single });
        var lettre = 'a';
        foreach (var l in Lignes(i.MentionLpc))
        {
            var p = Justifie(sec, l.StartsWith('-') ? $"{lettre++}.  " + l.TrimStart('-', ' ') : l);
            if (l.StartsWith('-')) { p.Format.LeftIndent = Unit.FromCentimeter(1); p.Format.FirstLineIndent = Unit.FromCentimeter(-0.5); }
        }

        Duree(sec, i);
        Signatures(sec, i, s);

        sec.AddPageBreak();
        Formule(sec, i);
        return Rendre(doc);
    }

    // --- Blocs du formulaire / Form blocks ---

    private static void Identification(Section sec, InfosContrat i, SignaturesContrat s)
    {
        var t = sec.AddTable();
        t.AddColumn(Unit.FromCentimeter(9.3));
        t.AddColumn(Unit.FromCentimeter(9.3));
        var ligne = t.AddRow();
        var g = ligne.Cells[0];
        var d = ligne.Cells[1];
        SousTitre(g, "Identification du prestataire de services");
        SousTitre(d, "Identification du parent");

        var (rp, rn) = Couper(i.Rsge);
        var (ra, rc) = Adresse(i.AdresseGarderie);
        Champ(g, "Prénom : ", rp);
        Champ(g, "Nom : ", rn);
        Champ(g, "Adresse : ", ra);
        Champ(g, "Code postal : ", rc);
        Champ(g, "Courriel : ", i.RsgeCourriel);
        Champ(g, "N° de tél. : ", i.TelephoneGarderie);

        var (pp, pn) = i.ParentPrenom is not null ? (i.ParentPrenom, i.ParentNomFamille) : Couper(i.ParentNom);
        var (pa, pc) = Adresse(i.ParentAdresse);
        Champ(d, "Prénom : ", pp);
        Champ(d, "Nom : ", pn);
        Champ(d, "Adresse : ", pa);
        Champ(d, "Code postal : ", pc);
        Champ(d, "NAS POUR RELEVÉS IMPÔTS : ", s.Nas is { Length: 9 } nas ? $"{nas[..3]} {nas[3..6]} {nas[6..]}" : s.Nas);
        Champ(d, "Courriel : ", i.ParentCourriel);
        Champ(d, "N° de tél. : ", i.ParentTelephone1);
        Champ(d, "N° de tél. autre : ", i.ParentTelephone2);
        if (!string.IsNullOrWhiteSpace(i.ParentTelephoneAutre)) Champ(d, "Cellulaire papa : ", i.ParentTelephoneAutre);
        sec.AddParagraph().Format.SpaceAfter = Unit.FromPoint(4);
    }

    private static void Enfant(Section sec, InfosContrat i)
    {
        var t = sec.AddTable();
        t.Borders.Width = 0.75;
        t.AddColumn(Largeur);
        var c = t.AddRow().Cells[0];
        c.Format.LeftIndent = Unit.FromPoint(4);
        var titre = c.AddParagraph("Identification de l'enfant");
        titre.Format.SpaceBefore = Unit.FromPoint(4);
        titre.Format.SpaceAfter = Unit.FromPoint(6);
        var (ep, en) = i.EnfantPrenom is not null ? (i.EnfantPrenom, i.EnfantNomFamille) : Couper(i.EnfantNom);
        var n = c.AddParagraph("Prénom : ");
        Valeur(n, ep, 25);
        n.AddText(Ins("        Nom : "));
        Valeur(n, en, 25);
        n.Format.SpaceAfter = Unit.FromPoint(6);
        var dn = c.AddParagraph("Date de naissance : ");
        Valeur(dn, LongueDate(i.EnfantNaissance), 20);
        dn.AddText(Ins("        Sexe : "));
        dn.AddText(Ins($"{(i.EnfantSexe == Sexe.F ? Coche : Vide)} F   {(i.EnfantSexe == Sexe.M ? Coche : Vide)} M"));
        dn.Format.SpaceAfter = Unit.FromPoint(6);
        var po = c.AddParagraph("Numéro inscription portail : ");
        Valeur(po, i.EnfantPortail, 25);
        po.Format.SpaceAfter = Unit.FromPoint(4);
    }

    private static void Fermetures(Section sec, InfosContrat i)
    {
        Titre(sec, "Fermeture du service de garde");
        Puce(sec).AddText("Le service de garde sera fermé les jours fériés suivants :");

        var motifsFeries = Feries.Calculer(2026).Select(f => f.Nom).ToHashSet();
        var jours = i.FermeturesDetail.Where(f => f.Debut == f.Fin && f.Demi is null && motifsFeries.Contains(f.Motif))
            .Select(f => f.Motif).ToHashSet();
        var autres = jours.Where(m => !FeriesModele.Any(x => x.Motif == m)).Distinct().Take(2).ToList();
        var cases = FeriesModele.Select(x => (x.Libelle, Coche: jours.Contains(x.Motif))).ToList();
        cases.AddRange(autres.Select(m => (m, true)));
        while (cases.Count < 13) cases.Add(("", false));

        var t = sec.AddTable();
        t.Format.LeftIndent = Unit.FromCentimeter(0.6);
        t.AddColumn(Unit.FromCentimeter(6.6));
        t.AddColumn(Unit.FromCentimeter(6.0));
        t.AddColumn(Unit.FromCentimeter(6.0));
        for (var r = 0; r < 5; r++)
        {
            var ligne = t.AddRow();
            for (var k = 0; k < 3; k++)
            {
                var idx = k * 5 + r;
                if (idx >= cases.Count || (k == 2 && r > 2)) continue;
                var (lib, coche) = cases[idx];
                var p = ligne.Cells[k].AddParagraph(Ins((coche ? Coche : Vide) + " "));
                if (lib.Length > 0) p.AddText(lib);
            }
        }

        Puce(sec).AddText("Le service de garde sera fermé pour les vacances les dates suivantes :");
        var vacances = i.FermeturesDetail.Where(f => !(f.Debut == f.Fin && f.Demi is null && motifsFeries.Contains(f.Motif))).ToList();
        foreach (var v in vacances)
        {
            var texte = v.Debut == v.Fin
                ? $"{v.Motif} : {LongueDate(v.Debut)}{(v.Demi is null ? "" : $" ({v.Demi})")}"
                : $"{v.Motif} : du {LongueDate(v.Debut)} au {LongueDate(v.Fin)} inclusivement";
            LigneValeur(sec, texte);
        }
        if (!string.IsNullOrWhiteSpace(i.NoteVacances)) LigneValeur(sec, i.NoteVacances);
        if (vacances.Count == 0 && string.IsNullOrWhiteSpace(i.NoteVacances)) LigneValeur(sec, null);

        foreach (var l in Lignes(i.ClausesFermeture ?? ""))
        {
            var p = Puce(sec);
            p.Format.Alignment = ParagraphAlignment.Justify;
            p.AddText(l);
        }
    }

    private static void Duree(Section sec, InfosContrat i)
    {
        Titre(sec, "Durée de l'entente");
        var p = Puce(sec);
        p.AddText("L'entente s'étend du ");
        Valeur(p, LongueDate(i.Debut), 18);
        p.AddText(" (date de la première journée de fréquentation de l'enfant) au ");
        Valeur(p, LongueDate(i.Fin), 18);
        p.AddText(".");
    }

    private static void Paiement(Section sec, InfosContrat i)
    {
        Titre(sec, "Modalités de paiement");
        var t = Puce(sec);
        t.AddText("Le tarif payable par le parent est de ");
        Valeur(t, Argent(i.Tarif), 10);
        t.AddText(" par jour de garde.");
        Puce(sec).AddText("Le versement se fera selon les modalités suivantes :");
        var hebdo = !string.IsNullOrWhiteSpace(i.JourPaiement);
        var l1 = Retrait(sec, (hebdo ? Coche : Vide) + " Le ");
        Valeur(l1, i.JourPaiement, 12);
        l1.AddText(" (inscrire le jour) de chaque semaine.");
        Retrait(sec, Vide + " À toutes les 2 semaines");
        var l3 = Retrait(sec, Vide + " À tous les ");
        Valeur(l3, null, 10);
        l3.AddText(" (inscrire la date) du mois.");

        var v = Puce(sec);
        v.AddText("Chaque versement sera de ");
        Valeur(v, Argent(i.Versement), 10);
        v.AddText(" et se fera :");
        var modes = (i.ModesPaiement ?? "").ToLowerInvariant();
        bool comptant = modes.Contains("comptant"), cheque = modes.Contains("chèque") || modes.Contains("cheque"),
            preautorise = modes.Contains("préautoris") || modes.Contains("preautoris");
        Retrait(sec, (comptant ? Coche : Vide) + " En argent comptant");
        Retrait(sec, (cheque ? Coche : Vide) + " Par chèque");
        Retrait(sec, (preautorise ? Coche : Vide) + " Par paiement préautorisé");
        if (!string.IsNullOrWhiteSpace(i.ModesPaiement))
        {
            var autre = Retrait(sec, "Précision : ");
            Valeur(autre, i.ModesPaiement, 10);
        }
        var tot = Puce(sec);
        tot.AddText("La somme totale déboursée en vertu de cette entente de services de garde est de ");
        Valeur(tot, Argent(i.Total), 12);
        tot.AddText(".");
        var nsf = Puce(sec);
        nsf.AddText("Des frais de ");
        Valeur(nsf, string.IsNullOrWhiteSpace(i.FraisNsf) ? null : i.FraisNsf.Trim().TrimEnd('$', ' ') + " $", 10);
        nsf.AddText(" seront facturés pour les chèques sans provision.");
    }

    private static void Signatures(Section sec, InfosContrat i, SignaturesContrat s)
    {
        Titre(sec, "Signatures");
        var t = sec.AddTable();
        t.AddColumn(Unit.FromCentimeter(5));
        t.AddColumn(Unit.FromCentimeter(5));
        t.AddColumn(Unit.FromCentimeter(8.6));
        void Ligne(DateOnly? date, string? lieu, byte[]? image, string role)
        {
            var haut = t.AddRow();
            haut.Height = Unit.FromCentimeter(1.5);
            haut.VerticalAlignment = VerticalAlignment.Bottom;
            haut.Borders.Bottom.Width = 0.75;
            ValeurCellule(haut.Cells[0], LongueDate(date));
            ValeurCellule(haut.Cells[1], lieu);
            if (image is not null)
            {
                var img = haut.Cells[2].AddParagraph().AddImage("base64:" + Convert.ToBase64String(image));
                img.Height = Unit.FromCentimeter(1.2);
                img.LockAspectRatio = true;
            }
            var bas = t.AddRow();
            bas.Cells[0].AddParagraph("Date");
            bas.Cells[1].AddParagraph("Lieu");
            bas.Cells[2].AddParagraph(role);
            bas.KeepWith = 0;
            haut.KeepWith = 1;
        }
        Ligne(s.DateParent, s.SignatureParent is null ? null : i.Ville, s.SignatureParent, "Signature du parent");
        Ligne(null, null, null, "Signature du parent");
        Ligne(s.DateRsge, s.SignatureRsge is null ? null : i.Ville, s.SignatureRsge, "Signature du prestataire");
        if (!string.IsNullOrWhiteSpace(s.NomParent))
        {
            var n = sec.AddParagraph("Nom du parent signataire : ");
            n.Format.SpaceBefore = Unit.FromPoint(6);
            Valeur(n, s.NomParent, 20);
        }
    }

    /// <summary>Formule de resiliation annexee (LPC) / Annexed termination form.</summary>
    private static void Formule(Section sec, InfosContrat i)
    {
        var t = sec.AddParagraph("FORMULE DE RÉSILIATION");
        t.Format.Alignment = ParagraphAlignment.Center;
        t.Format.Font.Size = 13;
        t.Format.SpaceAfter = Unit.FromPoint(4);
        var a = sec.AddParagraph("(Annexe à l'entente de services de garde éducatifs — Loi sur la protection du consommateur, art. 190 à 196)");
        a.Format.Alignment = ParagraphAlignment.Center;
        a.Format.Font.Size = 8.5;
        a.Format.SpaceAfter = Unit.FromPoint(16);
        var p = Justifie(sec, "");
        p.AddText("À ");
        Valeur(p, i.Rsge, 16);
        p.AddText(", prestataire de services de garde (");
        Valeur(p, i.AdresseGarderie, 20);
        p.AddText("). Je résilie l'entente de services de garde éducatifs pour l'enfant ");
        Valeur(p, i.EnfantNom, 16);
        p.AddText(", conclue pour la période du ");
        Valeur(p, LongueDate(i.Debut), 12);
        p.AddText(" au ");
        Valeur(p, LongueDate(i.Fin), 12);
        p.AddText(", à compter du ");
        Valeur(p, null, 14);
        p.AddText(".");
        for (var k = 0; k < 2; k++)
        {
            var s = sec.AddParagraph(Ins("Date : ____________________     Nom du parent : ______________________________"));
            s.Format.SpaceBefore = Unit.FromPoint(22);
            var sg = sec.AddParagraph("Signature du parent : ________________________________________");
            sg.Format.SpaceBefore = Unit.FromPoint(14);
        }
    }

    // --- Outils de mise en page / Layout helpers ---

    private static Document Nouveau()
    {
        var doc = new Document();
        doc.Info.Title = "Entente de services de garde éducatifs";
        var normal = doc.Styles[StyleNames.Normal]!;
        normal.Font.Name = Police;
        normal.Font.Size = 9.5;
        normal.ParagraphFormat.SpaceAfter = Unit.FromPoint(2);
        normal.ParagraphFormat.LineSpacingRule = LineSpacingRule.Multiple;
        normal.ParagraphFormat.LineSpacing = 1.15;
        var sec = doc.AddSection();
        sec.PageSetup = doc.DefaultPageSetup.Clone();
        sec.PageSetup.PageFormat = PageFormat.Letter;
        sec.PageSetup.LeftMargin = Unit.FromCentimeter(1.6);
        sec.PageSetup.RightMargin = Unit.FromCentimeter(1.4);
        sec.PageSetup.TopMargin = Unit.FromCentimeter(1.3);
        sec.PageSetup.BottomMargin = Unit.FromCentimeter(1.5);
        var pied = sec.Footers.Primary.AddParagraph();
        pied.Format.Alignment = ParagraphAlignment.Right;
        pied.Format.Font.Size = 7.5;
        pied.AddText("Page ");
        pied.AddPageField();
        pied.AddText(" de ");
        pied.AddNumPagesField();
        return doc;
    }

    private static void Titre(Section sec, string texte)
    {
        var p = sec.AddParagraph();
        p.Format.SpaceBefore = Unit.FromPoint(10);
        p.Format.SpaceAfter = Unit.FromPoint(4);
        p.Format.KeepWithNext = true;
        p.AddFormattedText(texte, new Font(Police, 10) { Underline = Underline.Single });
    }

    private static void SousTitre(Cell c, string texte)
    {
        var p = c.AddParagraph();
        p.Format.SpaceAfter = Unit.FromPoint(6);
        p.AddFormattedText(texte, new Font(Police, 10) { Underline = Underline.Single });
    }

    /// <summary>Paragraphe a puce (•) / Bulleted paragraph.</summary>
    private static Paragraph Puce(Section sec)
    {
        var p = sec.AddParagraph();
        p.Format.LeftIndent = Unit.FromCentimeter(1.0);
        p.Format.FirstLineIndent = Unit.FromCentimeter(-0.5);
        p.Format.TabStops.AddTabStop(Unit.FromCentimeter(1.0));
        p.AddText("•\t");
        return p;
    }

    private static Paragraph Retrait(Section sec, string texte)
    {
        var p = sec.AddParagraph(texte);
        p.Format.LeftIndent = Unit.FromCentimeter(1.4);
        return p;
    }

    private static Paragraph Justifie(Section sec, string texte)
    {
        var p = sec.AddParagraph(texte);
        p.Format.Alignment = ParagraphAlignment.Justify;
        return p;
    }

    private static void Champ(Cell c, string libelle, string? valeur)
    {
        var p = c.AddParagraph(libelle);
        p.Format.SpaceAfter = Unit.FromPoint(4);
        Valeur(p, valeur, 22);
    }

    /// <summary>Valeur remplie (bleu, soulignee) ou ligne a remplir / Filled value (blue, underlined) or a blank.</summary>
    private static void Valeur(Paragraph p, string? valeur, int blancs)
    {
        if (string.IsNullOrWhiteSpace(valeur)) { p.AddText(new string('_', blancs)); return; }
        var f = p.AddFormattedText(valeur.Trim(), TextFormat.Underline);
        f.Color = Encre;
    }

    private static void ValeurCellule(Cell c, string? valeur)
    {
        if (string.IsNullOrWhiteSpace(valeur)) return;
        c.AddParagraph().AddFormattedText(valeur).Color = Encre;
    }

    private static void LigneValeur(Section sec, string? texte)
    {
        var p = sec.AddParagraph();
        p.Format.LeftIndent = Unit.FromCentimeter(1.0);
        if (string.IsNullOrWhiteSpace(texte)) p.AddText(new string('_', 80));
        else p.AddFormattedText(texte).Color = Encre;
    }

    /// <summary>Tableau de la semaine : ligne « de » et ligne « à » / Weekly table: "from" and "to" rows.</summary>
    private static void Semaine(Section sec, Func<string, (string? De, string? A)> heures)
    {
        string[] jours = ["Lundi", "Mardi", "Mercredi", "Jeudi", "Vendredi", "Samedi", "Dimanche"];
        var t = sec.AddTable();
        t.Borders.Width = 0.75;
        t.Format.Alignment = ParagraphAlignment.Center;
        t.AddColumn(Unit.FromCentimeter(1.4));
        foreach (var _ in jours) t.AddColumn(Unit.FromCentimeter(2.45));
        var entete = t.AddRow();
        entete.HeadingFormat = true;
        for (var k = 0; k < 7; k++) entete.Cells[k + 1].AddParagraph(jours[k]);
        var de = t.AddRow();
        var a = t.AddRow();
        de.Cells[0].AddParagraph("de");
        a.Cells[0].AddParagraph("à");
        foreach (var r in new[] { entete, de, a })
        {
            r.Height = Unit.FromCentimeter(0.75);
            r.VerticalAlignment = VerticalAlignment.Center;
        }
        for (var k = 0; k < 7; k++)
        {
            var (h1, h2) = heures(jours[k]);
            ValeurCellule(de.Cells[k + 1], h1);
            ValeurCellule(a.Cells[k + 1], h2);
        }
        entete.KeepWith = 2;
        sec.AddParagraph().Format.SpaceAfter = Unit.FromPoint(2);
    }

    private static IEnumerable<string> Lignes(string texte) =>
        texte.Replace("\r\n", "\n").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0);

    private static string? Argent(decimal? m) => m is { } v ? Affichage.Argent(v) : null;

    private static string? LongueDate(DateOnly? d) => d is null ? null : Affichage.Date(d);

    private static string Ins(string s) => s.Replace(' ', ' ');

    /// <summary>« Prenom Nom » -> (prenom, nom) au premier espace / Splits at the first space.</summary>
    public static (string? Prenom, string? Nom) Couper(string? nom)
    {
        if (string.IsNullOrWhiteSpace(nom)) return (null, null);
        var t = nom.Trim();
        var k = t.IndexOf(' ');
        return k < 0 ? (t, null) : (t[..k], t[(k + 1)..]);
    }

    /// <summary>Separe le code postal de l'adresse / Splits the postal code off the address.</summary>
    public static (string? Adresse, string? CodePostal) Adresse(string? adresse)
    {
        if (string.IsNullOrWhiteSpace(adresse)) return (null, null);
        var m = CodePostal().Match(adresse);
        if (!m.Success) return (adresse.Trim(), null);
        var reste = adresse[..m.Index].Trim().TrimEnd(',', ' ');
        return (reste, $"{m.Groups[1].Value} {m.Groups[2].Value}".ToUpperInvariant());
    }

    [GeneratedRegex(@"([A-Za-z]\d[A-Za-z])\s?(\d[A-Za-z]\d)\s*$")]
    private static partial Regex CodePostal();

    /// <summary>Montant et tranche de minutes (reglages, sinon lus dans le texte de la regie) / Late fee parts.</summary>
    public static (string? Montant, string? Minutes) Retard(InfosContrat i)
    {
        string? montant = i.RetardMontant, minutes = i.RetardMinutes;
        if ((montant is null || minutes is null) && i.FraisRetard is { } texte)
        {
            var m = Regex.Match(texte, @"(\d+(?:[.,]\d{1,2})?)\s*\$");
            var n = Regex.Match(texte, @"(\d+)\s*(?:\p{L}+\s+)?min");
            montant ??= m.Success ? m.Groups[1].Value : null;
            minutes ??= n.Success ? n.Groups[1].Value : null;
        }
        return (montant?.Trim().TrimEnd('$', ' '), minutes?.Trim());
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
