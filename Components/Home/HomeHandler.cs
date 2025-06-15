using Hx.Components.Layout;
using Hx.Rx;

namespace Hx.Components.Home;

public class HomeHandler : IRequestHandler {

    public void MapRoutes(IEndpointRouteBuilder router) {
        router.MapGet("/", Get).AllowAnonymous();
    }

    public static async Task<IResult> Get(
        HttpContext context,
        IRxDriver rxDriver,
        ILogger<HomeHandler> logger
    ) {
        return await rxDriver
            .With(context)
            .AddPage<App, HomePage>("Home")
            .Render();
    }
}

