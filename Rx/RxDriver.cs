using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Hx.Rx;

public enum FragmentSwapStrategyType {
    Replace = 0,
    Morph = 1
}

public static class RxDriverServices {
    public static void AddRxDriver(this IServiceCollection services) {
        if (!services.Any(x => x.ServiceType == typeof(HtmlRenderer))) {
            services.AddScoped<HtmlRenderer>();
        }
        services.AddScoped<IRxDriver, RxDriver>();
        services.ConfigureOptions<HxJsonOptions>();
    }
}

public interface IRxDriver : IAsyncDisposable, IDisposable {
    IRxResponseBuilder With(HttpContext context);
}

public interface IRxResponseBuilder {
    IRxResponseBuilder AddPage<TRoot, TComponent, TModel>(TModel model, string? title = null)
    where TRoot : IRootComponent
    where TComponent : IComponent, IComponentModel<TModel>;

    IRxResponseBuilder AddPage<TRoot, TComponent>(string? title = null)
    where TRoot : IRootComponent
    where TComponent : IComponent;

    IRxResponseBuilder AddPage<TRoot, THead, TComponent, TModel>(TModel model, string? title = null)
    where TRoot : IRootComponent
    where THead : IComponent
    where TComponent : IComponent, IComponentModel<TModel>;

    IRxResponseBuilder AddPage<TRoot, THead, TComponent>(string? title = null)
    where TRoot : IRootComponent
    where THead : IComponent
    where TComponent : IComponent;

    IRxResponseBuilder AddFragment<TComponent, TModel>(
        TModel model,
        string targetId,
        FragmentSwapStrategyType fragmentSwapStrategy = FragmentSwapStrategyType.Replace
    ) where TComponent : IComponent, IComponentModel<TModel>;

    IRxResponseBuilder AddFragment<TComponent>(
        string targetId,
        FragmentSwapStrategyType fragmentSwapStrategy = FragmentSwapStrategyType.Replace
    ) where TComponent : IComponent;

    Task<IResult> Render(
        bool ignoreActiveElementValueOnMorph = false
    );
}

file sealed class RxDriver(HtmlRenderer htmlRenderer, ILogger<RxDriver> logger) : IRxDriver {
    public IRxResponseBuilder With(HttpContext context) {
        return new RxResponseBuilder(context, htmlRenderer, logger);
    }

    public ValueTask DisposeAsync() {
        logger.LogDebug("Async Disposed");
        return htmlRenderer.DisposeAsync();
    }

    public void Dispose() {
        logger.LogDebug("Disposed");
        htmlRenderer.Dispose();
    }
}

file record SwapStrategy(string Target, string Strategy);

