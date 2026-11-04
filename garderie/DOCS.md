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
3. **Fiches** → l'enfant : saisir le paiement, vérifier les codes, **signer** au doigt.
4. **Envoyer au parent** (courriel avec lien unique), **Copier un lien** (texto) ou **Faire signer ici** (tablette).
5. Le parent vérifie, coche l'attestation et signe, ou **signale une erreur** (la RSGE reçoit un courriel ;
   « Reprendre la fiche » annule sa signature et les liens).
6. Le PDF final (formulaire officiel rempli, champs verrouillés) est rangé chiffré dans les documents du dossier
   et envoyé par courriel à la RSGE, qui le transmet au bureau coordonnateur.

Le journal garde chaque étape (signatures, IP du parent, empreinte SHA-256 du PDF).

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
