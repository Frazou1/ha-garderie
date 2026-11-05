using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.IO;

namespace Garderie.Web.Services;

/// <summary>
/// Remplit les champs d'un formulaire PDF officiel protege (Revenu Quebec : chiffrement AES-128, ouverture sans mot de
/// passe, droits « remplir les champs » et « imprimer » seulement). Comme Acrobat Reader qui enregistre un formulaire,
/// on n'ajoute qu'une mise a jour incrementale a la fin du fichier : les champs remplis (valeur /V et apparence /AP),
/// chiffres avec la cle du document. Le reste du document et sa protection sont intacts.
/// Fills an official protected PDF form (AES-128, empty user password, fill-in rights only) with an incremental
/// update holding only the filled fields (value + appearance), encrypted with the document key.
/// </summary>
public static partial class FormulaireProtege
{
    /// <summary>Valeur d'un champ et texte affiche (masque du formulaire), sinon la valeur / Field value and shown text.</summary>
    /// <param name="Cases">Nombre de cases d'un champ en peigne, si le formulaire se trompe (ex. NAS a 9 cases
    /// declare a 8) / Comb cell count when the form declares it wrong.</param>
    public sealed record Champ(string Valeur, string? Affichage = null, int? Cases = null)
    {
        public string Texte => Affichage ?? Valeur;
    }

    /// <summary>Valeur actuelle des champs texte (ex. numero sequentiel par defaut) / Current text field values.</summary>
    public static Dictionary<string, string> LireValeurs(byte[] pdf)
    {
        var doc = PdfReader.Open(new MemoryStream(pdf), PdfDocumentOpenMode.Import);
        var res = new Dictionary<string, string>();
        foreach (var (nom, champ, _) in Champs(doc))
            if (Deref(champ.Elements["/V"]) is PdfString s) res[nom] = s.Value;
        return res;
    }

    /// <summary>
    /// Verifie qu'on sait remplir ce PDF (protection prise en charge) ; retourne les champs requis absents.
    /// Checks the PDF can be filled (supported protection); returns the missing required fields.
    /// </summary>
    public static List<string> Verifier(byte[] pdf, IEnumerable<string> requis)
    {
        Lire(pdf);
        var doc = PdfReader.Open(new MemoryStream(pdf), PdfDocumentOpenMode.Import);
        var noms = Champs(doc).Select(c => c.Nom).ToHashSet();
        return requis.Where(r => !noms.Contains(r)).ToList();
    }

