using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace ClimateAlert.Gateway.Proxy;

public static class TrustedProxy
{
    public static void UseExplicitProxyProtocol(this WebApplication app)
    {
        var proxies = app.Configuration.GetSection("Gateway:TrustedProxyAddresses").Get<string[]>() ?? [];
        // Do not enable forwarding with empty allowlists: that would trust every peer.
        if (proxies.Length == 0) return;
        var options = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedProto,
            ForwardLimit = 1
        };
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
        foreach (var value in proxies)
        {
            if (!IPAddress.TryParse(value, out var address) || address.Equals(IPAddress.Any)
                || address.Equals(IPAddress.IPv6Any))
                throw new InvalidOperationException("TrustedProxyAddresses must contain explicit proxy IP addresses.");
            options.KnownProxies.Add(address);
        }
        // Only the two new routes need this protocol hint; preserve legacy Phase 3 behavior.
        app.UseWhen(context => context.Request.Path.StartsWithSegments("/api/device-readings")
            || context.Request.Path.StartsWithSegments("/api/device-credentials"), branch => branch.UseForwardedHeaders(options));
    }
}
