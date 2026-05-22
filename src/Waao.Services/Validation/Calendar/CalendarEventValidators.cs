using FluentValidation;
using Ical.Net.DataTypes;
using Waao.Services.Abstractions.Dtos.Calendar;

namespace Waao.Services.Validation.Calendar;

public class CreateCalendarEventValidator : AbstractValidator<CreateCalendarEventDto>
{
	public CreateCalendarEventValidator()
	{
		RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
		RuleFor(x => x.Description).MaximumLength(2000).When(x => x.Description is not null);
		RuleFor(x => x.Location).MaximumLength(200).When(x => x.Location is not null);
		RuleFor(x => x.EndsAtUtc).GreaterThanOrEqualTo(x => x.StartsAtUtc)
			.WithMessage("EndsAtUtc must be >= StartsAtUtc.");
		RuleFor(x => x.RecurrenceRule)
			.Must(BeValidRrule!)
			.When(x => !string.IsNullOrWhiteSpace(x.RecurrenceRule))
			.WithMessage("RecurrenceRule is not a valid RRULE string.");
	}

	private static bool BeValidRrule(string rule)
	{
		try { _ = new RecurrencePattern(rule); return true; }
		catch { return false; }
	}
}

public class UpdateCalendarEventValidator : AbstractValidator<UpdateCalendarEventDto>
{
	public UpdateCalendarEventValidator()
	{
		RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
		RuleFor(x => x.Description).MaximumLength(2000).When(x => x.Description is not null);
		RuleFor(x => x.Location).MaximumLength(200).When(x => x.Location is not null);
		RuleFor(x => x.EndsAtUtc).GreaterThanOrEqualTo(x => x.StartsAtUtc)
			.WithMessage("EndsAtUtc must be >= StartsAtUtc.");
		RuleFor(x => x.RecurrenceRule)
			.Must(BeValidRrule!)
			.When(x => !string.IsNullOrWhiteSpace(x.RecurrenceRule))
			.WithMessage("RecurrenceRule is not a valid RRULE string.");
	}

	private static bool BeValidRrule(string rule)
	{
		try { _ = new RecurrencePattern(rule); return true; }
		catch { return false; }
	}
}

public class EventWindowQueryValidator : AbstractValidator<EventWindowQueryDto>
{
	public EventWindowQueryValidator()
	{
		RuleFor(x => x.ToUtc).GreaterThan(x => x.FromUtc)
			.WithMessage("ToUtc must be > FromUtc.");
		RuleFor(x => x)
			.Must(q => (q.ToUtc - q.FromUtc).TotalDays <= 366)
			.WithMessage("Query window must not exceed 366 days.")
			.OverridePropertyName("window");
	}
}