    public static byte[] Remplir(byte[] pdf, IReadOnlyDictionary<string, Champ> valeurs)
    {
        var brut = Lire(pdf);
        var doc = PdfReader.Open(new MemoryStream(pdf), PdfDocumentOpenMode.Import);
        var acro = Deref(doc.Internals.Catalog.Elements["/AcroForm"]) as PdfDictionary
                   ?? throw new InvalidDataException("Le PDF n'a pas de formulaire.");
        var polices = Deref(Deref(acro.Elements["/DR"]) is PdfDictionary dr ? dr.Elements["/Font"] : null) as PdfDictionary;
        var champs = Champs(doc).ToDictionary(c => c.Nom);
        var manquants = valeurs.Keys.Where(k => !champs.ContainsKey(k)).ToList();
        if (manquants.Count > 0) throw new InvalidDataException("Champs absents du formulaire : " + string.Join(", ", manquants));

        var sortie = new MemoryStream();
        sortie.Write(pdf);
        if (pdf[^1] != (byte)'\n') sortie.WriteByte((byte)'\n');
        var positions = new SortedDictionary<int, long>();
        var prochain = brut.Taille;

        void Ecrire(int numero, Func<int, byte[]> contenu)
        {
            positions[numero] = sortie.Position;
            sortie.Write(Encoding.ASCII.GetBytes($"{numero} 0 obj\n"));
            sortie.Write(contenu(numero));
            sortie.Write(Encoding.ASCII.GetBytes("\nendobj\n"));
        }

        foreach (var (nom, valeur) in valeurs)
        {
            var (_, champ, widgets) = champs[nom];
            foreach (var w in widgets)
            {
                var ap = prochain++;
                var flux = Apparence(w, champ, valeur.Texte, polices, valeur.Cases);
                Ecrire(ap, n => Flux(brut.Cle, n, flux.Dict, flux.Contenu));
                var remplacements = new Dictionary<string, string> { ["/AP"] = $"<< /N {ap} 0 R >>" };
                if (ReferenceEquals(w, champ)) remplacements["/V"] = Chaine(brut.Cle, Numero(champ), valeur.Valeur);
                Ecrire(Numero(w), n => Encoding.ASCII.GetBytes(Dictionnaire(brut.Cle, n, w, remplacements)));
            }
            if (!widgets.Any(w => ReferenceEquals(w, champ)))
                Ecrire(Numero(champ), n => Encoding.ASCII.GetBytes(Dictionnaire(brut.Cle, n, champ,
                    new() { ["/V"] = Chaine(brut.Cle, n, valeur.Valeur) })));
        }

        // Flux de references (meme format que l'original, non chiffre), chaine a la section precedente
        // Cross-reference stream (same format as the original, not encrypted), chained with /Prev
        var xref = prochain++;
        positions[xref] = sortie.Position;
        var lignes = new MemoryStream();
        foreach (var (_, pos) in positions)
        {
            lignes.WriteByte(1);
            lignes.Write([(byte)(pos >> 24), (byte)(pos >> 16), (byte)(pos >> 8), (byte)pos, 0, 0]);
        }
        var index = string.Join(" ", positions.Keys.Select(k => $"{k} 1"));
        var dict = $"<< /Type /XRef /Size {prochain} /Index [{index}] /W [1 4 2] /Prev {brut.StartXref} /Root {brut.Racine}" +
                   $" /Encrypt {brut.Chiffrement}{(brut.Info is null ? "" : $" /Info {brut.Info}")} /ID [<{brut.Id0}> <{brut.Id1}>] /Length {lignes.Length} >>";
        sortie.Write(Encoding.ASCII.GetBytes($"{xref} 0 obj\n{dict}\nstream\n"));
        sortie.Write(lignes.ToArray());
        sortie.Write(Encoding.ASCII.GetBytes($"\nendstream\nendobj\nstartxref\n{positions[xref]}\n%%EOF\n"));
        return sortie.ToArray();
    }

    // --- Champs du formulaire / Form fields ---

    private static PdfItem? Deref(PdfItem? i) => i is PdfReference r ? r.Value : i;

    private static int Numero(PdfDictionary d) =>
        d.Reference?.ObjectNumber ?? throw new InvalidDataException("Champ sans numéro d'objet.");

    /// <summary>Champs terminaux : nom complet, dictionnaire du champ, widgets / Terminal fields with their widgets.</summary>
    private static IEnumerable<(string Nom, PdfDictionary Champ, List<PdfDictionary> Widgets)> Champs(PdfDocument doc)
    {
        var acro = Deref(doc.Internals.Catalog.Elements["/AcroForm"]) as PdfDictionary;
        if (Deref(acro?.Elements["/Fields"]) is not PdfArray racines) yield break;
        var pile = new Stack<(PdfDictionary D, string Prefixe)>();
        foreach (var r in racines.Elements.Reverse()) if (Deref(r) is PdfDictionary d) pile.Push((d, ""));
        while (pile.Count > 0)
        {
            var (d, prefixe) = pile.Pop();
            var t = (Deref(d.Elements["/T"]) as PdfString)?.Value;
            var nom = t is null ? prefixe : prefixe.Length == 0 ? t : prefixe + "." + t;
            var enfants = (Deref(d.Elements["/Kids"]) as PdfArray)?.Elements.Select(Deref).OfType<PdfDictionary>().ToList() ?? [];
            var sousChamps = enfants.Where(k => k.Elements.ContainsKey("/T")).ToList();
            if (sousChamps.Count > 0)
            {
                foreach (var k in sousChamps.AsEnumerable().Reverse()) pile.Push((k, nom));
                continue;
            }
            var widgets = enfants.Count > 0 ? enfants : [d];
            yield return (nom, d, widgets);
        }
    }

    private static PdfItem? Herite(PdfDictionary d, string cle)
    {
        for (var o = d; o is not null; o = Deref(o.Elements["/Parent"]) as PdfDictionary)
            if (o.Elements.ContainsKey(cle)) return Deref(o.Elements[cle]);
        return null;
    }

