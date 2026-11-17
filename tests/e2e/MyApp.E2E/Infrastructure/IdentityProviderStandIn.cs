using Microsoft.Playwright;

namespace MyApp.E2E.Infrastructure;

/// <summary>
/// The identity provider of a visitor's browser. The run starts the frontend with an authority on a host that
/// belongs to no tenant (<see cref="Host"/>, see <c>ApplicationManager</c>), so a real tenant is never needed
/// and never reached: this class answers that host inside the browser context, with the discovery document the
/// sign-in library asks for and a plain page for the sign-in address. "The sign-in page opens" then means the
/// browser left the application for that address, which is what the application itself does.
///
/// Test classes never call it: <c>VisitorTestBase</c> and <c>HelpSignedOutTestBase</c> install it.
/// </summary>
public static class IdentityProviderStandIn
{
    public const string Host = "login.e2e.test";
    public const string ClientId = "00000000-0000-0000-0000-00000000e2e0";
    public static string Authority => $"https://{Host}/e2e-tenant/v2.0";

    public static Task Install(IBrowserContext context) =>
        context.RouteAsync($"https://{Host}/**", route =>
        {
            var url = route.Request.Url;
            var headers = new Dictionary<string, string>
            {
                ["access-control-allow-origin"] = "*",
                ["access-control-allow-headers"] = "*",
                ["cache-control"] = "no-store"
            };

            if (route.Request.Method == "OPTIONS")
                return route.FulfillAsync(new RouteFulfillOptions { Status = 204, Headers = headers });

            if (url.Contains("openid-configuration", StringComparison.OrdinalIgnoreCase))
            {
                var root = $"https://{Host}/e2e-tenant";
                return route.FulfillAsync(new RouteFulfillOptions
                {
                    Status = 200,
                    ContentType = "application/json",
                    Headers = headers,
                    Body = $$"""
                        {
                          "issuer": "{{root}}/v2.0",
                          "authorization_endpoint": "{{root}}/oauth2/v2.0/authorize",
                          "token_endpoint": "{{root}}/oauth2/v2.0/token",
                          "end_session_endpoint": "{{root}}/oauth2/v2.0/logout",
                          "jwks_uri": "{{root}}/discovery/v2.0/keys",
                          "response_types_supported": ["code"],
                          "subject_types_supported": ["pairwise"],
                          "id_token_signing_alg_values_supported": ["RS256"]
                        }
                        """
                });
            }

            return route.FulfillAsync(new RouteFulfillOptions
            {
                Status = 200,
                ContentType = "text/html",
                Headers = headers,
                Body = "<!doctype html><title>Sign in</title><p>Sign in (E2E stand-in for the identity provider)</p>"
            });
        });
}