file sealed class RxResponseBuilder(HttpContext context, HtmlRenderer htmlRenderer, ILogger logger) : IRxResponseBuilder  {
    private bool isRendering = false;
    private Type? rootComponent = null;
    private ParameterView rootParameters;
    private readonly StringBuilder content = new();
    private readonly List<Task> renderTasks = [];
    private readonly List<SwapStrategy> swapStrategies = [];
    private static readonly JsonSerializerOptions serializerSettings = new(JsonSerializerDefaults.Web);

    public IRxResponseBuilder AddPage<TRoot, TComponent, TModel>(TModel model, string? title = null)
    where TRoot : IRootComponent
    where TComponent : IComponent, IComponentModel<TModel> {
        CheckRenderingStatus();
        CheckPageRenderStatus();
        rootComponent = typeof(TRoot);
        rootParameters = ParameterView.FromDictionary(new Dictionary<string, object?> {
            { nameof(IRootComponent.MainContent), typeof(TComponent) },
            { nameof(IComponentModel<TModel>.Model), model },
            { nameof(IRootComponent.Title), title },
        });
        return this;
    }

    public IRxResponseBuilder AddPage<TRoot, TComponent>(string? title = null)
    where TRoot : IRootComponent
    where TComponent : IComponent {
        CheckRenderingStatus();
        CheckPageRenderStatus();
        rootComponent = typeof(TRoot);
        rootParameters = ParameterView.FromDictionary(new Dictionary<string, object?> {
            { nameof(IRootComponent.MainContent), typeof(TComponent) },
            { nameof(IRootComponent.Title), title },
        });
        return this;
    }

    public IRxResponseBuilder AddPage<TRoot, THead, TComponent, TModel>(TModel model, string? title = null)
    where TRoot : IRootComponent
    where THead : IComponent
    where TComponent : IComponent, IComponentModel<TModel> {
        CheckRenderingStatus();
        CheckPageRenderStatus();
        rootComponent = typeof(TRoot);
        rootParameters = ParameterView.FromDictionary(new Dictionary<string, object?> {
            { nameof(IRootComponent.MainContent), typeof(TComponent) },
            { nameof(IRootComponent.HeadContent), typeof(THead) },
            { nameof(IComponentModel<TModel>.Model), model },
            { nameof(IRootComponent.Title), title },
        });
        return this;
    }

    public IRxResponseBuilder AddPage<TRoot, THead, TComponent>(string? title = null)
    where TRoot : IRootComponent
    where THead : IComponent
    where TComponent : IComponent {
        CheckRenderingStatus();
        CheckPageRenderStatus();
        rootComponent = typeof(TRoot);
        rootParameters = ParameterView.FromDictionary(new Dictionary<string, object?> {
            { nameof(IRootComponent.MainContent), typeof(TComponent) },
            { nameof(IRootComponent.HeadContent), typeof(THead) },
            { nameof(IRootComponent.Title), title },
        });
        return this;
    }

    public IRxResponseBuilder AddFragment<TComponent, TModel>(
        TModel model,
        string targetId,
        FragmentSwapStrategyType fragmentSwapStrategy = FragmentSwapStrategyType.Replace
    ) where TComponent : IComponent, IComponentModel<TModel> {
        CheckRenderingStatus();
        CheckPageRenderStatus();
        var parameters = ParameterView.FromDictionary(new Dictionary<string, object?> {
            { nameof(IComponentModel<TModel>.Model), model }
        });
        renderTasks.Add(htmlRenderer.Dispatcher.InvokeAsync(async () => {
            var output = await htmlRenderer.RenderComponentAsync<TComponent>(parameters);
            content.Append(output.ToHtmlString());
        }));
        AddSwapStrategy(targetId, fragmentSwapStrategy);
        return this;
    }

    public IRxResponseBuilder AddFragment<TComponent>(
        string targetId,
        FragmentSwapStrategyType fragmentSwapStrategy = FragmentSwapStrategyType.Replace
    ) where TComponent : IComponent {
        CheckRenderingStatus();
        CheckPageRenderStatus();
        renderTasks.Add(htmlRenderer.Dispatcher.InvokeAsync(async () => {
            var output = await htmlRenderer.RenderComponentAsync<TComponent>();
            content.Append(output.ToHtmlString());
        }));
        AddSwapStrategy(targetId, fragmentSwapStrategy);
        return this;
    }

    public async Task<IResult> Render(
        bool ignoreActiveElementValueOnMorph = false
    ) {
        CheckRenderingStatus();
        if (rootComponent is not null) {
            logger.LogDebug("Rendering Page");
            return await HandlePageRequest();
        }
        if (!context.Request.Headers.ContainsKey("fx-request")) {
            logger.LogDebug("No Content Response");
            return TypedResults.NotFound();
        }
        isRendering = true;
        if (renderTasks.Count == 0) {
            if (context.Request.Method.Equals("delete", StringComparison.CurrentCultureIgnoreCase)) {
                logger.LogDebug("OK Response for DELETE Request");
                return TypedResults.Ok();
            }
            logger.LogDebug("No Content Response");
            return TypedResults.NoContent();
        }
        if (ignoreActiveElementValueOnMorph) {
            context.Response.Headers.Append("fx-morph-ignore-active", true.ToString());
        }
        context.Response.Headers.Append("fx-swap", JsonSerializer.Serialize(swapStrategies, serializerSettings));
        await Task.WhenAll(renderTasks);
        logger.LogDebug("Fragment Response");
        return Results.Content(content.ToString(), "text/html");
    }

    private void AddSwapStrategy(string targetId, FragmentSwapStrategyType fragmentSwapStrategy) {
        var swapStrategy = fragmentSwapStrategy == FragmentSwapStrategyType.Replace
            ? "replace"
            : "morph";
        swapStrategies.Add(new(targetId, swapStrategy));
    }

    private async Task<IResult> HandlePageRequest() {
        if (context.Request.Headers.ContainsKey("fx-request")) {
            throw new InvalidOperationException("AddPage was attempted on a fetch request.");
        }
        string output = default!;
        await htmlRenderer.Dispatcher.InvokeAsync(async () => {
            var root = await htmlRenderer.RenderComponentAsync(rootComponent!, rootParameters);
            output = root.ToHtmlString();
        });
        return Results.Content(output, "text/html");
        
    }

    private void CheckRenderingStatus() {
        if (isRendering) {
            throw new InvalidOperationException("Render has already been called and may only be called once per request.");
        }
    }

    private void CheckPageRenderStatus() {
        if (rootComponent is not null) {
            throw new InvalidOperationException("RxDriver is set to render a page. No other operations are allowed and Render must be called.");
        }
    }
}
