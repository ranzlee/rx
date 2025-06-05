using Hx.Components.Layout;
using Hx.Rx;

namespace Hx.Components.Examples;

public class ExamplesHandler : IRequestHandler {

    public void MapRoutes(IEndpointRouteBuilder router) {
        router.MapGet("/examples", Get).AllowAnonymous();
    }

    public static async Task<IResult> Get(
        HttpContext context,
        IDriver rxDriver,
        ILogger<ExamplesPage> logger
    ) {
        return await rxDriver
            .With(context)
            .AddPage<App, ExamplesPage>()
            .Render();
    }
}
