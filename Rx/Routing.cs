using System.Net;
using System.Reflection;
using Hx.Components.Error;
using Hx.Components.Layout;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http.Extensions;

namespace Hx.Rx;

file sealed class DefaultRootComponent {

    private DefaultRootComponent() { }
    private static Type RootComponentType = null!;

    public static void Set<TRootComponent>() where TRootComponent : IRootComponent {
        if (RootComponentType is not null) {
            throw new InvalidOperationException("Router already initialized");
        }
        RootComponentType = typeof(TRootComponent);
    }

    public static Type Get() {
        return RootComponentType;
    }
}

public static class RoutingExtensions {

    /// <summary>
    /// Creates a route group for the application for using ASP.NET Minimal API route handlers with
    /// RazorComponents and htmx. Typically, an application will have only one route group for the web
    /// application routes.
    /// </summary>
    /// <typeparam name="TRootComponent">The default IRootComponent layout.</typeparam>
    /// <typeparam name="TErrorPage">The IComponentModel<ErrorModel> component error page.</typeparam>
    /// <param name="app">WebApplication</param>
    /// <param name="routePrefix">ASP.NET Minimal API route group prefix, typically an empty string.</param>
    public static void UseRxRouter<TRootComponent, TErrorPage>(this WebApplication app, string routePrefix = "")
        where TRootComponent : IRootComponent
        where TErrorPage : IComponent, IComponentModel<ErrorModel> {

        DefaultRootComponent.Set<TRootComponent>();

        // Setup router group
        var router = app.MapGroup(routePrefix);
        
        // Map fallback to error page
        router.MapFallback(static async (HttpContext context, IRxDriver rxDriver) => {
            return await rxDriver
                .With(context)
                .AddPage<App, ErrorPage>("Error")
                .Render();
        });
        
        // Inspect for IRouteGroups 
        var routeGroups = Assembly.GetExecutingAssembly().DefinedTypes
            .Where(type => type is { IsAbstract: false, IsInterface: false }
                && type.IsAssignableTo(typeof(IRequestHandler)))
            .Select(type => Activator.CreateInstance(type) as IRequestHandler)
            .ToArray();

        // Map routes for IRouteGroups found
        foreach (var routeGroup in routeGroups) {
            routeGroup?.MapRoutes(router);
        }
    }
}

file sealed class RouteHandler(ILogger<RouteHandler> logger) : IEndpointFilter {
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next) {
        // Verify is GET request.
        if (context.HttpContext.Request.Method != "GET") {
            logger.LogTrace("Skip pre-route processing for non-get request {method}:{request}.",
                context.HttpContext.Request.Method,
                context.HttpContext.Request.GetDisplayUrl());
            return await next(context);
        }
        // Verify endpoint.
        var endpoint = context.HttpContext.GetEndpoint();
        if (endpoint is null) {
            logger.LogTrace("Skip pre-route processing for non-determined endpoint {method}:{request}.",
                context.HttpContext.Request.Method,
                context.HttpContext.Request.GetDisplayUrl());
            // If no endpoint return 404.
            context.HttpContext.Items.Add(nameof(ErrorModel), new ErrorModel(HttpStatusCode.NotFound));
            return await next(context);
        }
        // Check for skip Rx custom route processing metadata.    
        var skipRouteHandling = endpoint.Metadata.GetMetadata<WithRxSkipRouteHandlingAttribute>();
        if (skipRouteHandling is not null) {
            return await next(context);
        }
        // Check if the request is a not-boosted htmx request.
        if (context.HttpContext.Request.IsHxRequest() && !context.HttpContext.Request.IsHxBoosted()) {
            // htmx request, so call next middleware and bailout 
            logger.LogTrace("Skip pre-route processing for htmx partial request {method}:{request}.",
                context.HttpContext.Request.Method,
                context.HttpContext.Request.GetDisplayUrl());
            return await next(context);
        }
        // Check for razor component response metadata.    
        var rootComponentAttr = endpoint.Metadata.GetMetadata<IWithRxRootComponentAttribute>();
        // Full page request to a partial component
        if (rootComponentAttr is null) {
            logger.LogTrace("No WithRxRootComponentAttribute for request {method}:{request}. Responding with 404 NOT FOUND.",
                context.HttpContext.Request.Method,
                context.HttpContext.Request.GetDisplayUrl());
            // The status code may have been sent by the client after a handler response error
            if (context.HttpContext.Request.Query.Any(x => x.Key == "code")) {
                var code = context.HttpContext.Request.Query.FirstOrDefault(x => x.Key == "code");
                if (int.TryParse(code.Value, out var val)
                && Enum.TryParse<HttpStatusCode>(val.ToString(), out var status)) {
                    context.HttpContext.Items.Add(nameof(ErrorModel), new ErrorModel(status));
                    return await next(context);
                }
            }
            // Default to 404 since the route may only be valid as a partial via hx-[verb]
            context.HttpContext.Items.Add(nameof(ErrorModel), new ErrorModel(HttpStatusCode.NotFound));
            return await next(context);
        }
        // Add the root component type to the context.
        var rootComponent = rootComponentAttr.GetRootComponentType();
        logger.LogTrace("Adding WithRxRootComponent context item for root component type {rootComponent} for request {method}:{request}.",
                rootComponent,
                context.HttpContext.Request.Method,
                context.HttpContext.Request.GetDisplayUrl());
        context.HttpContext.Items.Add(nameof(IWithRxRootComponentAttribute), rootComponent);
        return await next(context);
    }
}

/// <summary>
/// Common interface for the WithRxRootComponentAttributes
/// </summary>
public interface IWithRxRootComponentAttribute {
    public Type GetRootComponentType();
}

/// <summary>
/// Adds the IRootComponent layout for a page-level component route.
/// </summary>
/// <typeparam name="TRootComponent">The IRootComponent that is the layout for the page.</typeparam>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class WithRxRootComponentAttribute<TRootComponent> : Attribute, IWithRxRootComponentAttribute
where TRootComponent : IRootComponent {
    public Type GetRootComponentType() {
        return typeof(TRootComponent);
    }
}

/// <summary>
/// Adds the UseRouter default IRootComponent layout for a page-level component route.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class WithRxRootComponentAttribute() : Attribute, IWithRxRootComponentAttribute {
    public Type GetRootComponentType() {
        return DefaultRootComponent.Get();
    }
}

file class WithRxRootComponent() : IEndpointFilter {
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next) {
        return await next(context);
    }
}

/// <summary>
/// Skips route handling for the configured route. Useful for things like file downloads.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class WithRxSkipRouteHandlingAttribute : Attribute { }

file class WithRxSkipRouteHandling() : IEndpointFilter {
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next) {
        return await next(context);
    }
}


