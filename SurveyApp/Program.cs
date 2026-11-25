using SurveyApp.Components;
using SurveyApp.Extensions;
using SurveyApp.Options;
using SurveyApp.Security;
using SurveyApp.Services;

var builder = WebApplication.CreateBuilder(args);

// Validated once: unknown/obsolete settings or missing provider settings stop the app here with a clear message.
var settings = SurveyStartupSettings.Load(builder.Configuration);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddOptions<SurveyOptions>()
    .BindConfiguration(SurveyOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<FeatureFlagsOptions>()
    .BindConfiguration(FeatureFlagsOptions.SectionName);

builder.Services.AddSingleton<ISurveyConfigurationService, SurveyConfigurationService>();
builder.Services.AddSingleton<ISurveySubmissionService, SurveySubmissionService>();
builder.Services.AddScoped<ToastService>();

builder.AddSurveyAuthentication(settings);
builder.Services.AddSurveyPersistence(settings);
builder.Services.AddReverseProxySupport(settings.ReverseProxy);

var app = builder.Build();

// Fail fast with a clear message if the initial survey configuration is missing or invalid.
app.Services.GetRequiredService<ISurveyConfigurationService>();

app.UseReverseProxySupport(settings.ReverseProxy);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseSurveyAuthentication(settings);

app.UseAntiforgery();

// Static files (CSS, JS, logo) carry no data and must load on the public error / signed-out pages too.
app.MapStaticAssets().AllowAnonymous();
app.MapSurveyAuthenticationEndpoints(settings);
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

// Exposed for integration tests.
public partial class Program;
