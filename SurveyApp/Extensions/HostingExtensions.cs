using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using SurveyApp.Options;
using SurveyApp.Persistence;

namespace SurveyApp.Extensions;

public static class HostingExtensions
{
    /// <summary>Registers the SQL store when persistence is enabled, otherwise the explicitly non-persisting one.</summary>
    public static void AddSurveyPersistence(this IServiceCollection services, SurveyStartupSettings settings)
    {
        services.AddSingleton(TimeProvider.System);

        if (settings.SqlPersistenceEnabled)
        {
            services.AddSingleton(new SqlPersistenceSettings(settings.ConnectionString!));
            services.AddSingleton<ISurveySubmissionStore, SqlServerSubmissionStore>();
        }
        else
        {
            services.AddSingleton<ISurveySubmissionStore, NonPersistingSubmissionStore>();
        }
    }

    /// <summary>Trusted reverse-proxy support: forwarded headers only from configured proxies, plus an optional path prefix.</summary>
    public static void AddReverseProxySupport(this IServiceCollection services, ReverseProxyOptions proxy)
    {
        if (!proxy.Enabled) return;

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();
            foreach (var address in proxy.KnownProxies) options.KnownProxies.Add(IPAddress.Parse(address));
            foreach (var network in proxy.KnownNetworks)
            {
                SurveyStartupSettings.TryParseNetwork(network, out var parsed);
                options.KnownIPNetworks.Add(parsed);
            }
            options.ForwardLimit = 1;
        });
    }

    /// <summary>Must be the first middleware so redirect URIs are built from the public scheme, host and path.</summary>
    public static void UseReverseProxySupport(this WebApplication app, ReverseProxyOptions proxy)
    {
        if (!string.IsNullOrEmpty(proxy.PathBase)) app.UsePathBase(proxy.PathBase);
        if (proxy.Enabled) app.UseForwardedHeaders();
    }
}
