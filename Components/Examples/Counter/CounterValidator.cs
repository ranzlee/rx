using FluentValidation;
using Hx.Rx;

namespace Hx.Components.Examples.Counter;

public class CounterValidator : Validator<CounterModel> {
    public CounterValidator(ValidationContext validationContext, ILogger<CounterValidator> logger)
    : base(validationContext, logger) {

        RuleFor(x => x.Count)
            .InclusiveBetween(-5, 5)
            .WithName(nameof(CounterModel.Count))
            .WithMessage("Value must be between -5 and 5.");
    }
}