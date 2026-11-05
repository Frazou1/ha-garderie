# Changelog

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