    private static double Nombre(PdfItem? i) => i switch
    {
        PdfInteger n => n.Value, PdfReal r => r.Value, PdfLongInteger l => l.Value,
        PdfIntegerObject n => n.Value, PdfRealObject r => r.Value, _ => 0,
    };

    // --- Apparence / Appearance ---

    private sealed record FluxApparence(string Dict, byte[] Contenu);

    [GeneratedRegex(@"/([^\s/\[\]()<>{}%]+)\s+([\d.]+)\s+Tf")]
    private static partial Regex RegexTf();

    private static FluxApparence Apparence(PdfDictionary widget, PdfDictionary champ, string texte, PdfDictionary? polices, int? cases)
    {
        var rect = (Deref(widget.Elements["/Rect"]) as PdfArray)!.Elements.Select(e => Nombre(Deref(e))).ToArray();
        double l = Math.Abs(rect[2] - rect[0]), h = Math.Abs(rect[3] - rect[1]);
        var da = (Herite(widget, "/DA") as PdfString)?.Value ?? "/Helv 0 Tf 0 g";
        var m = RegexTf().Match(da);
        var police = m.Success ? m.Groups[1].Value : "Helv";
        var taille = m.Success ? double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) : 0;
        var couleur = m.Success ? da[(m.Index + m.Length)..].Trim() : "0 g";
        var refPolice = polices?.Elements["/" + police] is PdfReference pr ? $"{pr.ObjectNumber} 0 R" : null;
        var largeurs = Largeurs(Deref(polices?.Elements["/" + police]) as PdfDictionary);
        var q = (int)Nombre(Herite(widget, "/Q"));
        var ff = (int)Nombre(Herite(champ, "/Ff"));
        var maxLen = cases ?? (int)Nombre(Herite(champ, "/MaxLen"));
        var multi = (ff & (1 << 12)) != 0;
        var peigne = (ff & (1 << 24)) != 0 && maxLen > 0;

        double Largeur(string s, double t) => s.Sum(c => largeurs(c)) * t / 1000;
        var lignes = multi ? texte.Replace("\r\n", "\n").Split('\n') : [texte.Replace('\n', ' ')];
        if (taille <= 0)
        {
            // Taille automatique : 9 pt, reduite pour tenir / Auto size: 9 pt, reduced to fit
            taille = multi ? 9 : Math.Min(9, (h - 3) / 1.15);
            while (taille > 5 && (lignes.Any(x => Largeur(x, taille) > l - 4) || (multi && lignes.Length * taille * 1.15 > h - 3))) taille -= 0.5;
        }
        else if (!peigne && !multi)
            while (taille > 5 && Largeur(lignes[0], taille) > l - 4) taille -= 0.5;

