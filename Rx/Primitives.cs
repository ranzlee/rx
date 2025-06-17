using System.Net;

namespace Hx.Rx;

/// <summary>
/// Interface for a class that contains endpoints.
/// </summary>
public interface IRequestHandler {
    void MapRoutes(IEndpointRouteBuilder router);
}

/// <summary>
/// Interface for a component that represents a page layout.
/// </summary>
public interface IRootComponent {
    public Type? HeadContent { get; set; }
    public Type MainContent { get; set; }
    public Dictionary<string, object?> MainContentParameters { get; set; }
    public string? Title { get; set; }
}

/// <summary>
/// Interface for a component with a model.
/// </summary>
/// <typeparam name="TModel">The model to bind to the component.</typeparam>
public interface IComponentModel<TModel> {
    TModel Model { get; set; }
}

/// <summary>
/// Contains error details for HTTP errors that occur in the pipeline.
/// 400-499 for client and 500+ for server
/// </summary>
/// <param name="StatusCode">HTTP status code</param>
public record ErrorModel(HttpStatusCode StatusCode) { }
