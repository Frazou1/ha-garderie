namespace Garderie.Web.Services;

/// <summary>
/// Textes par defaut de l'entente de service (modele 2018 de la RSGE), modifiables dans Reglages.
/// Aucune donnee personnelle : {garderie} et {rsge} sont remplaces par les Reglages.
/// Default service-agreement texts (editable in Settings). No personal data: placeholders filled from Settings.
/// Chaque ligne qui commence par « - » devient un point de liste / Lines starting with "-" become bullet points.
/// </summary>
public static class TextesContrat
{
    public const string ObligationsRsge = """
        - Respecter le contrat.
        - Présenter toute personne choisie pour l'assister ou la remplacer.
        - Établir une relation de confiance.
        - Ne donner aucun médicament non prescrit et sans autorisation écrite du parent.
        - Fournir un repas et deux collations nutritives et informer les parents du contenu des repas et des collations dispensés à l'enfant.
        - Faire rapport aux parents de tout accident ou incident survenu à son enfant durant la journée.
        - S'abstenir de toutes sanctions corporelles.
        - Respecter la confidentialité des informations personnelles fournies par le parent et inscrites au dossier de l'enfant.
        - Maintenir les locaux, l'équipement, le mobilier et le matériel propres et en bon état.
        - Informer le parent au moins un mois à l'avance de sa ou ses périodes de vacances.
        - Émettre une fois l'an un reçu fédéral pour fins d'impôt couvrant le total des frais de garde payés par le parent ou la personne responsable de l'enfant (voir régie interne).
        """;

    public const string ObligationsParent = """
        - Respecter le contrat et les règlements de la régie interne.
        - En cas de fermeture imprévue, le parent est responsable de choisir un autre mode de garde (voir la politique de la régie interne).
        - Compléter au moment requis les fiches personnelles de l'enfant et s'assurer d'en faire la mise à jour lorsque nécessaire.
        - Fournir à l'éducatrice les médicaments et signer les autorisations requises.
        - Fournir des vêtements de rechange (tel que mentionné dans la régie interne) et, s'il y a lieu, les aliments ou autres produits spéciaux.
        - Accompagner l'enfant et le reprendre au service de garde aux heures convenues, selon l'horaire et le contrat.
        - Le parent doit venir chercher l'enfant sur demande de l'éducatrice dans les cas suivants : fièvre (38,5 et plus), diarrhée, vomissements, maladie contagieuse. Tout enfant présentant ces symptômes peut être refusé par l'éducatrice.
        - Le parent s'engage à présenter une autorisation médicale au retour de son enfant si nécessaire, selon la régie interne.
        - Prévenir l'éducatrice de tout événement pouvant avoir des conséquences sur la journée de garde : fièvre, chute, changement émotif, fatigue, etc.
        - Informer l'éducatrice au moins un mois à l'avance de sa ou ses périodes de vacances.
        """;

    public const string MentionLpc = """
        - Le consommateur (parent ou personne responsable de l'enfant) peut résilier le contrat à tout moment en signant la partie ci-dessous du présent document ou en rédigeant un autre avis écrit.
        - Le contrat est résilié, sans autre formalité, dès la remise de la formule ou de l'avis.
        - Si le consommateur résilie le présent contrat avant que le commerçant (éducatrice-propriétaire) n'ait commencé à exécuter son obligation principale (avant le premier jour de garde), le consommateur n'a aucuns frais de pénalité à payer.
        - Si le consommateur résilie le contrat après que le commerçant a commencé à exécuter son obligation principale, le consommateur n'a qu'à payer : le prix des services qui lui ont été fournis, calculé au taux stipulé dans le contrat, en plus de 50 $ ou une somme d'au plus 10 % du prix des services qui ne lui ont pas été fournis.
        - Dans les dix jours qui suivent la résiliation du contrat, le commerçant doit restituer au consommateur la somme qu'il lui doit et vice versa.
        Le consommateur aura avantage à consulter les articles 190 à 196 de la Loi sur la protection du consommateur et, au besoin, à communiquer avec l'Office de la protection du consommateur.
        Malgré la mention exigée par la Loi sur la protection du consommateur, un avis de deux semaines serait grandement apprécié pour permettre à l'une ou l'autre des parties de s'organiser.
        """;

