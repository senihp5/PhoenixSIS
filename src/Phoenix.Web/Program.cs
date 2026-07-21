using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using Phoenix.Core.Features;
using Phoenix.Infrastructure.Features;
using Phoenix.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// Entra ID authentication (PHX-1.5).
// RoleClaimType = "groups" makes group Object IDs usable with RequireRole,
// matching the CMS group-GUID RBA model.
builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(options =>
    {
        builder.Configuration.GetSection("AzureAd").Bind(options);
        options.TokenValidationParameters.RoleClaimType = "groups";
    });

// Sign-in/sign-out endpoints (MicrosoftIdentity/Account/...).
builder.Services.AddControllersWithViews()
    .AddMicrosoftIdentityUI();

builder.Services.AddAuthorization(options =>
{
    // Admin = member of the PhoenixSIS.Admin Entra security group.
    options.AddPolicy("RequireAdmin", policy =>
        policy.RequireRole(
            builder.Configuration["AzureAd:Groups:Admin"]
                ?? throw new InvalidOperationException(
                    "AzureAd:Groups:Admin is not configured.")));

    // Require a signed-in user everywhere by default; endpoints must
    // opt out explicitly with [AllowAnonymous].
    options.FallbackPolicy = options.DefaultPolicy;
});

builder.Services.AddCascadingAuthenticationState();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Phoenix SIS Feature Flag Service (PHX-1.2)
builder.Services.AddSingleton<IFeatureFlagService, FeatureFlagService>();

var app = builder.Build();

// Resolve the feature flag service at startup so all feature states
// are logged immediately, not on first use (PHX-1.2 DoD verification).
app.Services.GetRequiredService<IFeatureFlagService>();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapControllers();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
