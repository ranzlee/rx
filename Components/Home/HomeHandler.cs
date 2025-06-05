using Hx.Components.Layout;
using Hx.Rx;

namespace Hx.Components.Home;

public class HomeHandler : IRequestHandler {

    public void MapRoutes(IEndpointRouteBuilder router) {
        router.MapGet("/", Get).AllowAnonymous();
    }

    public static async Task<IResult> Get(
        HttpContext context,
        IDriver rxDriver,
        ILogger<HomeHandler> logger
    ) {
        return await rxDriver
            .With(context)
            .RenderPage<App, HomePage>()
            .Invoke();
    }
}

