# Changelog

## 0.16.0
- Fiche du parent : nouveau champ « Cellulaire papa (autre parent) », affiché dans le dossier de l'enfant.

## 0.15.0
- Signature du parent (contrat, résiliation, fiche d'assiduité) : bouton « Importer une image » à côté de « Effacer ».
  Le parent peut prendre en photo sa signature (encre foncée sur papier blanc) : le fond est retiré, le trait est
  recadré dans le cadre, et il peut encore l'effacer avant de signer. Le journal indique « signature importée (image) ».
- Contrat signé : bouton « Envoyer une copie à [nom] » pour chaque autre parent de l'enfant qui a un courriel
  (deuxième parent). Seuls les parents liés à l'enfant peuvent la recevoir ; l'envoi est noté au journal.

## 0.14.0
- Onglet « Aide » : la vidéo du tour de la plateforme, lisible sur place (iPad compris) ou dans un nouvel onglet.
  La vidéo se téléverse dans Réglages → Vidéo d'aide (MP4, 15 Mo maximum) ; elle reste dans les données du module,
  jamais dans le dépôt.

## 0.13.2
- Bouton « Archiver » : l'encadré s'ouvre juste sous le bouton (il s'affichait au bord droit de l'écran).

## 0.13.1
- Dossier de l'enfant : bouton « Archiver » (à côté de « Modifier l'identité ») qui demande le dernier jour de
  fréquentation (hier par défaut). Le lendemain de ce jour, le dossier passe aux anciens, en lecture seule.

## 0.13.0
- Contrat complété selon le modèle du bureau coordonnateur (dans la mise en page du modèle Word de la RSGE) :
  numéro d'inscription au portail (fiche de l'enfant), courriels du parent et de l'éducatrice, description des services
  fournis et heures des collations et du dîner, horaire du service de garde, clauses de fermeture (avis des vacances,
  frais), fréquentation de l'enfant (jours de son horaire, aux heures d'ouverture) et date de début, modalités de
  paiement (tarif, jour, modes, montant de chaque versement, total de l'entente), frais de retard, résiliation par
  l'éducatrice et par le parent, date et lieu de la signature de l'éducatrice.
- Mention de la Loi sur la protection du consommateur : libellé officiel (aussi repris dans la régie interne).
- Réglages : nouvelles sections modifiables du contrat ; modes de paiement dans la régie interne.
- Les contrats déjà signés gardent leur contenu d'origine.

## 0.12.3
- Dossier de l'enfant : le PDF d'un relevé 24 n'a plus de bouton « Supprimer » (bouton « Relevés 24 » à la place) ;
  un relevé se supprime dans l'onglet Relevés 24, qui ajuste aussi le sommaire et la numérotation.
- Dossier de l'enfant : les archives signées (contrat, résiliation, fiche d'assiduité) ne se suppriment plus seules
  (badge « archive signée ») ; sans elles, « Envoyer une copie » partait sans pièce jointe.

## 0.12.2
- Relevés 24 : bouton « Supprimer » pour un relevé produit par erreur (jamais remis ni posté) ; le relevé précédent
  redevient en vigueur et la numérotation reprend après le dernier relevé restant.
- Supprimer le dossier d'un enfant retire aussi ses relevés 24.

## 0.12.1
- Réglages → « La garderie » : adresse découpée comme sur les formulaires de Revenu Québec (app., numéro, rue, ville,
  province, code postal). Le relevé 24 et le sommaire la reprennent ; le contrat et la régie utilisent la ligne
  complète. L'ancienne adresse sur une ligne est découpée automatiquement à l'ouverture (à vérifier).
- Réglages → Relevé 24 : l'adresse en double est retirée.
- Réglages : une saisie refusée affiche maintenant son erreur au lieu de ne rien enregistrer.

## 0.12.0
- Relevés 24 : nouvel onglet « Relevés 24 ». Un relevé par parent payeur, jours payés et montants proposés à partir des
  présences, de l'horaire et du tarif, corrigés avant de produire. Remplissage du RL-24 officiel de Revenu Québec
  (protégé, non modifié), numéro calculé comme le formulaire, PDF classé dans le dossier, relevé modifié (code A),
  sommaire 24 rempli à signer et poster.
