using Garderie.Web.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Garderie.Web.Pages;

/// <summary>Aide : video du tour de la plateforme (Reglages) / Help: platform tour video.</summary>
public class AideModel(GarderieOptions options) : PageModel
{
    public long Version => System.IO.File.Exists(options.FichierVideoAide)
        ? System.IO.File.GetLastWriteTimeUtc(options.FichierVideoAide).Ticks : 0;

    public void OnGet() { }

    /// <summary>Video servie par plages (lecture et avance sur iPad) / Served with range support.</summary>
    public IActionResult OnGetVideo()
    {
        if (!System.IO.File.Exists(options.FichierVideoAide)) return NotFound();
        Response.Headers.CacheControl = "private, max-age=86400";   // l'adresse change avec la video / URL changes with the video
        return new PhysicalFileResult(options.FichierVideoAide, "video/mp4") { EnableRangeProcessing = true };
    }

    /// <summary>Signature MP4 : boite « ftyp » au debut / MP4 signature: leading "ftyp" box.</summary>
    public static bool EstMp4(byte[] o) =>
        o.Length > 12 && o[4] == (byte)'f' && o[5] == (byte)'t' && o[6] == (byte)'y' && o[7] == (byte)'p';
}
