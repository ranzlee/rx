using Hx.Components.Layout;
using Hx.Rx;

namespace Hx.Components.Examples.Counter;

public class CounterHandler : IRequestHandler {

    public void MapRoutes(IEndpointRouteBuilder router) {
        router.MapGet("/examples/counter", Get).RequireAuthorization();
        router.MapPost("/examples/counter/decrement", DecrementCounter).RequireAuthorization();
        router.MapPost("/examples/counter/increment", IncrementCounter).RequireAuthorization();
    }

    public static async Task<IResult> Get(
        HttpContext context,
        IRxDriver rxDriver,
        ILogger<CounterHandler> logger
    ) {
        return await rxDriver
            .With(context)
            .AddPage<App, CounterHead, CounterPage>("The Obligatory Counter")
            .Render();
    }

    public static async Task<IResult> DecrementCounter(
        HttpContext context,
        CounterModel model,
        CounterValidator validator,
        ValidationContext validationContext,
        IRxDriver rxDriver,
        ILogger<CounterHandler> logger
    ) {
        //throw new InvalidOperationException();
        model = model with { Count = model.Count - 1 };
        validationContext.ValidationResult = await validator.ValidateAsync(model);
        return await rxDriver
            .With(context)
            .AddFragment<CounterValue, CounterModel>(model, "counter-value", FragmentMergeStrategyType.Swap)
            .AddFragment<CounterError, CounterModel>(model, "counter-error", FragmentMergeStrategyType.Morph)
            .Render();
    }

    public static async Task<IResult> IncrementCounter(
        HttpContext context,
        CounterModel model,
        CounterValidator validator,
        ValidationContext validationContext,
        IRxDriver rxDriver,
        ILogger<CounterHandler> logger
    ) {
        model = model with { Count = model.Count + 1 };
        validationContext.ValidationResult = await validator.ValidateAsync(model);
        return await rxDriver
            .With(context)
            .AddFragment<CounterValue, CounterModel>(model, "counter-value", FragmentMergeStrategyType.Morph)
            .AddFragment<CounterError, CounterModel>(model, "counter-error", FragmentMergeStrategyType.Swap)
            .Render(true);
    }
}
