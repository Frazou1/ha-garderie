using System.Security.Cryptography;
using System.Text.Json;
using Garderie.Web.Configuration;
using Garderie.Web.Data;
using Garderie.Web.Securite;
using Microsoft.EntityFrameworkCore;

namespace Garderie.Web.Services;

/// <summary>Renseignements saisis par le parent avant de signer / Info entered by the parent before signing.</summary>
public sealed class SaisieParent
{
    public string? EnfantAdresse { get; set; }
    public string? EnfantTelephone { get; set; }
    public string? ParentAdresse { get; set; }
    public string? ParentTelephone1 { get; set; }
    public string? ParentTelephone2 { get; set; }
    public string? Lien { get; set; }
    /// <summary>Nouveau NAS (vide = garder celui deja au dossier) / New SIN (empty = keep the one on file).</summary>
    public string? Nas { get; set; }
    public string? VacancesParent { get; set; }
    /// <summary>Consent a recevoir le releve 24 par courriel / Consents to receive the RL-24 by email.</summary>
    public bool ConsentementRl24 { get; set; }
}

/// <summary>
/// Ententes de service : creation, lien unique au parent, signature du parent (avec ses renseignements),
/// contresignature de la RSGE, PDF, resiliation.
/// Service agreements: creation, one-time parent link, parent signature, RSGE countersignature, PDF, termination.
/// </summary>
public sealed class ContratsService(GarderieDb db, GarderieOptions options, DocumentsService documents, Chiffreur chiffreur, Courriels courriels)
{
    public static readonly TimeSpan DureeLien = TimeSpan.FromDays(14);

    /// <summary>PDF de la regie interne, genere des sections de Reglages / Internal rules PDF, generated.</summary>
    public async Task<byte[]> RegiePdfAsync() =>
        RegieInterne.Pdf(await db.Parametres.AsNoTracking().FirstOrDefaultAsync() ?? new Parametres());

    // --- Renseignements / Info ---

    /// <summary>Fermetures de la periode, regroupees (jours consecutifs, meme motif) / Grouped closures.</summary>
    public async Task<List<string>> FermeturesAsync(DateOnly debut, DateOnly fin) =>
        (await FermeturesDetailAsync(debut, fin)).Select(f =>
            (f.Debut == f.Fin ? $"{f.Motif} : {Affichage.Date(f.Debut)}" : $"{f.Motif} : du {Affichage.Date(f.Debut)} au {Affichage.Date(f.Fin)}")
            + (f.Demi is null ? "" : $" ({f.Demi})")).ToList();

    /// <summary>Fermetures regroupees, en donnees / Grouped closures, as data.</summary>
    public async Task<List<FermetureContrat>> FermeturesDetailAsync(DateOnly debut, DateOnly fin)
    {
        var jours = await db.Fermetures.AsNoTracking().Where(f => f.Date >= debut && f.Date <= fin).OrderBy(f => f.Date).ToListAsync();
        var lignes = new List<FermetureContrat>();
        for (var i = 0; i < jours.Count;)
        {
            var j = i;
            // Meme motif, jours qui se suivent (en sautant la fin de semaine) / Same reason, consecutive days
            while (j + 1 < jours.Count && jours[j + 1].Motif == jours[i].Motif && jours[j + 1].Date.DayNumber - jours[j].Date.DayNumber <= 3
                   && Enumerable.Range(1, jours[j + 1].Date.DayNumber - jours[j].Date.DayNumber - 1)
                       .All(k => jours[j].Date.AddDays(k).DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday))
                j++;
            var motif = string.IsNullOrWhiteSpace(jours[i].Motif) ? "Fermeture" : jours[i].Motif;
            var demi = jours[i].Demi switch { DemiJournee.Matin => "matin", DemiJournee.Pm => "après-midi", _ => null };
            lignes.Add(new FermetureContrat(motif!, jours[i].Date, jours[j].Date, i == j ? demi : null));
            i = j + 1;
        }
        return lignes;
    }

    /// <summary>Parent du contrat (payeur, sinon le premier) et son lien / Contract parent and relationship.</summary>
    public async Task<EnfantParent?> LienParentAsync(Contrat c) =>
        await db.EnfantParents.Include(x => x.Parent)
            .Where(x => x.EnfantId == c.EnfantId && (c.ParentId == null || x.ParentId == c.ParentId))
            .OrderByDescending(x => x.EstPayeur).ThenBy(x => x.Ordre).FirstOrDefaultAsync();

