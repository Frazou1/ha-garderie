using Garderie.Web.Configuration;
using Garderie.Web.Data;
using Garderie.Web.Infrastructure;
using Garderie.Web.Securite;
using Garderie.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Garderie.Web.Pages;

/// <summary>
/// Parametres de la garderie, saisis dans l'appli (jamais dans le code public) ; formulaire FO-0310 vierge ;
/// etat du courriel (configure dans les options du module).
/// Daycare settings, blank FO-0310 form, email status (configured in the add-on options).
/// </summary>
public class ParametresModel(GarderieDb db, Journal journal, GarderieOptions options, Courriels courriels, ContratsService contrats, Chiffreur chiffreur) : PageModel
{
    [BindProperty] public Parametres Donnees { get; set; } = new();
    public bool ModeleDisponible => System.IO.File.Exists(options.FichierModeleFo0310);
    public DateTime? ModeleDate => ModeleDisponible ? System.IO.File.GetLastWriteTimeUtc(options.FichierModeleFo0310) : null;
    public bool CourrielConfigure => courriels.EstConfigure;
    public string UrlPublique => options.UrlPublique;
    public string CompteCourriel => options.SmtpUtilisateur;
    /// <summary>Texte de chaque section de la regie (cle -> texte) / Text of each rules section.</summary>
    [BindProperty] public Dictionary<string, string?> Regie { get; set; } = new();
    [BindProperty] public string? RegieTarif { get; set; }
    [BindProperty] public string? RegieJourPaiement { get; set; }
    [BindProperty] public string? RegieFraisRetard { get; set; }
    [BindProperty] public string? RegieModesPaiement { get; set; }
    public DateTime? RegieModifieeLe { get; private set; }
    /// <summary>Sections modifiees (differentes du texte par defaut) / Edited sections.</summary>
    public HashSet<string> RegieModifiees { get; private set; } = new();

    /// <summary>Textes du contrat a afficher (Reglages, sinon textes par defaut) / Contract texts to show.</summary>
    public sealed class SaisieContrat
    {
        public string? VilleSignature { get; set; }
        public string? NoteVacances { get; set; }
        public string ObligationsRsge { get; set; } = "";
        public string ObligationsParent { get; set; } = "";
        public string MentionLpc { get; set; } = "";
        public string Attestation { get; set; } = "";
        public string NonRespect { get; set; } = "";
        public string Services { get; set; } = "";
        public string ClausesFermeture { get; set; } = "";
        public string ResiliationRsge { get; set; } = "";
        public string ResiliationParent { get; set; } = "";
        public string? HeureCollationMatin { get; set; }
        public string? HeureCollationPm { get; set; }
        public string? HeureDiner { get; set; }
    }

    [BindProperty] public SaisieContrat Contrat { get; set; } = new();

