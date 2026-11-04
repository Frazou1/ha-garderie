namespace Garderie.Web.Pages.Shared;

/// <summary>
/// Avatar cliquable qui envoie une photo au gestionnaire du dossier (Photo, PhotoParent, PhotoContact).
/// Clickable avatar posting a photo to the record handler.
/// </summary>
public sealed record PhotoModifiableVue(AvatarVue Avatar, int EnfantId, string Handler, string Libelle, string? Cle = null, int? Valeur = null);
