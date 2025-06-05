﻿using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Http.HttpResults;
using System.Text;

namespace Hx.Rx;

// public class FragmentRenderer(HtmlRenderer htmlRenderer) {
//     public Task<string> RenderComponent<TComponent, TModel>(TModel model)
//     where TComponent : IComponent, IComponentModel<TModel> {
//         var parameters = ParameterView.FromDictionary(new Dictionary<string, object?> { { nameof(IComponentModel<TModel>.Model), model } });
//         return htmlRenderer.Dispatcher.InvokeAsync(async () => {
//             var output = await htmlRenderer.RenderComponentAsync<TComponent>(parameters);
//             return output.ToHtmlString();
//         });
//     }
// }


public static class RazorComponentRenderer {

    /// <summary>
    /// Renders a RazorComponent response.
    /// </summary>
    /// <typeparam name="TComponent">RazorComponent type to render</typeparam>
    /// <typeparam name="TModel">Model type to bind</typeparam>
    /// <param name="response">HttpResponse</param>
    /// <param name="model">Model instance</param>
    /// <param name="logger">ILogger</param>
    /// <returns>RazorComponentResult</returns>
    public static RazorComponentResult RenderComponent<TComponent, TModel>(this HttpResponse response, TModel model, ILogger<IRequestHandler>? logger = default)
    where TComponent : IComponent, IComponentModel<TModel> {
        var root = response.HttpContext.GetRootComponent();
        if (root is not null) {
            var parameters = new Dictionary<string, object?> {
                { nameof(IRootComponent.MainContent), typeof(TComponent) },
                { nameof(IRootComponent.MainContentParameters), new Dictionary<string, object?> {{ nameof(IComponentModel<TModel>.Model), model }}},
            };
            logger?.LogInformation("Rendering page - Root component: {root} - Content component: {content} - Model: {model}",
                root,
                typeof(TComponent),
                typeof(TModel));
            return new RazorComponentResult(root, parameters);
        }
        logger?.LogInformation("Rendering partial - Content component: {content} - Model: {model}",
                typeof(TComponent),
                typeof(TModel));
        return new RazorComponentResult<TComponent>(new { Model = model });
    }

    // public static async Task<IResult> RenderFragments(
    //     this HttpResponse response,
    //     IEnumerable<(Type component, object model)> fragments,
    //     ILogger<IRequestHandler>? logger = default) {
    //     var fragmentRenderer = response.HttpContext.GetFragmentRenderer();
    //     var content = new StringBuilder();
    //     foreach (var (component, model) in fragments) {
    //         content.Append(await fragmentRenderer.RenderComponent(component, model));
    //     }
    //     return Results.Content(content.ToString(), "text/html");
    // }

    /// <summary>
    /// Renders a RazorComponent response.
    /// </summary>
    /// <typeparam name="TComponent">RazorComponent type to render</typeparam>
    /// <typeparam name="TModel">Model type to bind</typeparam>
    /// <param name="response">HttpResponse</param>
    /// <param name="model">Model instance</param>
    /// <param name="slotIdToRender">The Id of the RxSlot to include in rendering, or empty string for all slots</param>
    /// <param name="logger">ILogger</param>
    /// <returns>RazorComponentResult</returns>
    public static RazorComponentResult RenderComponent<TComponent, TModel>(this HttpResponse response, TModel model, string slotIdToRender = "", ILogger<IRequestHandler>? logger = default)
    where TComponent : IComponent, IComponentModelWithSlots<TModel> {
        var root = response.HttpContext.GetRootComponent();
        if (root is not null) {
            var parameters = new Dictionary<string, object?> {
                { nameof(IRootComponent.MainContent), typeof(TComponent) },
                { nameof(IRootComponent.MainContentParameters), new Dictionary<string, object?> {
                    { nameof(IComponentModelWithSlots<TModel>.Model), model },
                    { nameof(IComponentModelWithSlots<TModel>.SlotIdToRender), slotIdToRender ?? string.Empty }
                }},
            };
            logger?.LogInformation("Rendering page - Root component: {root} - Content component: {content} - Model: {model} - SlotIdToRender: {slot}",
                root,
                typeof(TComponent),
                typeof(TModel),
                slotIdToRender ?? string.Empty);
            return new RazorComponentResult(root, parameters);
        }
        logger?.LogInformation("Rendering partial - Content component: {content} - Model: {model} - SlotIdToRender: {slot}",
                typeof(TComponent),
                typeof(TModel),
                slotIdToRender ?? string.Empty);
        return new RazorComponentResult<TComponent>(new { Model = model, SlotIdToRender = slotIdToRender });
    }

    /// <summary>
    /// Renders a RazorComponent response.
    /// </summary>
    /// <typeparam name="TComponent">RazorComponent type to render</typeparam>
    /// <param name="response">HttpResponse</param>
    /// <param name="logger">ILogger</param>
    /// <returns>RazorComponentResult</returns>
    public static RazorComponentResult RenderComponent<TComponent>(this HttpResponse response, ILogger<IRequestHandler>? logger = default)
    where TComponent : IComponent {
        var root = response.HttpContext.GetRootComponent();
        if (root is not null) {
            var parameters = new Dictionary<string, object?> {
                { nameof(IRootComponent.MainContent), typeof(TComponent) },
            };
            logger?.LogInformation("Rendering page (no model) - Root component: {root} - Content component: {content}",
                root,
                typeof(TComponent));
            return new RazorComponentResult(root, parameters);
        }
        logger?.LogInformation("Rendering partial (no model) - Content component: {content}",
                typeof(TComponent));
        return new RazorComponentResult<TComponent>();
    }

    /// <summary>
    /// Get the current route.
    /// </summary>
    /// <param name="context">HttpContext</param>
    /// <returns>String route</returns>
    public static string GetCurrentRoute(this HttpContext context) {
        return context.Request.Path.ToString().ToLower();
    }

    private static Type? GetRootComponent(this HttpContext context) {
        return context.Items[nameof(IWithRxRootComponentAttribute)] as Type;
    }

    // private static FragmentRenderer GetFragmentRenderer(this HttpContext context) {
    //     return (context.Items[nameof(FragmentRenderer)] as FragmentRenderer)!;
    // }

}