    public const string Attestation =
        "J'atteste avoir lu et compris l'entente de service (contrat) ainsi que la régie interne du service de garde {garderie} " +
        "appartenant à {rsge}, et je m'engage à respecter les clauses et règlements qui y sont mentionnés durant tout le séjour de mon enfant.";

    public const string NonRespect =
        "Dans le cas du non-respect d'une ou plusieurs clauses ou règlements de cette entente, je comprends que je pourrais devoir " +
        "trouver un autre service de garde avant le prochain renouvellement ou, dans une situation exceptionnelle, voir mon contrat prendre fin immédiatement.";

    /// <summary>Remplace {garderie} et {rsge} / Replaces the placeholders.</summary>
    public static string Remplir(string texte, string? garderie, string? rsge) =>
        texte.Replace("{garderie}", garderie ?? "").Replace("{rsge}", rsge ?? "");
}

/// <summary>Jours feries du Quebec (fermetures habituelles d'un service de garde) / Quebec statutory holidays.</summary>
public static class Feries
{
    public static IReadOnlyList<(DateOnly Date, string Nom)> Calculer(int annee)
    {
        var paques = Paques(annee);
        DateOnly Lundi(int mois, int rang) // rang-ieme lundi du mois / n-th Monday of the month
        {
            var d = new DateOnly(annee, mois, 1);
            while (d.DayOfWeek != DayOfWeek.Monday) d = d.AddDays(1);
            return d.AddDays(7 * (rang - 1));
        }
        var patriotes = new DateOnly(annee, 5, 24);
        while (patriotes.DayOfWeek != DayOfWeek.Monday) patriotes = patriotes.AddDays(-1);
        return
        [
            (new DateOnly(annee, 1, 1), "Jour de l'an"),
            (new DateOnly(annee, 1, 2), "Le 2 janvier"),
            (paques.AddDays(-2), "Vendredi saint"),
            (paques.AddDays(1), "Lundi de Pâques"),
            (patriotes, "Fête des patriotes"),
            (new DateOnly(annee, 6, 24), "Fête nationale"),
            (new DateOnly(annee, 7, 1), "Fête du Canada"),
            (Lundi(9, 1), "Fête du travail"),
            (Lundi(10, 2), "Action de grâce"),
            (new DateOnly(annee, 12, 24), "Veille de Noël"),
            (new DateOnly(annee, 12, 25), "Jour de Noël"),
            (new DateOnly(annee, 12, 26), "Lendemain de Noël"),
        ];
    }

    /// <summary>Feries decrits pour la regie interne (meme ordre) / Holidays described for the internal rules.</summary>
    public static readonly IReadOnlyList<string> Descriptions =
    [
        "Jour de l'An (1er janvier)", "2 janvier", "Vendredi saint", "Lundi de Pâques",
        "Fête des patriotes (lundi qui précède le 25 mai)", "Fête nationale (24 juin)", "Fête du Canada (1er juillet)",
        "Fête du travail (1er lundi de septembre)", "Action de grâce (2e lundi d'octobre)", "Veille de Noël (24 décembre)",
        "Jour de Noël (25 décembre)", "Lendemain de Noël (26 décembre)",
    ];

    /// <summary>Dimanche de Paques (calendrier gregorien) / Easter Sunday (Gregorian).</summary>
    public static DateOnly Paques(int a)
    {
        int b = a % 19, c = a / 100, d = a % 100, e = c / 4, f = c % 4, g = (c + 8) / 25, h = (c - g + 1) / 3;
        int i = (19 * b + c - e - h + 15) % 30, k = d / 4, l = d % 4, m = (32 + 2 * f + 2 * k - i - l) % 7;
        int n = (b + 11 * i - 22 * m) / 451, mois = (i + m - 7 * n + 114) / 31, jour = (i + m - 7 * n + 114) % 31 + 1;
        return new DateOnly(a, mois, jour);
    }
}
