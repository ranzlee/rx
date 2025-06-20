using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Hx.Rx;
using Hx.Components.Layout;

namespace Hx.Components.Auth;

/// <summary>
/// THIS IS AN EXAMPLE ONLY!
/// This handler must be replaced with a real Identity Provider configured
/// for the app. This is just to demonstrate how this meta-framework integrates
/// with authentication / authorization.
/// </summary>
public class AuthHandler : IRequestHandler {

    public void MapRoutes(IEndpointRouteBuilder router) {
        router.MapPost("/auth/sign-in", SignIn).AllowAnonymous();
        router.MapGet("/auth/complete", SignInComplete).AllowAnonymous();
        router.MapPost("/auth/sign-out", SignOut).RequireAuthorization();
    }

    // Example sign in - must be replaced with OIDC identity provider
    public static async Task<IResult> SignIn(
        HttpContext httpContext,
        IAntiforgery antiforgery) {
        await antiforgery.ValidateRequestAsync(httpContext);
        var identity = new ClaimsIdentity("ExampleIdentityProvider", "Name", "Role");
        identity.AddClaim(new Claim("ExampleIdentityProvider", "RxIdentityProvider"));
        identity.AddClaim(new Claim("Name", "Test"));
        identity.AddClaim(new Claim("Role", "Admin"));
        await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
        return TypedResults.Redirect("/auth/complete");
    }

    // Signin complete - provides the client the opportunity to request a route and sync cookie state
    public static async Task<IResult> SignInComplete(
        HttpContext context,
        IRxDriver rxDriver) {
        return await rxDriver
            .With(context)
            .AddPage<App, AuthenticatedHead, Authenticated>()
            .Render();
    }

    // Sign out
    public static async Task<IResult> SignOut(
        HttpContext httpContext,
        IAntiforgery antiforgery) {
        await antiforgery.ValidateRequestAsync(httpContext);
        var props = new AuthenticationProperties { RedirectUri = "/" };
        return TypedResults.SignOut(props, [CookieAuthenticationDefaults.AuthenticationScheme]);
    }
}