using Garderie.Web.Data;
using Garderie.Web.Services;

namespace Garderie.Web.Pages.Presences;

/// <summary>Modele d'une case matin / apres-midi / Half-day cell view model.</summary>
public sealed record CelluleVue(int Index, int EnfantId, DemiJournee Demi, CaseDemi Case, string Libelle);
