using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Hx.Rx;

public interface IDriver {
    IRxResponseBuilder With(HttpContext context);
}

public enum FragmentSwapStrategyType {
    Replace = 0,
    Morph = 1
}

public static class DriverService {
    public static void AddRxDriver(this IServiceCollection services) {
        services.AddScoped<IDriver, Driver>();
    }
}

file sealed class Driver(HtmlRenderer htmlRenderer, ILogger<Driver> logger) : IDriver {
    public IRxResponseBuilder With(HttpContext context) {
        return new RxResponseBuilder(context, htmlRenderer, logger);
    }
}

public interface IRxResponseBuilder {
    IRxResponseBuilder AddPage<TRoot, TComponent, TModel>(TModel model)
    where TRoot : IRootComponent
    where TComponent : IComponent, IComponentModel<TModel>;

    IRxResponseBuilder AddPage<TRoot, TComponent>()
    where TRoot : IRootComponent
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

file record SwapStrategy(string Target, string Strategy);

file sealed class RxResponseBuilder(HttpContext context, HtmlRenderer htmlRenderer, ILogger logger) : IRxResponseBuilder {
    private bool isRendering = false;
    private Type? rootComponent = null;
    private readonly Dictionary<string, object?> rootParameters = [];
    private readonly StringBuilder content = new();
    private readonly List<Task> renderTasks = [];
    private readonly List<SwapStrategy> swapStrategies = [];
    private static readonly JsonSerializerOptions serializerSettings = new(JsonSerializerDefaults.Web);

    public IRxResponseBuilder AddPage<TRoot, TComponent, TModel>(TModel model)
    where TRoot : IRootComponent
    where TComponent : IComponent, IComponentModel<TModel> {
        CheckRenderingStatus();
        CheckPageRenderStatus();
        rootComponent = typeof(TRoot);
        rootParameters.Add(nameof(IRootComponent.MainContent), typeof(TComponent));
        rootParameters.Add(nameof(IRootComponent.MainContentParameters), new Dictionary<string, object?> {
            { nameof(IComponentModel<TModel>.Model), model }
        });
        return this;
    }

    public IRxResponseBuilder AddPage<TRoot, TComponent>()
    where TRoot : IRootComponent
    where TComponent : IComponent {
        CheckRenderingStatus();
        CheckPageRenderStatus();
        rootComponent = typeof(TRoot);
        rootParameters.Add(nameof(IRootComponent.MainContent), typeof(TComponent));
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
            return HandlePageRequest();
        }
        if (!context.Request.Headers.ContainsKey("fx-request")) {
            return TypedResults.NotFound();
        }
        isRendering = true;
        if (renderTasks.Count == 0) {
            if (context.Request.Method.Equals("delete", StringComparison.CurrentCultureIgnoreCase)) {
                return TypedResults.Ok();
            }
            return TypedResults.NoContent();
        }
        if (ignoreActiveElementValueOnMorph) {
            context.Response.Headers.Append("fx-morph-ignore-active", true.ToString());
        }
        context.Response.Headers.Append("fx-swap", JsonSerializer.Serialize(swapStrategies, serializerSettings));
        await Task.WhenAll(renderTasks);
        return Results.Content(content.ToString(), "text/html");
    }

    private void AddSwapStrategy(string targetId, FragmentSwapStrategyType fragmentSwapStrategy) {
        var swapStrategy = fragmentSwapStrategy == FragmentSwapStrategyType.Replace
            ? "replace"
            : "morph";
        swapStrategies.Add(new(targetId, swapStrategy));
    }

    private RazorComponentResult HandlePageRequest() {
        if (context.Request.Headers.ContainsKey("fx-request")) {
            throw new InvalidOperationException("AddPage was attempted on a fetch request.");
        }
        return new RazorComponentResult(rootComponent!, rootParameters);
    }

    private void CheckRenderingStatus() {
        if (isRendering) {
            throw new InvalidOperationException("Render has already been called and may only be called once per request.");
        }
    }

    private void CheckPageRenderStatus() {
        if (rootComponent is not null) {
            throw new InvalidOperationException("Driver is set to render a page. No other operations are allowed and Render must be called.");
        }
    }
}