    /// <summary>Renseignements de la garderie et textes, sans enfant ni parent / Daycare info and texts, no child or parent.</summary>
    private async Task<InfosContrat> InfosGarderieAsync(DateOnly debut, DateOnly fin)
    {
        var p = await db.Parametres.AsNoTracking().FirstOrDefaultAsync() ?? new Parametres();
        var garderie = p.NomGarderie ?? "Garderie";
        static string? H(TimeOnly? t) => t is { } x ? $"{x:H\\ \\h\\ mm}".Replace(" h 00", " h") : null;
        var heures = p.HeureOuverture is { } o && p.HeureFermeture is { } f ? $"de {o:H\\ \\h\\ mm} à {f:H\\ \\h\\ mm}".Replace(" h 00", " h") : null;
        return new InfosContrat
        {
            Garderie = garderie, Rsge = p.NomRsge, AdresseGarderie = p.Adresse, TelephoneGarderie = p.Telephone, Heures = heures,
            Fermetures = await FermeturesAsync(debut, fin), FermeturesDetail = await FermeturesDetailAsync(debut, fin), NoteVacances = p.ContratNoteVacances,
            ObligationsRsge = p.ContratObligationsRsge ?? TextesContrat.ObligationsRsge,
            ObligationsParent = p.ContratObligationsParent ?? TextesContrat.ObligationsParent,
            MentionLpc = p.ContratMentionLpc ?? TextesContrat.MentionLpc,
            Attestation = TextesContrat.Remplir(p.ContratAttestation ?? TextesContrat.Attestation, garderie, p.NomRsge),
            NonRespect = TextesContrat.Remplir(p.ContratNonRespect ?? TextesContrat.NonRespect, garderie, p.NomRsge),
            Regie = RegieInterne.Composer(p), Ville = p.VilleSignature, Debut = debut, Fin = fin,
            RsgeCourriel = p.CourrielRsge, Services = p.ContratServices ?? TextesContrat.Services,
            HeureCollationMatin = p.HeureCollationMatin, HeureCollationPm = p.HeureCollationPm, HeureDiner = p.HeureDiner,
            Tarif = RelevesService.Tarif(p.RegieTarif), JourPaiement = p.RegieJourPaiement,
            ModesPaiement = p.RegieModesPaiement ?? TextesContrat.ModesPaiement, FraisRetard = p.RegieFraisRetard,
            ClausesFermeture = p.ContratClausesFermeture ?? TextesContrat.ClausesFermeture,
            ResiliationRsge = p.ContratResiliationRsge ?? TextesContrat.ResiliationRsge,
            ResiliationParent = p.ContratResiliationParent ?? TextesContrat.ResiliationParent,
            Frequentation = Frequentation([], debut).Jours, DebutFrequentation = debut,
            HeureOuverture = H(p.HeureOuverture), HeureFermeture = H(p.HeureFermeture),
            FraisNsf = p.ContratFraisNsf, RetardMontant = p.ContratRetardMontant, RetardMinutes = p.ContratRetardMinutes,
        };
    }

    /// <summary>Renseignements actuels (avant la signature du parent) / Current info (before the parent signs).</summary>
    public async Task<InfosContrat> InfosActuellesAsync(Contrat c)
    {
        var e = await db.Enfants.AsNoTracking().Include(x => x.Horaires).FirstAsync(x => x.Id == c.EnfantId);
        var lien = await LienParentAsync(c);
        var infos = await InfosGarderieAsync(c.Debut, c.Fin);
        var (jours, versement) = Frequentation(e.Horaires, c.Debut);
        return infos with
        {
            EnfantPortail = e.NumeroPortail, ParentCourriel = lien?.Parent.Courriel, Frequentation = jours,
            DebutFrequentation = e.DateDebut > c.Debut ? e.DateDebut : c.Debut,
            Versement = infos.Tarif is { } t ? versement * t : null,
            Total = infos.Tarif is { } t2 ? Math.Round(JoursPrevus(e.Horaires, c.Debut, c.Fin) * t2, 2) : null,
            EnfantNom = e.NomComplet, EnfantAdresse = e.Adresse, EnfantTelephone = e.Telephone, EnfantNaissance = e.DateNaissance, EnfantSexe = e.Sexe,
            ParentNom = lien?.Parent.NomComplet ?? "", ParentAdresse = lien?.Parent.Adresse, ParentTelephone1 = lien?.Parent.Telephone1,
            ParentTelephone2 = lien?.Parent.Telephone2, ParentLien = lien?.Lien, VacancesParent = c.VacancesParent,
            EnfantPrenom = e.Prenom, EnfantNomFamille = e.Nom, ParentPrenom = lien?.Parent.Prenom, ParentNomFamille = lien?.Parent.Nom,
            ParentTelephoneAutre = lien?.Parent.TelephoneAutreParent,
        };
    }