    public async Task OnGetAsync()
    {
        Donnees = await db.Parametres.FindAsync(1) ?? new Parametres();
        Donnees.DebutPeriodes ??= FichesService.AncreParDefaut;
        if (Donnees.Rl24Rue is null && Donnees.Rl24Ville is null && !string.IsNullOrWhiteSpace(Donnees.Adresse))
        {
            // Ancienne adresse sur une ligne : decoupee pour la verifier / Old one-line address, split to check
            var a = RelevesService.Decouper(Donnees.Adresse);
            (Donnees.Rl24Appartement, Donnees.Rl24Numero, Donnees.Rl24Rue, Donnees.Rl24Ville, Donnees.Rl24Province, Donnees.Rl24CodePostal) =
                (a.Appartement, a.Numero, a.Rue, a.Ville, a.Province, a.CodePostalAffiche);
        }
        Donnees.Rl24Province ??= "QC";
        var saisies = RegieInterne.Lire(Donnees.RegieSections);
        Regie = RegieInterne.Sections.ToDictionary(x => x.Cle, x => (string?)RegieInterne.Texte(x, saisies));
        RegieModifiees = saisies.Keys.ToHashSet();
        RegieTarif = Donnees.RegieTarif;
        RegieJourPaiement = Donnees.RegieJourPaiement;
        RegieFraisRetard = Donnees.RegieFraisRetard;
        RegieModesPaiement = Donnees.RegieModesPaiement ?? TextesContrat.ModesPaiement;
        RegieModifieeLe = Donnees.RegieModifieeLe;
        Rl24 = new SaisieRl24
        {
            Identifiant = chiffreur.DechiffrerTexte(Donnees.Rl24IdentifiantChiffre, Contextes.IdentifiantRl24) is { } id ? Masquer(id) : null,
            Signataire = Donnees.Rl24Signataire,
        };
        IdentifiantEnregistre = Donnees.Rl24IdentifiantChiffre is not null;
        if (System.IO.File.Exists(options.FichierRl24)
            && FormulaireProtege.LireValeurs(await System.IO.File.ReadAllBytesAsync(options.FichierRl24)).GetValueOrDefault("numSeq") is { Length: 9 } n)
            Rl24ProchainNumero = Releve24.Prochain(n, Donnees.Rl24DernierNumero);
        Contrat = new SaisieContrat
        {
            VilleSignature = Donnees.VilleSignature, NoteVacances = Donnees.ContratNoteVacances,
            ObligationsRsge = Donnees.ContratObligationsRsge ?? TextesContrat.ObligationsRsge,
            ObligationsParent = Donnees.ContratObligationsParent ?? TextesContrat.ObligationsParent,
            MentionLpc = Donnees.ContratMentionLpc ?? TextesContrat.MentionLpc,
            Attestation = Donnees.ContratAttestation ?? TextesContrat.Attestation,
            NonRespect = Donnees.ContratNonRespect ?? TextesContrat.NonRespect,
            Services = Donnees.ContratServices ?? TextesContrat.Services,
            ClausesFermeture = Donnees.ContratClausesFermeture ?? TextesContrat.ClausesFermeture,
            ResiliationRsge = Donnees.ContratResiliationRsge ?? TextesContrat.ResiliationRsge,
            ResiliationParent = Donnees.ContratResiliationParent ?? TextesContrat.ResiliationParent,
            HeureCollationMatin = Donnees.HeureCollationMatin, HeureCollationPm = Donnees.HeureCollationPm, HeureDiner = Donnees.HeureDiner,
        };
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ModelState.Remove("Donnees.Rl24CodePostal");   // « G1A 1A1 » : normalise plus bas / normalized below
        if (!ModelState.IsValid)
        {
            TempData["Erreur"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).Distinct());
            return RedirectToPage();
        }
        var p = await db.Parametres.FindAsync(1);
        if (p is null) { p = new Parametres { Id = 1 }; db.Parametres.Add(p); }
        p.NomGarderie = Vide(Donnees.NomGarderie);
        p.NomRsge = Vide(Donnees.NomRsge);
        // Adresse decoupee comme sur les formulaires ; la ligne complete sert au contrat
        // Address split like on the forms; the full line is used by the contract
        var cp = new string((Donnees.Rl24CodePostal ?? "").Where(char.IsAsciiLetterOrDigit).ToArray()).ToUpperInvariant();
        if (cp.Length is not (0 or 6))
        {
            TempData["Erreur"] = "Code postal : 6 caractères, ex. G1A 1A1.";
            return RedirectToPage();
        }
        static string? V(string? s, int max) => string.IsNullOrWhiteSpace(s) ? null : s.Trim() is var t && t.Length > max ? t[..max] : s.Trim();
        p.Rl24Appartement = V(Donnees.Rl24Appartement, 10);
        p.Rl24Numero = V(Donnees.Rl24Numero, 20);
        p.Rl24Rue = V(Donnees.Rl24Rue, 100);
        p.Rl24Ville = V(Donnees.Rl24Ville, 60);
        p.Rl24Province = V(Donnees.Rl24Province, 2)?.ToUpperInvariant();
        p.Rl24CodePostal = cp.Length == 0 ? null : cp;
        var adresse = new Releve24.Adresse(p.Rl24Appartement, p.Rl24Numero, p.Rl24Rue, p.Rl24Ville, p.Rl24Province, p.Rl24CodePostal);
        p.Adresse = Vide(string.Join(", ", new[] { adresse.Ligne1, adresse.Ligne2 }.Where(x => x.Length > 0)));
        p.Telephone = Vide(Donnees.Telephone);
        p.CourrielRsge = Vide(Donnees.CourrielRsge);
        p.NomBureauCoordonnateur = Vide(Donnees.NomBureauCoordonnateur);
        if (!string.IsNullOrWhiteSpace(Donnees.LienFacebook) && Affichage.Facebook(Donnees.LienFacebook) is null)
        {
            TempData["Erreur"] = "Le lien du groupe Facebook doit commencer par https://www.facebook.com/";
            return RedirectToPage();
        }
        p.LienFacebook = Affichage.Facebook(Donnees.LienFacebook);
        p.HeureOuverture = Donnees.HeureOuverture;
        p.HeureFermeture = Donnees.HeureFermeture;
        p.DebutPeriodes = Donnees.DebutPeriodes is { } d ? PresencesService.Lundi(d) : null;
        journal.Noter("parametres.modifier");
        await db.SaveChangesAsync();
        TempData["Message"] = "Paramètres enregistrés.";
        return RedirectToPage();
    }

    /// <summary>Televerse le formulaire FO-0310 vierge (verifie ses champs) / Uploads the blank FO-0310 (fields checked).</summary>
    public async Task<IActionResult> OnPostModeleAsync(IFormFile? modele)
    {
        if (modele is null || modele.Length == 0 || modele.Length > 5 * 1024 * 1024)
        {
            TempData["Erreur"] = "Choisissez le PDF FO-0310 (5 Mo maximum).";
            return RedirectToPage();
        }
        using var ms = new MemoryStream();
        await modele.CopyToAsync(ms);
        var octets = ms.ToArray();
        if (DocumentsService.DetecterType(octets) != "application/pdf")
        {
            TempData["Erreur"] = "Ce fichier n'est pas un PDF.";
            return RedirectToPage();
        }
        var manquants = Fo0310Pdf.Valider(octets);
        if (manquants.Count > 0)
        {
            TempData["Erreur"] = "Ce PDF n'est pas le formulaire FO-0310 à remplir (champs absents : " + string.Join(", ", manquants.Take(4)) + ").";
            return RedirectToPage();
        }
        Directory.CreateDirectory(Path.GetDirectoryName(options.FichierModeleFo0310)!);
        await System.IO.File.WriteAllBytesAsync(options.FichierModeleFo0310, octets);
        await journal.NoterMaintenantAsync("parametres.modele_fo0310", details: $"{octets.Length} octets");
        TempData["Message"] = "Formulaire FO-0310 enregistré.";
        return RedirectToPage();
    }

    /// <summary>Textes du contrat (vide = texte par defaut) / Contract texts (empty = default text).</summary>
    public async Task<IActionResult> OnPostContratAsync()
    {
        var p = await db.Parametres.FindAsync(1);
        if (p is null) { p = new Parametres { Id = 1 }; db.Parametres.Add(p); }
        static string? T(string? s, string defaut, int max)
        {
            var t = (s ?? "").Replace("\r\n", "\n").Trim();
            if (t.Length == 0 || t == defaut.Replace("\r\n", "\n").Trim()) return null;   // null = texte par defaut
            return t.Length > max ? t[..max] : t;
        }
        p.VilleSignature = Vide(Contrat.VilleSignature);
        p.ContratNoteVacances = Vide(Contrat.NoteVacances);
        p.ContratObligationsRsge = T(Contrat.ObligationsRsge, TextesContrat.ObligationsRsge, 4000);
        p.ContratObligationsParent = T(Contrat.ObligationsParent, TextesContrat.ObligationsParent, 4000);
        p.ContratMentionLpc = T(Contrat.MentionLpc, TextesContrat.MentionLpc, 6000);
        p.ContratAttestation = T(Contrat.Attestation, TextesContrat.Attestation, 2000);
        p.ContratNonRespect = T(Contrat.NonRespect, TextesContrat.NonRespect, 2000);
        p.ContratServices = T(Contrat.Services, TextesContrat.Services, 4000);
        p.ContratClausesFermeture = T(Contrat.ClausesFermeture, TextesContrat.ClausesFermeture, 4000);
        p.ContratResiliationRsge = T(Contrat.ResiliationRsge, TextesContrat.ResiliationRsge, 4000);
        p.ContratResiliationParent = T(Contrat.ResiliationParent, TextesContrat.ResiliationParent, 2000);
        static string? Heure(string? h) => Vide(h) is { } v ? v[..Math.Min(20, v.Length)] : null;
        p.HeureCollationMatin = Heure(Contrat.HeureCollationMatin);
        p.HeureCollationPm = Heure(Contrat.HeureCollationPm);
        p.HeureDiner = Heure(Contrat.HeureDiner);
        journal.Noter("parametres.contrat");
        await db.SaveChangesAsync();
        TempData["Message"] = "Textes du contrat enregistrés (ils s'appliquent aux contrats pas encore signés).";
        return RedirectToPage();
    }

    /// <summary>Sections et valeurs de la regie interne (le PDF est genere) / Rules sections and values (PDF generated).</summary>
    public async Task<IActionResult> OnPostRegieAsync()
    {
        var p = await db.Parametres.FindAsync(1);
        if (p is null) { p = new Parametres { Id = 1 }; db.Parametres.Add(p); }
        if (Regie.Values.Any(t => (t?.Length ?? 0) > 20000))
        {
            TempData["Erreur"] = "Une section dépasse 20 000 caractères.";
            return RedirectToPage(null, null, "regie");
        }
        static string? Court(string? s, int max) => string.IsNullOrWhiteSpace(s) ? null : s.Trim() is var t && t.Length > max ? t[..max] : s.Trim();
        p.RegieSections = RegieInterne.Ecrire(Regie);
        p.RegieTarif = Court(RegieTarif, 50);
        p.RegieJourPaiement = Court(RegieJourPaiement, 30);
        p.RegieFraisRetard = Court(RegieFraisRetard, 200);
        p.RegieModesPaiement = Court(RegieModesPaiement, 100) is { } mp && mp != TextesContrat.ModesPaiement ? mp : null;
        p.RegieModifieeLe = DateTime.UtcNow;
        try { RegieInterne.Pdf(p); }
        catch (Exception ex) { TempData["Erreur"] = $"Le PDF n'a pas pu être généré ({ex.GetType().Name})."; return RedirectToPage(null, null, "regie"); }
        journal.Noter("parametres.regie_interne");
        await db.SaveChangesAsync();
        TempData["Message"] = "Régie interne enregistrée : le PDF remis aux parents est à jour.";
        return RedirectToPage(null, null, "regie");
    }

    // --- Releve 24 ---

    public sealed class SaisieRl24
    {
        public string? Identifiant { get; set; }
        public string? Signataire { get; set; }
    }

    [BindProperty] public SaisieRl24 Rl24 { get; set; } = new();
    public bool IdentifiantEnregistre { get; private set; }
    public string? Rl24ProchainNumero { get; private set; }
    public bool Rl24Present => System.IO.File.Exists(options.FichierRl24);
    public bool Rl24SPresent => System.IO.File.Exists(options.FichierRl24S);

    /// <summary>Identifiant masque (5 derniers chiffres caches) / Masked identifier.</summary>
    private static string Masquer(string id) => new string('•', id.Length - 4) + id[^4..];

    /// <summary>Renseignements du releve 24 (NEQ ou NAS chiffre, adresse) / RL-24 info (encrypted NEQ or SIN, address).</summary>
    public async Task<IActionResult> OnPostRl24Async()
    {
        var p = await db.Parametres.FindAsync(1);
        if (p is null) { p = new Parametres { Id = 1 }; db.Parametres.Add(p); }
        var id = new string((Rl24.Identifiant ?? "").Where(char.IsAsciiDigit).ToArray());
        if (!string.IsNullOrWhiteSpace(Rl24.Identifiant) && !Rl24.Identifiant.Contains('•'))
        {
            if (id.Length == 9 && Identifiants.NormaliserNas(id) is null || id.Length is not (9 or 10))
            {
                TempData["Erreur"] = "Case H : entrez votre NEQ (10 chiffres) ou votre NAS (9 chiffres valides).";
                return RedirectToPage(null, null, "rl24");
            }
            p.Rl24IdentifiantChiffre = chiffreur.ChiffrerTexte(id, Contextes.IdentifiantRl24);
        }
        static string? V(string? s, int max) => string.IsNullOrWhiteSpace(s) ? null : s.Trim() is var t && t.Length > max ? t[..max] : s.Trim();
        p.Rl24Signataire = V(Rl24.Signataire, 100);
        journal.Noter("parametres.rl24");   // jamais l'identifiant / never the identifier
        await db.SaveChangesAsync();
        TempData["Message"] = "Renseignements du relevé 24 enregistrés.";
        return RedirectToPage(null, null, "rl24");
    }

    /// <summary>Formulaire officiel RL-24 ou RL-24.S de Revenu Quebec / Official RL-24 or RL-24.S form.</summary>
    /// <summary>
    /// Apercu du RL-24 ou du sommaire : vos Reglages, un parent et un enfant fictifs (NAS de test), prochain numero
    /// sans le reserver. / Preview: your settings, a fictitious parent and child (test SIN), next number not reserved.
    /// </summary>
    public async Task<IActionResult> OnGetApercuRl24Async(string? quel)
    {
        var chemin = quel == "sommaire" ? options.FichierRl24S : options.FichierRl24;
        if (!System.IO.File.Exists(chemin)) return NotFound();
        var p = await db.Parametres.AsNoTracking().FirstOrDefaultAsync() ?? new Parametres();
        var modele = await System.IO.File.ReadAllBytesAsync(chemin);
        var fournisseur = new Releve24.Fournisseur(p.NomGarderie ?? "", p.NomRsge,
            new Releve24.Adresse(p.Rl24Appartement, p.Rl24Numero, p.Rl24Rue, p.Rl24Ville, p.Rl24Province, p.Rl24CodePostal),
            chiffreur.DechiffrerTexte(p.Rl24IdentifiantChiffre, Contextes.IdentifiantRl24));
        var tarif = decimal.TryParse(new string((p.RegieTarif ?? "").Where(c => char.IsAsciiDigit(c) || c is '.' or ',').ToArray()).Replace(',', '.'),
            System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var t) ? t : 45m;
        var annee = Temps.Aujourdhui().Year;
        var enfant = new Releve24.EnfantReleve("EXEMPLE", "Enfant", new DateOnly(annee - 3, 3, 14), 200, 200 * tarif, 0m);
        byte[] pdf;
        if (quel == "sommaire")
            pdf = FormulaireProtege.Remplir(modele, Releve24.ChampsSommaire(new Releve24.ContenuSommaire(
                annee, fournisseur, 1, 1, enfant.Jours, enfant.Admissible, p.Rl24Signataire, p.Telephone, Temps.Aujourdhui())));
        else
            pdf = FormulaireProtege.Remplir(modele, Releve24.ChampsReleve(new Releve24.ContenuReleve(
                annee, Releve24.Prochain(FormulaireProtege.LireValeurs(modele)["numSeq"], p.Rl24DernierNumero), 'R', null,
                "EXEMPLE", "Parent", new Releve24.Adresse(null, "1", "rue de l'Exemple", "Québec", "QC", "G1A1A1"), "130692544",
                fournisseur, [enfant])));
        return this.Fichier(pdf, "application/pdf", quel == "sommaire" ? "Aperçu sommaire 24.pdf" : "Aperçu relevé 24.pdf", afficher: true);
    }

    public async Task<IActionResult> OnPostFormulaireRl24Async(string? quel, IFormFile? fichier)
    {
        if (quel is not ("releve" or "sommaire")) return BadRequest();
        if (fichier is null || fichier.Length == 0 || fichier.Length > 5 * 1024 * 1024)
        {
            TempData["Erreur"] = "Choisissez le PDF de Revenu Québec (5 Mo maximum).";
            return RedirectToPage(null, null, "rl24");
        }
        using var ms = new MemoryStream();
        await fichier.CopyToAsync(ms);
        var octets = ms.ToArray();
        var nom = quel == "releve" ? "RL-24" : "RL-24.S";
        try
        {
            if (DocumentsService.DetecterType(octets) != "application/pdf") throw new InvalidDataException("pas un PDF");
            var manquants = FormulaireProtege.Verifier(octets, quel == "releve" ? Releve24.ChampsRl24 : Releve24.ChampsRl24S);
            if (manquants.Count > 0)
            {
                TempData["Erreur"] = $"Ce PDF n'est pas le {nom} à remplir de Revenu Québec (champs absents : {string.Join(", ", manquants.Take(4))}).";
                return RedirectToPage(null, null, "rl24");
            }
            if (quel == "releve" && !Releve24.Valide(FormulaireProtege.LireValeurs(octets).GetValueOrDefault("numSeq")))
            {
                TempData["Erreur"] = "Ce RL-24 n'a pas de numéro séquentiel valide : prenez la version « PDF remplissable à l'écran ».";
                return RedirectToPage(null, null, "rl24");
            }
        }
        catch (Exception ex) when (ex is InvalidDataException or PdfSharp.Pdf.IO.PdfReaderException)
        {
            TempData["Erreur"] = $"Ce fichier n'est pas le {nom} « PDF remplissable à l'écran » de Revenu Québec.";
            return RedirectToPage(null, null, "rl24");
        }
        var chemin = quel == "releve" ? options.FichierRl24 : options.FichierRl24S;
        Directory.CreateDirectory(Path.GetDirectoryName(chemin)!);
        await System.IO.File.WriteAllBytesAsync(chemin, octets);
        await journal.NoterMaintenantAsync("parametres.formulaire_rl24", details: $"{nom} ({octets.Length} octets)");
        TempData["Message"] = $"Formulaire {nom} enregistré.";
        return RedirectToPage(null, null, "rl24");
    }

    public bool LogoPresent => System.IO.File.Exists(options.FichierLogo);
    public string CheminLogo => options.FichierLogo;

    /// <summary>Logo (PNG ou JPG) affiche en haut de l'appli et des pages des parents / Logo shown at the top.</summary>
    public async Task<IActionResult> OnPostLogoAsync(IFormFile? logo)
    {
        if (logo is null || logo.Length == 0 || logo.Length > 2 * 1024 * 1024)
        {
            TempData["Erreur"] = "Choisissez une image PNG ou JPG (2 Mo maximum).";
            return RedirectToPage(null, null, "logo");
        }
        using var ms = new MemoryStream();
        await logo.CopyToAsync(ms);
        if (DocumentsService.DetecterType(ms.ToArray()) is not ("image/png" or "image/jpeg"))
        {
            TempData["Erreur"] = "Le logo doit être une image PNG ou JPG.";
            return RedirectToPage(null, null, "logo");
        }
        Directory.CreateDirectory(Path.GetDirectoryName(options.FichierLogo)!);
        await System.IO.File.WriteAllBytesAsync(options.FichierLogo, ms.ToArray());
        await journal.NoterMaintenantAsync("parametres.logo", details: $"{ms.Length} octets");
        TempData["Message"] = "Logo enregistré.";
        return RedirectToPage(null, null, "logo");
    }

    public async Task<IActionResult> OnPostRetirerLogoAsync()
    {
        if (System.IO.File.Exists(options.FichierLogo)) System.IO.File.Delete(options.FichierLogo);
        await journal.NoterMaintenantAsync("parametres.logo_retire");
        TempData["Message"] = "Logo retiré : le petit bateau revient.";
        return RedirectToPage(null, null, "logo");
    }

    /// <summary>Apercu du modele de contrat (champs de l'enfant et du parent a remplir) / Contract template preview.</summary>
    public async Task<IActionResult> OnGetContratModeleAsync(bool telecharger = false) =>
        this.Fichier(await contrats.ModelePdfAsync(), "application/pdf", "Modèle de contrat.pdf", !telecharger);

    /// <summary>PDF de la regie (tel que remis aux parents) / Rules PDF (as given to parents).</summary>
    public async Task<IActionResult> OnGetRegiePdfAsync(bool telecharger = false)
    {
        var p = await db.Parametres.AsNoTracking().FirstOrDefaultAsync() ?? new Parametres();
        return this.Fichier(RegieInterne.Pdf(p), "application/pdf", "Régie interne.pdf", !telecharger);
    }

    /// <summary>Courriel de test a la RSGE / Test email to the RSGE.</summary>
    public async Task<IActionResult> OnPostTestCourrielAsync()
    {
        var p = await db.Parametres.FindAsync(1);
        if (!courriels.EstConfigure || p?.CourrielRsge is null)
        {
            TempData["Erreur"] = "Configurez le compte courriel dans les options du module et le courriel de la RSGE ici.";
            return RedirectToPage();
        }
        try
        {
            await courriels.EnvoyerAsync(p.CourrielRsge, "Garderie : courriel de test", "Ce courriel confirme que l'envoi fonctionne.");
            TempData["Message"] = $"Courriel de test envoyé à {p.CourrielRsge}.";
        }
        catch (Exception ex)
        {
            TempData["Erreur"] = $"Échec de l'envoi ({ex.GetType().Name}) : vérifiez le compte et le mot de passe d'application.";
        }
        return RedirectToPage();
    }

    private static string? Vide(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
