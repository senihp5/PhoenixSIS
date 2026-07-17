using Phoenix.Core.Features;
using Phoenix.Infrastructure.Features;
using Phoenix.Web.Components;

var builder = WebApplication.CreateBuilder(args);

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
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