- Réglages → Relevé 24 : formulaires RL-24 et RL-24.S, case H chiffrée (NEQ ou NAS), adresse, signataire, aperçus.
- Consentement au relevé 24 par courriel (exigé par Revenu Québec) : case à la signature du contrat, et dans la fiche
  du parent (inscrire ou retirer). Envoi par courriel un à un ou groupé ; les autres sont à remettre sur papier.

## 0.11.0
- Réglages → « La garderie » : lien du groupe Facebook des parents, ajouté aux courriels envoyés aux parents
  (contrat et régie, fiches, résiliation, copies) et en haut de la régie interne.
- Réglages → « Logo » : image PNG ou JPG affichée en haut de l'application (à la place du petit bateau) et des
  pages des parents. Bouton « Retirer » pour revenir au bateau.

## 0.10.0
- Réglages : bouton « Aperçu du modèle de contrat » (textes, fermetures de la prochaine année, champs de l'enfant
  et du parent à remplir).
- Régie interne : « Annexe concernant la Régie interne » (Covid-19) ajoutée à la fin, sur sa propre page, et
  modifiable dans Réglages comme les autres sections. Elle est donc jointe au courriel du contrat.
- PDF de la régie : une liste reste sur la même page que la ligne qui l'annonce.

## 0.9.0
- Polices incluses dans le module, rien à configurer : Comic Neue (comme Comic Sans), Tinos (mesures de Times New
  Roman) et Carlito (mesures de Calibri), sous licence libre SIL OFL. La section « Polices du contrat » de
  Réglages est retirée (les polices Microsoft ne peuvent pas être redistribuées).

## 0.8.0
- Régie interne générée par l'application, sans document à déposer : 24 sections modifiables dans Réglages
  (regroupées par partie, texte par défaut = régie de la RSGE). Vider une section la remet par défaut, « (aucune) »
  la retire. Bouton « Afficher le PDF ».
- Valeurs tirées des Réglages : nom de la RSGE, heures d'ouverture et de fermeture (retard), tarif par jour, jour de
  paiement, frais de retard, liste et nombre des fériés, mention de la Loi sur la protection du consommateur (contrat).
- « Veille de Noël » ajoutée aux jours fériés (12 jours, comme la régie).
- Les contrats ne sont plus bloqués : la régie existe toujours. L'import Word de la 0.7.0 est retiré.

## 0.7.0
- Régie interne saisie dans Réglages : « Importer depuis Word (.docx) » une fois (titres, sous-titres, gras,
  listes, lignes centrées), puis texte modifiable ; le PDF (Comic Sans, comme le Word, pages numérotées) est
  généré automatiquement, joint au courriel du contrat et offert sur la page de signature. Aperçu dans Réglages.
- Le texte de la régie lue par le parent est figé avec son contrat.
- Le téléversement d'un PDF de régie est retiré (remplacé par le texte).

## 0.6.0
- Contrat PDF fidèle au modèle Word de la RSGE : titres, sections, valeurs soulignées, fériés « ( X ) jj/mm/aaaa »
  sur deux colonnes, vacances « *** », clauses en un paragraphe Times 9 gras, mêmes polices.
- Réglages → « Polices du contrat » : téléverser Comic Sans MS, Times New Roman, Calibri, Elephant (polices
  Microsoft, jamais incluses dans le module) ; sinon police de remplacement.
- Régie interne : carte bien visible sur la page du parent (lire / télécharger), jointe au courriel du contrat,
  et l'envoi d'un contrat est bloqué tant qu'elle n'est pas téléversée.
- Fériés nommés comme le modèle (« Le 2 janvier », « Fête des patriotes »…).

## 0.5.2
- Correctif : « Envoyer une copie au parent » / « M'envoyer une copie » étaient refusés (jeton antiforgery manquant).
  Test ajouté : chaque formulaire POST des pages principales porte son jeton.

## 0.5.1
- Pages des parents : la case à cocher est dans un encadré bien visible (« J'ai lu et j'accepte… » /
  « Je confirme »), sous le texte complet de l'attestation.
- PDF : boutons « Afficher » (dans le navigateur) et « Télécharger » pour les documents du dossier, les fiches et
  les contrats (et la résiliation).
- Renvoyer : « Renvoyer le courriel » quand un lien est déjà en cours ; « Envoyer une copie au parent » et
  « M'envoyer une copie » pour une fiche ou un contrat signé.