    private static readonly DayOfWeek[] Semaine =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday];

    /// <summary>Poids d'un jour selon l'horaire : 1 journee, 0,5 demi-journee / Day weight from the schedule.</summary>
    private static decimal Poids(List<HoraireType> horaires, DateOnly jour)
    {
        if (horaires.Count == 0) return jour.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? 0 : 1;
        var (m, p) = PresencesService.HorairePrevu(horaires, jour);
        return (m ? 0.5m : 0) + (p ? 0.5m : 0);
    }

    /// <summary>
    /// Jours de frequentation de la semaine qui commence le contrat (sans horaire : lundi au vendredi) et jours payes
    /// par semaine. / Attendance days of the contract's first week (no schedule: Mon-Fri) and paid days per week.
    /// </summary>
    public static (List<string> Jours, decimal ParSemaine) Frequentation(List<HoraireType> horaires, DateOnly debut)
    {
        var jours = new List<string>();
        decimal total = 0;
        foreach (var j in Semaine)
        {
            var d = debut.AddDays(((int)j - (int)debut.DayOfWeek + 7) % 7);
            var poids = Poids(horaires, d);
            if (poids == 0) continue;
            total += poids;
            var nom = Affichage.Jour(j);
            if (poids < 1) nom += PresencesService.HorairePrevu(horaires, d).Matin ? " (matin)" : " (après-midi)";
            jours.Add(nom);
        }
        return (jours, total);
    }

    /// <summary>Jours payes prevus sur la duree du contrat (les fermetures sont payables) / Paid days over the contract.</summary>
    public static decimal JoursPrevus(List<HoraireType> horaires, DateOnly debut, DateOnly fin)
    {
        decimal n = 0;
        for (var d = debut; d <= fin; d = d.AddDays(1)) n += Poids(horaires, d);
        return n;
    }

    /// <summary>
    /// Modele du contrat (Reglages) : textes et fermetures actuels sur un an a partir d'aujourd'hui, champs de l'enfant
    /// et du parent a remplir, sans signature. / Contract template preview: current texts, blank child/parent fields.
    /// </summary>
    public async Task<byte[]> ModelePdfAsync()
    {
        var debut = Temps.Aujourdhui();
        var infos = await InfosGarderieAsync(debut, debut.AddYears(1).AddDays(-1));
        return ContratPdf.Contrat(infos, new SignaturesContrat(null, null, null, null, null, null, null, null, null, null));
    }

    /// <summary>Renseignements figes si le parent a signe, sinon actuels / Frozen info if signed, else current.</summary>
    public async Task<InfosContrat> InfosAsync(Contrat c) =>
        c.Fige is { } json ? JsonSerializer.Deserialize<InfosContrat>(json)! : await InfosActuellesAsync(c);

    private async Task<SignaturesContrat> SignaturesAsync(Contrat c)
    {
        string? nas = null;
        if (c.NasChiffre is not null) nas = chiffreur.DechiffrerTexte(c.NasChiffre, Contextes.NasContrat);
        else if ((await LienParentAsync(c))?.Parent.NasChiffre is { } pn) nas = chiffreur.DechiffrerTexte(pn, Contextes.NasParent);
        DateOnly? Jour(DateTime? d) => d is { } x ? DateOnly.FromDateTime(Temps.VersLocal(x)) : null;
        return new SignaturesContrat(nas,
            await documents.LireFichierAsync(c.SignatureParentFichier), Jour(c.SigneParentLe), c.SignataireParent,
            await documents.LireFichierAsync(c.SignatureRsgeFichier), Jour(c.SigneRsgeLe),
            c.ResilieLe, await documents.LireFichierAsync(c.SignatureResiliationFichier), Jour(c.ResiliationSigneeLe), c.SignataireResiliation);
    }

    public async Task<byte[]> PdfAsync(Contrat c) => ContratPdf.Contrat(await InfosAsync(c), await SignaturesAsync(c));
    public async Task<byte[]> PdfResiliationAsync(Contrat c) => ContratPdf.Resiliation(await InfosAsync(c), await SignaturesAsync(c));

    public static string NomPdf(Contrat c, Enfant e) => $"Contrat {e.Prenom} {e.Nom} {c.Debut:yyyy-MM-dd}.pdf";
    public static string NomPdfResiliation(Contrat c, Enfant e) => $"Résiliation {e.Prenom} {e.Nom} {c.ResilieLe:yyyy-MM-dd}.pdf";

    // --- Liens / Links ---

    public async Task<string> CreerLienAsync(Contrat c, ButLien but, string? envoyeA, bool surPlace)
    {
        await AnnulerLiensAsync(c.Id);
        var jeton = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        db.LiensContrats.Add(new LienContrat
        {
            ContratId = c.Id, But = but, JetonHash = FichesService.Hacher(jeton), CreeLe = DateTime.UtcNow,
            ExpireLe = DateTime.UtcNow + (surPlace ? TimeSpan.FromHours(1) : DureeLien), EnvoyeA = envoyeA, SurPlace = surPlace,
        });
        await db.SaveChangesAsync();
        return jeton;
    }

    /// <summary>Lien valide et dont le contrat attend bien ce geste / Valid link whose contract awaits this action.</summary>
    public async Task<LienContrat?> TrouverLienAsync(string? jeton)
    {
        if (string.IsNullOrWhiteSpace(jeton) || jeton.Length > 100) return null;
        var hash = FichesService.Hacher(jeton);
        var l = await db.LiensContrats.Include(x => x.Contrat).ThenInclude(c => c.Enfant).FirstOrDefaultAsync(x => x.JetonHash == hash);
        if (l is null || l.UtiliseLe is not null || l.AnnuleLe is not null || l.ExpireLe < DateTime.UtcNow) return null;
        var attendu = l.But == ButLien.Signature
            ? l.Contrat.Etat == EtatContrat.Brouillon
            : l.Contrat.Etat == EtatContrat.Signe && l.Contrat.ResilieLe is not null;
        return attendu ? l : null;
    }

    /// <summary>Lien deja utilise (double toucher) : on remercie / Already used link: thank.</summary>
    public async Task<bool> DejaUtiliseAsync(string? jeton)
    {
        if (string.IsNullOrWhiteSpace(jeton) || jeton.Length > 100) return false;
        var hash = FichesService.Hacher(jeton);
        return await db.LiensContrats.AnyAsync(x => x.JetonHash == hash && x.UtiliseLe != null);
    }

    public async Task AnnulerLiensAsync(int contratId) =>
        await db.LiensContrats.Where(l => l.ContratId == contratId && l.UtiliseLe == null && l.AnnuleLe == null)
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.AnnuleLe, DateTime.UtcNow));

    public string? UrlLien(string jeton) =>
        string.IsNullOrWhiteSpace(options.UrlPublique) ? null : $"{options.UrlPublique}/Public/Contrat?j={jeton}";

    /// <summary>Envoie le lien au parent par courriel ; retourne son courriel / Emails the link; returns the address.</summary>
    public async Task<string> EnvoyerAsync(Contrat c, ButLien but)
    {
        var lien = await LienParentAsync(c) ?? throw new InvalidOperationException("Aucun parent au dossier.");
        var courriel = lien.Parent.Courriel ?? throw new InvalidOperationException("Le parent n'a pas de courriel.");
        var jeton = await CreerLienAsync(c, but, courriel, surPlace: false);
        var p = await db.Parametres.AsNoTracking().FirstOrDefaultAsync();
        var garderie = p?.NomGarderie ?? "la garderie";
        var e = c.Enfant ?? await db.Enfants.AsNoTracking().FirstAsync(x => x.Id == c.EnfantId);
        var (sujet, intro) = but == ButLien.Signature
            ? ($"Entente de service de {e.Prenom} à signer",
               $"L'entente de service (contrat) de {e.Prenom} du {Affichage.Date(c.Debut)} au {Affichage.Date(c.Fin)} est prête.\n" +
               "Vérifiez et complétez vos renseignements, lisez l'entente et la régie interne, puis signez ici")
            : ($"Résiliation de l'entente de service de {e.Prenom}",
               $"Voici la formule de résiliation de l'entente de service de {e.Prenom}, à compter du {Affichage.Date(c.ResilieLe)}.\n" +
               "Vérifiez-la et signez-la ici");
        // La regie interne est jointe : le parent atteste l'avoir lue / Internal rules attached: the parent attests reading them
        var pieces = but == ButLien.Signature ? new List<(string, byte[])> { ("Régie interne.pdf", await RegiePdfAsync()) } : null;
        try
        {
            await courriels.EnvoyerAsync(courriel, sujet,
                $"Bonjour {lien.Parent.Prenom},\n\n{intro} (lien personnel, valide 14 jours, une seule utilisation) :\n\n" +
                $"{UrlLien(jeton)}\n\n" +
                (pieces is null ? "" : "La régie interne du service de garde est jointe à ce courriel ; elle est aussi accessible sur la page de signature.\n\n") +
                Affichage.PiedFacebook(p) +
                $"Merci,\n{p?.NomRsge ?? garderie}\n{garderie}", pieces);
        }
        catch
        {
            await AnnulerLiensAsync(c.Id);
            throw;
        }
        return courriel;
    }

    // --- Signatures ---

    /// <summary>
    /// Signature du parent : met a jour le dossier avec ses renseignements, fige le contrat. Retourne une erreur ou null.
    /// Parent signs: updates the record with their info, freezes the contract. Returns an error or null.
    /// </summary>
    public async Task<string?> SignerParentAsync(LienContrat lien, SaisieParent s, byte[] png, string signataire, string ip)
    {
        var c = lien.Contrat;
        var lp = await LienParentAsync(c);
        if (lp is null) return "Aucun parent au dossier : communiquez avec l'éducatrice.";
        string? nas = null;
        if (!string.IsNullOrWhiteSpace(s.Nas))
        {
            nas = Identifiants.NormaliserNas(s.Nas);
            if (nas is null) return "Le NAS n'est pas valide (9 chiffres).";
        }
        else if (lp.Parent.NasChiffre is null) return "Le NAS est requis pour le relevé 24.";

        static string? V(string? x, int max) => string.IsNullOrWhiteSpace(x) ? null : (x.Trim().Length > max ? x.Trim()[..max] : x.Trim());
        var e = await db.Enfants.FirstAsync(x => x.Id == c.EnfantId);
        e.Adresse = V(s.EnfantAdresse, 300); e.Telephone = V(s.EnfantTelephone, 30); e.ModifieLe = DateTime.UtcNow;
        var p = lp.Parent;
        p.Adresse = V(s.ParentAdresse, 300); p.Telephone1 = V(s.ParentTelephone1, 30); p.Telephone2 = V(s.ParentTelephone2, 30);
        if (nas is not null) p.NasChiffre = chiffreur.ChiffrerTexte(nas, Contextes.NasParent);
        // Consentement donne en signant (identite : lien personnel et signature) ; decocher ne le retire pas
        // Consent given when signing; unchecking does not revoke it (revocation is by notice to the RSGE)
        if (s.ConsentementRl24 && p.ConsentRl24Le is null)
        {
            p.ConsentRl24Le = DateTime.UtcNow;
            p.ConsentRl24Source = lien.SurPlace ? "Signature du contrat sur place" : "Signature du contrat en ligne";
        }
        p.ModifieLe = DateTime.UtcNow;
        if (V(s.Lien, 50) is { } l) lp.Lien = l;
        c.VacancesParent = V(s.VacancesParent, 1000);
        c.ParentId = p.Id;
        await db.SaveChangesAsync();

        // Fige renseignements, textes et NAS / Freeze info, texts and SIN
        c.Fige = JsonSerializer.Serialize(await InfosActuellesAsync(c));
        var nasClair = nas ?? chiffreur.DechiffrerTexte(p.NasChiffre, Contextes.NasParent);
        c.NasChiffre = chiffreur.ChiffrerTexte(nasClair, Contextes.NasContrat);
        c.SignatureParentFichier = await documents.EnregistrerOctetsAsync(png);
        c.SigneParentLe = DateTime.UtcNow;
        c.SignataireParent = signataire;
        c.IpParent = ip.Length > 64 ? ip[..64] : ip;
        c.SigneSurPlace = lien.SurPlace;
        c.Etat = EtatContrat.SigneParent;
        c.ModifieLe = DateTime.UtcNow;
        lien.UtiliseLe = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return null;
    }

    /// <summary>
    /// Contresignature de la RSGE : PDF final dans le dossier, copie par courriel a la RSGE et au parent
    /// (la Loi sur la protection du consommateur exige une copie au consommateur). Retourne une erreur ou null.
    /// RSGE countersigns: final PDF stored, emailed to the RSGE and the parent.
    /// </summary>
    public async Task<string?> SignerRsgeAsync(int contratId, byte[] png, string? par)
    {
        var c = await db.Contrats.Include(x => x.Enfant).FirstOrDefaultAsync(x => x.Id == contratId);
        if (c is null) return "Contrat introuvable.";
        if (c.Etat != EtatContrat.SigneParent) return $"{c.Enfant.Prenom} : le parent doit d'abord signer.";
        c.SignatureRsgeFichier = await documents.EnregistrerOctetsAsync(png);
        c.SigneRsgeLe = DateTime.UtcNow;
        c.SigneRsgePar = par;
        c.Etat = EtatContrat.Signe;
        c.ModifieLe = DateTime.UtcNow;
        var pdf = await PdfAsync(c);
        var nom = NomPdf(c, c.Enfant);
        var doc = await documents.EnregistrerPdfAsync(c.EnfantId, TypeDocument.Contrat, nom, pdf, $"Du {Affichage.Date(c.Debut)} au {Affichage.Date(c.Fin)}");
        db.Documents.Add(doc);
        await db.SaveChangesAsync();
        c.DocumentId = doc.Id;
        await db.SaveChangesAsync();
        await CopiesAsync(c, $"Entente de service signée : {c.Enfant.NomComplet}",
            $"L'entente de service de {c.Enfant.NomComplet} (du {Affichage.Date(c.Debut)} au {Affichage.Date(c.Fin)}) est signée par les deux parties.\n" +
            "Le contrat signé est joint. Conservez-le.", nom, pdf);
        return null;
    }

    /// <summary>Prepare une resiliation (date) : le parent doit signer la formule / Prepares a termination.</summary>
    public async Task<string?> DemanderResiliationAsync(Contrat c, DateOnly date)
    {
        if (c.Etat != EtatContrat.Signe) return "Seul un contrat signé peut être résilié.";
        c.ResilieLe = date;
        c.ModifieLe = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return null;
    }

    /// <summary>
    /// Le parent signe la formule de resiliation : contrat resilie, date de fin de l'enfant mise a jour (archive).
    /// Parent signs the termination form: contract terminated, child's end date updated (archived).
    /// </summary>
    public async Task SignerResiliationAsync(LienContrat lien, byte[] png, string signataire, string ip)
    {
        var c = lien.Contrat;
        c.SignatureResiliationFichier = await documents.EnregistrerOctetsAsync(png);
        c.ResiliationSigneeLe = DateTime.UtcNow;
        c.SignataireResiliation = signataire;
        c.IpResiliation = ip.Length > 64 ? ip[..64] : ip;
        c.Etat = EtatContrat.Resilie;
        c.ModifieLe = DateTime.UtcNow;
        lien.UtiliseLe = DateTime.UtcNow;
        var e = await db.Enfants.FirstAsync(x => x.Id == c.EnfantId);
        if (e.DateFin is null || e.DateFin > c.ResilieLe) e.DateFin = c.ResilieLe;
        e.ModifieLe = DateTime.UtcNow;
        var pdf = await PdfResiliationAsync(c);
        var nom = NomPdfResiliation(c, c.Enfant);
        var doc = await documents.EnregistrerPdfAsync(c.EnfantId, TypeDocument.Resiliation, nom, pdf, $"À compter du {Affichage.Date(c.ResilieLe)}");
        db.Documents.Add(doc);
        await db.SaveChangesAsync();
        c.DocumentResiliationId = doc.Id;
        await db.SaveChangesAsync();
        await CopiesAsync(c, $"Résiliation de l'entente de service : {c.Enfant.NomComplet}",
            $"L'entente de service de {c.Enfant.NomComplet} est résiliée à compter du {Affichage.Date(c.ResilieLe)}.\nLa formule signée est jointe.", nom, pdf);
    }

    /// <summary>Copie du PDF a la RSGE et au parent ; un echec d'envoi ne bloque pas / Copies to RSGE and parent.</summary>
    private async Task CopiesAsync(Contrat c, string sujet, string texte, string nom, byte[] pdf)
    {
        if (!courriels.EstConfigure) return;
        var p = await db.Parametres.AsNoTracking().FirstOrDefaultAsync();
        var parent = (await LienParentAsync(c))?.Parent.Courriel;
        foreach (var a in new[] { p?.CourrielRsge, parent }.Where(x => x is not null).Distinct())
        {
            var corps = a == parent && Affichage.PiedFacebook(p) is { Length: > 0 } fb ? texte + "\n\n" + fb.TrimEnd() : texte;
            try { await courriels.EnvoyerAsync(a!, sujet, corps, [(nom, pdf)]); }
            catch (Exception) { /* courriel facultatif : le PDF est dans le dossier / optional email */ }
        }
    }
}
