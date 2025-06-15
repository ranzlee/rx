using System.Reflection;
using Microsoft.AspNetCore.ResponseCompression;
using FluentValidation;
using Hx.Components.Error;
using Hx.Components.Layout;
using Hx.Components.Rx;
using Hx.Rx;

// Asset fingerprinting and pre-compression is part of .NET 9 with the new MapStaticAssets middleware, however 
// this middleware is not working with Minimal APIs and RazorComponentResults. 
// -> https://github.com/dotnet/aspnetcore/issues/58937
// RazorX uses the older UseStaticFiles which ETags the static assets, but in addition the 
// build revision number is used by the ScriptHelper to fingerprint assets.
[assembly: AssemblyVersion("1.0.0.*")]

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;

// Add the scoped script helper for cache busting
services.AddScriptHelper();

services.AddAntiforgery();

services.AddHttpContextAccessor();

// Add fragment rendering support
services.AddRxDriver();

// Add services for <AuthorizeView>
//services.AddCascadingAuthenticationState();
//services.AddScoped<AuthenticationStateProvider, ServerAuthenticationStateProvider>();

// Add response compression - remove if using server level compression (e.g., nginx)
if (!builder.Environment.IsDevelopment()) {
    services.AddResponseCompression(options => {
        options.EnableForHttps = true;
        options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat([
            "application/javascript",
            "application/json",
            "text/css",
            "text/html"
        ]);
    });
}

// Add API standardized problem details
services.AddProblemDetails();

// Add HxTriggers for sending event triggers from the server to client 
services.AddHxTriggers();

// Add FluentValidation and validation services
services.AddScoped<ValidationContext>();
services.AddValidatorsFromAssemblyContaining<Program>();

// Add auth - this is just for example purposes. You will need to 
// configure your own OIDC identity provider or ASP.NET Core Identity
builder.Services.AddAuthentication().AddCookie();
builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment()) {
    // Use the default exception handler
    app.UseExceptionHandler(handler => {
        handler.Run(context => {
            // The razorx.js error handler will handle async error redirects to /error
            if (!context.Request.IsHxRequest()) {
                // Page requests will redirect to /error
                context.Response.Redirect("/error?code=500");
            }
            return Task.CompletedTask;
        });
    });
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
    // Use compression - applied early for static file compression
    app.UseResponseCompression();
}

// Use HTTPS only
app.UseHttpsRedirection();

// Use static files served from wwwroot - applied early for short-circuiting the request pipeline
app.UseStaticFiles(new StaticFileOptions {
    OnPrepareResponse = ctx => {
        // 7 day freshness
        ctx.Context.Response.Headers.Append("Cache-Control", "public, max-age=604800");
    }
});

// Use auth - must be before antiforgery
app.UseAuthentication();
app.UseAuthorization();

// Use antiforgery middleware - this is the built-in ASP.NET middleware
app.UseAntiforgery();

// Use cookie for antiforgery token - this is custom middleware to support using cookies to transport the antiforgery token
app.UseAntiforgeryCookie();

// Use App router
app.UseRxRouter<App, ErrorPage>();

// Let's Go!
app.Run();