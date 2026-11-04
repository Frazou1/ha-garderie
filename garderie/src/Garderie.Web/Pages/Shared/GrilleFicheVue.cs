using Garderie.Web.Data;

namespace Garderie.Web.Pages.Shared;

/// <summary>Grille en lecture seule des 2 semaines d'une fiche / Read-only 2-week grid of a sheet.</summary>
public sealed record GrilleFicheVue(DateOnly Debut, IReadOnlyDictionary<(DateOnly, DemiJournee), string> Codes, Enfant Enfant);
