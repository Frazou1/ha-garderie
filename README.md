# ha-garderie

Dépôt de module complémentaire Home Assistant : **Garderie**, gestion d'un service de garde en milieu familial
(Québec) — dossiers des enfants, présences, fiche d'assiduité FO-0310 (à venir), contrats (à venir).

Ce dépôt ne contient **que le code** : aucune donnée, aucun secret. Les données vivent dans `/data` du module.

## Ajouter à Home Assistant

Paramètres → Modules complémentaires → Boutique → ⋮ → Dépôts → `https://github.com/Frazou1/ha-garderie`

Documentation du module : [garderie/DOCS.md](garderie/DOCS.md)

## Développement

Prérequis : SDK .NET 10.

```bash
dotnet test
```

```bash
cd garderie/src/Garderie.Web && ASPNETCORE_ENVIRONMENT=Development dotnet run --no-launch-profile
```

- Administration (Ingress simulé, utilisateur `dev`) : http://localhost:5090/api/hassio_ingress/dev/
- Port public : http://localhost:5080/garderie/
- Données locales : `garderie/src/Garderie.Web/data-dev/` (ignoré par git)

Migration EF : `dotnet ef migrations add <Nom> --project garderie/src/Garderie.Web --output-dir Data/Migrations`
(outil local : `dotnet tool restore`).

## Licence

MIT. Bibliothèques : ASP.NET Core / EF Core / SQLite (MIT), Bootstrap (MIT), xUnit (Apache 2.0).