        var c = new StringBuilder();
        c.Append("/Tx BMC\nq\n1 1 ").Append(F(l - 2)).Append(' ').Append(F(h - 2)).Append(" re W n\nBT\n");
        if (refPolice is not null) c.Append('/').Append(police).Append(' ').Append(F(taille)).Append(" Tf\n");
        c.Append(couleur).Append('\n');
        if (peigne)
        {
            var cellule = l / maxLen;
            var y = (h - taille * 0.72) / 2;
            for (var i = 0; i < Math.Min(texte.Length, maxLen); i++)
            {
                var x = i * cellule + (cellule - Largeur(texte[i].ToString(), taille)) / 2;
                c.Append("1 0 0 1 ").Append(F(x)).Append(' ').Append(F(y)).Append(" Tm (").Append(Echapper(texte[i].ToString())).Append(") Tj\n");
            }
        }
        else
        {
            for (var i = 0; i < lignes.Length; i++)
            {
                var largeur = Largeur(lignes[i], taille);
                var x = q switch { 1 => (l - largeur) / 2, 2 => l - 2 - largeur, _ => 2 };
                var y = multi ? h - 2 - taille * 0.85 - i * taille * 1.15 : (h - taille * 0.72) / 2;
                c.Append("1 0 0 1 ").Append(F(x)).Append(' ').Append(F(y)).Append(" Tm (").Append(Echapper(lignes[i])).Append(") Tj\n");
            }
        }
        c.Append("ET\nQ\nEMC");
        var ressources = refPolice is null ? "" : $" /Resources << /Font << /{police} {refPolice} >> >>";
        return new($"/Type /XObject /Subtype /Form /BBox [0 0 {F(l)} {F(h)}]{ressources}", Ansi(c.ToString()));
    }

    /// <summary>Largeurs de la police du formulaire (/Widths), sinon Helvetica / Font widths, else Helvetica.</summary>
    private static Func<char, double> Largeurs(PdfDictionary? police)
    {
        if (police is not null && Deref(police.Elements["/Widths"]) is PdfArray w && Nombre(Deref(police.Elements["/FirstChar"])) is var premier)
        {
            var tableau = w.Elements.Select(e => Nombre(Deref(e))).ToArray();
            return ch =>
            {
                var k = OctetAnsi(ch) - (int)premier;
                return k >= 0 && k < tableau.Length && tableau[k] > 0 ? tableau[k] : 556;
            };
        }
        return ch => OctetAnsi(ch) is var b and >= 32 ? Helvetica[b - 32] is var v && v > 0 ? v : 556 : 0;
    }

    private static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    private static string Echapper(string s)
    {
        var b = new StringBuilder();
        foreach (var o in Ansi(s))
        {
            if (o is (byte)'(' or (byte)')' or (byte)'\\') b.Append('\\').Append((char)o);
            else if (o < 32 || o > 126) b.Append('\\').Append(Convert.ToString(o, 8).PadLeft(3, '0'));
            else b.Append((char)o);
        }
        return b.ToString();
    }

    // Windows-1252 sans dependance aux pages de codes / Windows-1252 without code page provider
    private static readonly Dictionary<char, byte> Cp1252 = new()
    {
        ['€'] = 0x80, ['‚'] = 0x82, ['ƒ'] = 0x83, ['„'] = 0x84, ['…'] = 0x85, ['†'] = 0x86, ['‡'] = 0x87, ['ˆ'] = 0x88,
        ['‰'] = 0x89, ['Š'] = 0x8A, ['‹'] = 0x8B, ['Œ'] = 0x8C, ['Ž'] = 0x8E, ['‘'] = 0x91, ['’'] = 0x92, ['“'] = 0x93,
        ['”'] = 0x94, ['•'] = 0x95, ['–'] = 0x96, ['—'] = 0x97, ['˜'] = 0x98, ['™'] = 0x99, ['š'] = 0x9A, ['›'] = 0x9B,
        ['œ'] = 0x9C, ['ž'] = 0x9E, ['Ÿ'] = 0x9F,
    };

    private static int OctetAnsi(char c) => Cp1252.TryGetValue(c, out var b) ? b : c is < (char)0x80 or (>= (char)0xA0 and <= (char)0xFF) ? c : '?';

    private static byte[] Ansi(string s) => s.Select(c => (byte)OctetAnsi(c)).ToArray();

    // Helvetica (memes mesures qu'Arial), caracteres 32 a 255 en Windows-1252 / Helvetica widths, chars 32-255
    private static readonly int[] Helvetica =
    [
        278,278,355,556,556,889,667,191,333,333,389,584,278,333,278,278,556,556,556,556,556,556,556,556,556,556,278,278,584,584,584,556,
        1015,667,667,722,722,667,611,778,722,278,500,667,556,833,722,778,667,778,722,667,611,722,667,944,667,667,611,278,278,278,469,556,
        333,556,556,500,556,556,278,556,556,222,222,500,222,833,556,556,556,556,333,500,278,556,500,722,500,500,500,334,260,334,584,0,
        556,0,222,556,333,1000,556,556,333,1000,667,333,1000,0,611,0,0,222,222,333,333,350,556,1000,333,1000,500,333,944,0,500,667,
        278,333,556,556,556,556,260,556,333,737,370,556,584,333,737,552,400,549,333,333,333,576,537,333,333,333,365,556,834,834,834,611,
        667,667,667,667,667,667,1000,722,667,667,667,667,278,278,278,278,722,722,778,778,778,778,778,584,778,722,722,722,722,667,667,611,
        556,556,556,556,556,556,889,500,556,556,556,556,278,278,278,278,556,556,556,556,556,556,556,549,611,556,556,556,556,500,556,500,
    ];

    // --- Ecriture des objets / Object writing ---

    private static string Dictionnaire(byte[] cle, int numero, PdfDictionary d, Dictionary<string, string> remplacements)
    {
        var b = new StringBuilder("<<");
        foreach (var k in d.Elements.Keys)
        {
            if (remplacements.ContainsKey(k)) continue;
            b.Append(' ').Append(Nom(k)).Append(' ').Append(Valeur(cle, numero, d.Elements[k]));
        }
        foreach (var (k, v) in remplacements) b.Append(' ').Append(k).Append(' ').Append(v);
        return b.Append(" >>").ToString();
    }

    private static string Valeur(byte[] cle, int numero, PdfItem? i) => i switch
    {
        null or PdfNull => "null",
        PdfReference r => $"{r.ObjectNumber} {r.GenerationNumber} R",
        PdfDictionary d when d.Reference is { } r => $"{r.ObjectNumber} {r.GenerationNumber} R",
        PdfArray a when a.Reference is { } r => $"{r.ObjectNumber} {r.GenerationNumber} R",
        PdfName n => Nom(n.Value),
        PdfBoolean b => b.Value ? "true" : "false",
        PdfInteger n => n.Value.ToString(CultureInfo.InvariantCulture),
        PdfLongInteger n => n.Value.ToString(CultureInfo.InvariantCulture),
        PdfReal r => F(r.Value),
        PdfString s => Chaine(cle, numero, s.Value),
        PdfArray a => "[" + string.Join(" ", a.Elements.Select(e => Valeur(cle, numero, e))) + "]",
        PdfDictionary d => Dictionnaire(cle, numero, d, new()),
        _ => throw new InvalidDataException($"Élément PDF non pris en charge : {i.GetType().Name}"),
    };

    private static string Nom(string nom)
    {
        var b = new StringBuilder("/");
        foreach (var c in nom.TrimStart('/'))
            b.Append(c is > (char)0x20 and < (char)0x7F and not ('#' or '/' or '(' or ')' or '<' or '>' or '[' or ']' or '{' or '}' or '%')
                ? c.ToString() : $"#{(int)c:X2}");
        return b.ToString();
    }

    /// <summary>Chaine chiffree en hexadecimal (PDFDocEncoding ou UTF-16BE) / Encrypted hex string.</summary>
    private static string Chaine(byte[] cle, int numero, string texte)
    {
        var simple = texte.All(c => c <= 0xFF && c is < (char)0x80 or >= (char)0xA0);
        var octets = simple ? texte.Select(c => (byte)c).ToArray() : [0xFE, 0xFF, .. Encoding.BigEndianUnicode.GetBytes(texte)];
        return "<" + Convert.ToHexString(Chiffrer(cle, numero, octets)) + ">";
    }

    private static byte[] Flux(byte[] cle, int numero, string dict, byte[] contenu)
    {
        var chiffre = Chiffrer(cle, numero, contenu);
        var tete = Encoding.ASCII.GetBytes($"<< {dict} /Length {chiffre.Length} >>\nstream\n");
        return [.. tete, .. chiffre, .. "\nendstream"u8];
    }

    // --- Chiffrement AES-128 (gestionnaire standard, R4, AESV2) / AES-128 encryption (standard handler, R4) ---

    private static byte[] Chiffrer(byte[] cleFichier, int numero, byte[] donnees)
    {
        var sel = new byte[cleFichier.Length + 9];
        cleFichier.CopyTo(sel, 0);
        sel[cleFichier.Length] = (byte)numero;
        sel[cleFichier.Length + 1] = (byte)(numero >> 8);
        sel[cleFichier.Length + 2] = (byte)(numero >> 16);
        sel[cleFichier.Length + 3] = 0;   // generation 0
        sel[cleFichier.Length + 4] = 0;
        "sAlT"u8.CopyTo(sel.AsSpan(cleFichier.Length + 5));
        using var aes = Aes.Create();
        aes.Key = MD5.HashData(sel);
        aes.GenerateIV();
        return [.. aes.IV, .. aes.EncryptCbc(donnees, aes.IV, PaddingMode.PKCS7)];
    }

    private static readonly byte[] Bourrage =
    [
        0x28, 0xBF, 0x4E, 0x5E, 0x4E, 0x75, 0x8A, 0x41, 0x64, 0x00, 0x4E, 0x56, 0xFF, 0xFA, 0x01, 0x08,
        0x2E, 0x2E, 0x00, 0xB6, 0xD0, 0x68, 0x3E, 0x80, 0x2F, 0x0C, 0xA9, 0xFE, 0x64, 0x53, 0x69, 0x7A,
    ];

    private sealed record Structure(byte[] Cle, long StartXref, int Taille, string Racine, string Chiffrement, string? Info, string Id0, string Id1);

    [GeneratedRegex(@"startxref\s+(\d+)\s+%%EOF\s*$")]
    private static partial Regex RegexStartXref();

    /// <summary>Derniere section de references, dictionnaire de chiffrement et cle (mot de passe vide) / Trailer and key.</summary>
    private static Structure Lire(byte[] pdf)
    {
        var fin = Encoding.Latin1.GetString(pdf, Math.Max(0, pdf.Length - 1024), Math.Min(1024, pdf.Length));
        var sx = RegexStartXref().Match(fin);
        if (!sx.Success) throw new InvalidDataException("PDF sans startxref.");
        var startXref = long.Parse(sx.Groups[1].Value, CultureInfo.InvariantCulture);
        var section = Encoding.Latin1.GetString(pdf, (int)startXref, Math.Min(4096, pdf.Length - (int)startXref));
        var debut = section.StartsWith("xref", StringComparison.Ordinal) ? section.IndexOf("trailer", StringComparison.Ordinal) : 0;
        var trailer = DictionnaireBrut(section, section.IndexOf("<<", debut, StringComparison.Ordinal));
        string Ref(string cle) => Regex.Match(trailer, $@"/{cle}\s+(\d+\s+\d+\s+R)") is { Success: true } m ? Regex.Replace(m.Groups[1].Value, @"\s+", " ") : "";
        var taille = int.Parse(Regex.Match(trailer, @"/Size\s+(\d+)").Groups[1].Value, CultureInfo.InvariantCulture);
        var ids = Regex.Matches(Regex.Match(trailer, @"/ID\s*\[(.*?)\]", RegexOptions.Singleline).Groups[1].Value, @"<([0-9A-Fa-f]*)>|\(");
        if (ids.Count < 2 || ids.Any(i => !i.Groups[1].Success)) throw new InvalidDataException("Identifiant du PDF illisible.");
        var racine = Ref("Root");
        var chiffrement = Ref("Encrypt");
        if (racine.Length == 0 || chiffrement.Length == 0) throw new InvalidDataException("Ce PDF n'est pas un formulaire protégé.");

        // Dictionnaire /Encrypt : jamais dans un flux d'objets / Encryption dictionary: never in an object stream
        var num = chiffrement.Split(' ')[0];
        var texte = Encoding.Latin1.GetString(pdf);
        var obj = Regex.Match(texte, $@"(?<![0-9]){num}\s+0\s+obj");
        if (!obj.Success) throw new InvalidDataException("Dictionnaire de chiffrement introuvable.");
        var enc = DictionnaireBrut(texte, texte.IndexOf("<<", obj.Index, StringComparison.Ordinal));
        if (!Regex.IsMatch(enc, @"/V\s+4\b") || !Regex.IsMatch(enc, @"/R\s+4\b") || !enc.Contains("/AESV2"))
            throw new InvalidDataException("Chiffrement non pris en charge (AES-128, révision 4 attendue).");
        var o = ChaineBrute(enc, "/O");
        var u = ChaineBrute(enc, "/U");
        var p = int.Parse(Regex.Match(enc, @"/P\s+(-?\d+)").Groups[1].Value, CultureInfo.InvariantCulture);
        var metadonnees = !Regex.IsMatch(enc, @"/EncryptMetadata\s+false");
        var id0 = Convert.FromHexString(ids[0].Groups[1].Value);

        // Algorithme 2 (cle du fichier, mot de passe vide) / Algorithm 2 (file key, empty password)
        var md5 = new List<byte>(Bourrage);
        md5.AddRange(o.Take(32));
        md5.AddRange(BitConverter.GetBytes(p));
        md5.AddRange(id0);
        if (!metadonnees) md5.AddRange([0xFF, 0xFF, 0xFF, 0xFF]);
        var cle = MD5.HashData(md5.ToArray());
        for (var i = 0; i < 50; i++) cle = MD5.HashData(cle.AsSpan(0, 16));
        cle = cle[..16];

        // Algorithme 5 : verifie la cle avec /U / Algorithm 5: check the key against /U
        var verif = Rc4(cle, MD5.HashData([.. Bourrage, .. id0]));
        for (byte i = 1; i <= 19; i++) verif = Rc4(cle.Select(x => (byte)(x ^ i)).ToArray(), verif);
        if (!verif.AsSpan(0, 16).SequenceEqual(u.AsSpan(0, 16)))
            throw new InvalidDataException("Ce PDF demande un mot de passe pour l'ouvrir.");

        return new(cle, startXref, taille, racine, chiffrement, Ref("Info") is { Length: > 0 } info ? info : null,
            ids[0].Groups[1].Value, ids[1].Groups[1].Value);
    }

    private static string DictionnaireBrut(string s, int debut)
    {
        var profondeur = 0;
        for (var i = debut; i < s.Length - 1; i++)
        {
            if (s[i] == '(')
            {
                // Chaine litterale : ignorer son contenu / Literal string: skip its content
                var n = 1;
                for (i++; i < s.Length && n > 0; i++)
                    if (s[i] == '\\') i++;
                    else if (s[i] == '(') n++;
                    else if (s[i] == ')') n--;
                i--;
            }
            else if (s[i] == '<' && s[i + 1] == '<') { profondeur++; i++; }
            else if (s[i] == '>' && s[i + 1] == '>') { profondeur--; i++; if (profondeur == 0) return s[debut..(i + 1)]; }
        }
        throw new InvalidDataException("Dictionnaire PDF incomplet.");
    }

    private static byte[] ChaineBrute(string dict, string cle)
    {
        var i = Regex.Match(dict, Regex.Escape(cle) + @"\s*[(<]");
        if (!i.Success) throw new InvalidDataException($"Entrée {cle} absente.");
        var k = i.Index + i.Length - 1;
        if (dict[k] == '<')
            return Convert.FromHexString(Regex.Replace(dict[(k + 1)..dict.IndexOf('>', k)], @"\s", ""));
        var octets = new List<byte>();
        for (int j = k + 1, n = 1; j < dict.Length; j++)
        {
            var c = dict[j];
            if (c == '\\')
            {
                var e = dict[++j];
                if (e is >= '0' and <= '7')
                {
                    var oct = e.ToString();
                    while (oct.Length < 3 && dict[j + 1] is >= '0' and <= '7') oct += dict[++j];
                    octets.Add((byte)Convert.ToInt32(oct, 8));
                }
                else if (e is '\r' or '\n') { if (e == '\r' && dict[j + 1] == '\n') j++; }
                else octets.Add((byte)(e switch { 'n' => '\n', 'r' => '\r', 't' => '\t', 'b' => '\b', 'f' => '\f', _ => e }));
                continue;
            }
            if (c == '(') n++;
            else if (c == ')' && --n == 0) break;
            octets.Add((byte)c);
        }
        return octets.ToArray();
    }

    private static byte[] Rc4(byte[] cle, byte[] donnees)
    {
        var s = Enumerable.Range(0, 256).Select(x => (byte)x).ToArray();
        for (int i = 0, j = 0; i < 256; i++)
        {
            j = (j + s[i] + cle[i % cle.Length]) & 255;
            (s[i], s[j]) = (s[j], s[i]);
        }
        var res = new byte[donnees.Length];
        for (int k = 0, i = 0, j = 0; k < donnees.Length; k++)
        {
            i = (i + 1) & 255;
            j = (j + s[i]) & 255;
            (s[i], s[j]) = (s[j], s[i]);
            res[k] = (byte)(donnees[k] ^ s[(s[i] + s[j]) & 255]);
        }
        return res;
    }
}
