namespace Garderie.Web.Pages.Shared;

/// <summary>Avatar rond : photo si disponible, sinon initiales / Round avatar: photo or initials.</summary>
/// <param name="Taille">Classe de taille : av-sm, av-md, av-lg / Size class.</param>
public sealed record AvatarVue(string Nom, string Initiales, int Graine, string? Url, string Taille = "av-md");
