using System.Globalization;
using Garderie.Web.Configuration;
using Garderie.Web.Data;
using Garderie.Web.Infrastructure;
using Garderie.Web.Securite;
using Garderie.Web.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Options du module HA (/data/options.json) / HA add-on options
var dataDir = builder.Configuration["Garderie:DataDir"] ?? "/data";
builder.Configuration.AddJsonFile(Path.Combine(Path.GetFullPath(dataDir), "options.json"), optional: true, reloadOnChange: false);

var options = GarderieOptions.Charger(builder.Configuration);
options.DataDir = Path.GetFullPath(options.DataDir);
Directory.CreateDirectory(options.DataDir);
builder.Services.AddSingleton(options);

// Deux ports : public (parents, via NPM) et Ingress (administration, via HA)
// Two ports: public (parents, via NPM) and Ingress (admin, via HA)
builder.WebHost.ConfigureKestrel(k =>
{
    k.ListenAnyIP(options.PortPublic);
    k.ListenAnyIP(options.PortIngress);
    k.Limits.MaxRequestBodySize = 20 * 1024 * 1024;
    k.AddServerHeader = false;
});
builder.Services.Configure<FormOptions>(f => f.MultipartBodyLengthLimit = 16 * 1024 * 1024);

builder.Services.AddDbContext<GarderieDb>(o => o.UseSqlite($"Data Source={options.FichierBase}"));
builder.Services.AddSingleton<Chiffreur>();
builder.Services.AddScoped<Journal>();
builder.Services.AddScoped<PresencesService>();
builder.Services.AddScoped<DocumentsService>();
builder.Services.AddHttpContextAccessor();

builder.Services.AddDataProtection()
    .SetApplicationName("garderie")
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(options.DossierCles, "dp")));

builder.Services.AddAntiforgery(a =>
{
    a.Cookie.Name = "garderie.af";
    a.Cookie.SameSite = SameSiteMode.Strict;
    a.Cookie.HttpOnly = true;
    a.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

builder.Services.AddAuthentication(IngressAuthHandler.Schema)
    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, IngressAuthHandler>(IngressAuthHandler.Schema, null);
builder.Services.AddAuthorization();

builder.Services.AddRazorPages(o =>
{
    // Tout exige l'Ingress HA, sauf les pages publiques / Everything requires HA Ingress except public pages
    o.Conventions.AuthorizeFolder("/");
    o.Conventions.AllowAnonymousToFolder("/Public");
    o.Conventions.AllowAnonymousToPage("/Error");
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<GarderieDb>().Database.Migrate();
    scope.ServiceProvider.GetRequiredService<Chiffreur>(); // cree la cle au besoin / creates key if needed
}
if (options.UtilisateursAutorises.Count == 0)
    app.Logger.LogWarning("Aucun utilisateur HA autorise : ajoutez-en dans la configuration du module (utilisateurs_autorises). / No allowed HA user configured.");

app.UseMiddleware<ZonesMiddleware>();
app.UseMiddleware<EnTetesSecuriteMiddleware>();
if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

var fr = new CultureInfo("fr-CA");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(fr),
    SupportedCultures = [fr],
    SupportedUICultures = [fr],
});

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/sante", () => Results.Text("ok")).AllowAnonymous();
app.MapRazorPages();

app.Run();

public partial class Program;
