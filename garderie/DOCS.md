# Garderie — module complémentaire Home Assistant

Gestion d'un service de garde en milieu familial : dossiers des enfants, présences, jours de fermeture,
fiches d'assiduité FO-0310 signées en ligne par la RSGE et les parents.
Les données restent dans le dossier `/data` du module ; **aucune donnée d'enfant n'est envoyée à Home Assistant**.

## Deux portes d'entrée

| Usage | Accès | Authentification |
|---|---|---|
| Administration (RSGE) | Menu latéral de HA → **Garderie** (Ingress) | Compte Home Assistant, limité à la liste « Utilisateurs HA autorisés » |
| Pages des parents | `https://<votre-domaine>/garderie` via Nginx Proxy Manager → port 8099 | Lien unique par courriel (7 jours, une seule utilisation) |

Le port public ne sert **que** les pages des parents : toute page d'administration y répond 404.

## Installation

1. Paramètres → Modules complémentaires → Boutique → ⋮ → **Dépôts** → ajouter l'URL du dépôt GitHub.
2. Installer **Garderie** (la première construction télécharge le SDK .NET : quelques minutes).
3. Onglet **Configuration** :
   - `utilisateurs_autorises` : noms d'utilisateur HA (ex. `rsge`). Vide = personne n'entre.
     En cas de refus, la page affiche le nom exact à ajouter.
   - `proxy_de_confiance` : IP de Nginx Proxy Manager telle que vue par le module (pour journaliser la vraie IP
     des parents). Laisser vide si inconnue.
   - `path_base_public` : `/garderie`.
   - `url_publique` : adresse complète des pages des parents, ex. `https://<votre-domaine>/garderie`
     (sert à construire les liens envoyés par courriel).
   - `smtp_utilisateur` / `smtp_mot_de_passe` : compte Gmail qui envoie les courriels et son **mot de passe
     d'application** (Compte Google → Sécurité → Validation en deux étapes → Mots de passe des applications).
     Ne jamais mettre le vrai mot de passe du compte.
4. Démarrer, puis activer **Afficher dans la barre latérale**.
5. Le compte HA de la RSGE peut être un utilisateur **non administrateur** (`panel_admin: false`).

## Fiches d'assiduité (FO-0310)

1. **Réglages** : téléverser le formulaire FO-0310 vierge (PDF à remplir du ministère, fourni par le bureau
   coordonnateur), le nom du bureau coordonnateur, le nom et le courriel de la RSGE, et un lundi qui commence une
   période de 2 semaines.
2. **Présences** : enregistrer toutes les journées de la période.
3. **Fiches** : saisir les paiements dans chaque fiche au besoin, puis cocher les enfants prêts et **signer une
   seule fois** pour toutes (ou signer une fiche à la fois depuis la fiche de l'enfant).
4. **Envoyer aux parents** : un courriel et un lien unique par parent, qui regroupe tous ses enfants. Depuis la
   fiche d'un enfant : **Copier un lien** (texto) ou **Faire signer ici** (tablette).
5. Le parent vérifie, coche l'attestation et signe, ou **signale une erreur** (la RSGE reçoit un courriel ;
   « Reprendre la fiche » annule sa signature et les liens).
6. Le PDF final (formulaire officiel rempli, champs verrouillés) est rangé chiffré dans les documents du dossier
   et envoyé par courriel à la RSGE, qui le transmet au bureau coordonnateur.

Le journal garde chaque étape (signatures, IP du parent, empreinte SHA-256 du PDF).

## Contrats (entente de service)

1. **Réglages** : vérifier les textes du contrat (préremplis), la ville de signature, la note sur les vacances,
   la **régie interne** (générée par l'application : tarif, jour de paiement et frais de retard, puis texte
   modifiable section par section ; heures, nom, fériés et mention LPC viennent des Réglages). Les PDF utilisent
   des polices libres incluses (Comic Neue, Tinos, Carlito), semblables à celles du modèle Word : rien à configurer.
2. **Fermetures** : « Ajouter les jours fériés » de l'année, plus les vacances de la RSGE (motif ex. « Relâche »).
3. **Contrats** (ou le dossier de l'enfant) : « + Nouveau », choisir les dates, puis envoyer au parent (courriel,
   lien à copier ou signature sur place).
