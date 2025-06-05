using Hx.Components.Layout;
using Hx.Rx;

namespace Hx.Components.Examples.Counter;

public class CounterHandler : IRequestHandler {

    public void MapRoutes(IEndpointRouteBuilder router) {
        router.MapGet("/examples/counter", Get).AllowAnonymous();

        router.MapPost("/examples/counter/decrement", DecrementCounter).AllowAnonymous();

        router.MapPost("/examples/counter/increment", IncrementCounter).AllowAnonymous();
    }

    public static async Task<IResult> Get(
        HttpContext context,
        IDriver rxDriver,
        ILogger<CounterHandler> logger
    ) {
        return await rxDriver
            .With(context)
            .RenderPage<App, CounterPage>()
            .Invoke();
    }

    public static async Task<IResult> DecrementCounter(
        HttpContext context,
        CounterModel model,
        CounterValidator validator,
        ValidationContext validationContext,
        IDriver rxDriver,
        ILogger<CounterHandler> logger
    ) {
        model = model with { Count = model.Count - 1 };
        validationContext.ValidationResult = await validator.ValidateAsync(model);
        return await rxDriver
            .With(context)
            .AddFragment<CounterValue, CounterModel>(model)
            .AddFragment<CounterError, CounterModel>(model)
            .Invoke();
    }

    public static async Task<IResult> IncrementCounter(
        HttpContext context,
        CounterModel model,
        CounterValidator validator,
        ValidationContext validationContext,
        IDriver rxDriver,
        ILogger<CounterHandler> logger
    ) {
        model = model with { Count = model.Count + 1 };
        validationContext.ValidationResult = await validator.ValidateAsync(model);
        return await rxDriver
            .With(context)
            .AddFragment<CounterValue, CounterModel>(model)
            .AddFragment<CounterError, CounterModel>(model)
            .Invoke();
    }
}