## 0.5.0
- Contrats (entente de service) : création par enfant, lien unique au parent (courriel, lien à copier ou
  signature sur place) ; le parent vérifie et complète ses renseignements (adresse, téléphones, lien, NAS,
  vacances), lit l'entente et la régie interne, puis signe ; la RSGE contresigne (une à une ou groupée).
  PDF mis en page comme le modèle, rangé chiffré dans le dossier, copie par courriel à la RSGE et au parent.
- Résiliation : formule signée par le parent ; contrat résilié et date de fin de l'enfant mise à jour.
- Réglages : textes du contrat modifiables (préremplis), ville de signature, note sur les vacances, régie interne.
- Fermetures : bouton « Ajouter les jours fériés du Québec » ; les fermetures de la période vont au contrat.

## 0.4.0
- Signature groupée : sur la page Fiches, la RSGE coche les enfants prêts et signe une seule fois ;
  « Envoyer aux parents » envoie un seul courriel et un seul lien par parent, pour tous ses enfants.
- Le parent signe une seule fois pour toutes les fiches de ses enfants ; chaque enfant reçoit son PDF dans son
  dossier et la RSGE reçoit un courriel avec tous les PDF.
- Page du parent : la section « Contribution réduite » n'apparaît que si un paiement est saisi.
- Inclut 0.3.2 (semaines signées verrouillées dans Présences).

## 0.3.2
- Présences : une semaine dont la fiche est signée affiche « ✓ Fiche signée » ou « Fiche en attente du parent »
  (lien vers la fiche) et passe en lecture seule (aussi refusé côté serveur). « Reprendre la fiche » la
  rend de nouveau modifiable tant que le parent n'a pas signé.

## 0.3.1
- Signature du parent : le bouton se désactive au premier toucher ; un lien déjà utilisé pour signer affiche
  « Merci » au lieu de « Lien invalide » (double toucher).

## 0.3.0
- Fiches d'assiduité aux 2 semaines : formulaire officiel FO-0310 rempli (téléversé dans Réglages), paiements,
  signature de la RSGE au doigt, lien unique au parent par courriel (7 jours, usage unique), lien à copier ou
  signature sur place, « Signaler une erreur », PDF final verrouillé rangé dans le dossier et envoyé à la RSGE.
- Courriels par Gmail (mot de passe d'application dans les options du module).
- Réglages : période de référence, formulaire FO-0310, test du courriel ; onglet Fiches.
- Montants acceptés avec point ou virgule ; « 1er » dans les dates.

## 0.2.1
- Présences : retour à 2 cases par jour (matin / après-midi), comme sur la fiche FO-0310 ; P dans les 2 cases
  par défaut. Fermeture d'une demi-journée : F ½ dans la case fermée, P ½ dans l'autre. Migration : le code
  d'un jour est recopié dans la case de l'après-midi.
- Un jour où l'enfant n'est pas encore inscrit (« · ») mène à sa date de début de fréquentation.
- Horaire : précise qu'il ne change pas les présences (seule la date de début de fréquentation compte).

## 0.2.0
- Présences à la semaine : une case par jour et par enfant (comme le FO-0310), P proposé du lundi au vendredi,
  F les jours de fermeture. Les présences ne sont plus séparées matin / après-midi.
- Photos de l'enfant, des parents et des personnes autorisées : touchez l'avatar (appareil photo ou galerie),
  photo réduite dans le navigateur puis chiffrée.
- Nouvelle apparence (douce et chaleureuse), onglets en bas de l'écran sur téléphone.

## 0.1.2
- Dossier archivé (date de fin passée) : badge « Ancien », lecture seule (refusée aussi côté serveur),
  documents et NAS toujours consultables pour l'impôt. Bouton « Réactiver » (retire la date de fin, journalisé).

## 0.1.1
- Suppression d'un dossier d'enfant (prénom à retaper) : contacts, santé, horaire, présences,
  documents chiffrés et parents sans autre enfant. L'action reste inscrite au journal.

## 0.1.0
- Première version : dossiers des enfants (identité, parents, contacts, santé, horaire, documents chiffrés),
  présences du jour, jours de fermeture, paramètres, journal des accès.
- Administration par l'Ingress de Home Assistant ; port public réservé aux pages des parents.