4. Le parent vérifie et complète ses renseignements (adresse, téléphones, NAS pour le relevé 24), lit l'entente et la
   régie interne, et signe. Ses renseignements sont mis à jour dans le dossier.
5. **Contrats** : la RSGE coche les contrats signés par les parents et **contresigne une seule fois**. Le PDF est rangé
   dans le dossier et envoyé par courriel à la RSGE et au parent (copie exigée par la Loi sur la protection du
   consommateur). Le NAS figure en clair sur ce PDF.
6. **Résilier** (contrat signé) : choisir la date, puis faire signer la formule au parent ; la date de fin de l'enfant
   est mise à jour (dossier archivé).

## Relevés 24 (Revenu Québec)

1. **Réglages → Relevé 24** : téléverser le RL-24 et le RL-24.S en « PDF remplissable à l'écran » (revenuquebec.ca),
   entrer la case H (NEQ, ou NAS si pas de NEQ ; chiffré), l'adresse découpée et le signataire. Boutons « Aperçu ».
   Les formulaires de Revenu Québec ne sont jamais inclus dans le module.
2. **Relevés 24** (onglet du haut) : une ligne par parent payeur, avec les jours payés (présences, absences et
   fermetures, selon les présences et l'horaire) et le montant proposé (jours × tarif de la régie).
3. **Préparer** : corriger les sommes réellement payées (C) et les frais non admissibles (D), vérifier l'adresse,
   « Aperçu », puis « Produire le relevé ». Le numéro suit celui du formulaire (même calcul) ; le PDF va dans le
   dossier de l'enfant. Produire de nouveau crée un relevé modifié (code A).
4. **Remise** : par courriel seulement si le parent a consenti par écrit (case à la signature du contrat, ou inscrite
   dans la fiche du parent) ; sinon, remettre la copie 2 (page 2) sur papier.
5. **Sommaire 24** : l'imprimer, le signer et le poster avec la copie 1 (page 1) de chaque relevé, au plus tard le
   dernier jour de février.

Le formulaire protégé de Revenu Québec est rempli sans être modifié : seules les valeurs des champs sont ajoutées
(comme Acrobat Reader). Ouvrir les PDF dans Acrobat Reader pour imprimer.

## Nginx Proxy Manager (pages des parents)

Dans l'hôte proxy existant (votre domaine), onglet **Custom locations** :
- Location : `/garderie`
- Forward : `http` · IP de HA · port `8099`

Ne pas réécrire le chemin : l'appli retire elle-même `/garderie`.

## Sauvegardes et sécurité

- Le module utilise une **sauvegarde à froid** (`backup: cold`) : il est arrêté quelques secondes pendant la
  sauvegarde HA pour que la base SQLite soit cohérente.
- `/data` contient la base, les documents chiffrés **et la clé de chiffrement** (`/data/keys`). Une sauvegarde HA
  contient donc tout : **activez le chiffrement des sauvegardes HA** et protégez la clé de sauvegarde.
- NAS des parents, NAM des enfants et documents téléversés sont chiffrés (AES-256-GCM).
- Chaque consultation de dossier, affichage de NAS, téléchargement de document et modification est inscrit au
  **Journal** (utilisateur HA, IP, date).
- Ne jamais copier ces données dans `/config/www` (public sans connexion).

## Codes de présence

Ceux de la fiche d'assiduité FO-0310 : P, P ½, A, A ½, R, R ½, F, F ½, AN, AD, L, S, S ½.
L'écran **Présences** montre la semaine, 2 cases par jour (matin, après-midi) et par enfant, comme la fiche.
Il propose **P** dans les 2 cases du lundi au vendredi et **F** les jours de fermeture (F ½ / P ½ pour une
demi-journée), en pointillé ; rien n'est enregistré avant **Enregistrer la semaine**. Les jours affichés
dépendent de la **date de début de fréquentation** de l'enfant (Modifier l'identité), pas de l'horaire.

## Photos

Touchez l'avatar rond d'un enfant, d'un parent ou d'une personne autorisée pour prendre ou choisir une photo.
Elle est réduite dans le navigateur (640 px, sans position GPS) puis chiffrée sur le serveur.
