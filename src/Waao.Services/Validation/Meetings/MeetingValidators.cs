using FluentValidation;
using Ical.Net.DataTypes;
using Waao.Domain.Models.Enums;
using Waao.Services.Abstractions.Dtos.Meetings;

namespace Waao.Services.Validation.Meetings;

public class CreateMeetingValidator : AbstractValidator<CreateMeetingDto>
{
	public CreateMeetingValidator()
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
		RuleFor(x => x)
			.Must(x => x.AttendeeCollaboratorIds.Count > 0 || x.AttendeeDepartmentIds.Count > 0)
			.WithMessage("At least one attendee (collaborator or department) is required.")
			.OverridePropertyName("Attendees");
		RuleForEach(x => x.Agenda).SetValidator(new AgendaItemValidator());
	}

	private static bool BeValidRrule(string rule)
	{
		try { _ = new RecurrencePattern(rule); return true; }
		catch { return false; }
	}
}

public class UpdateMeetingValidator : AbstractValidator<UpdateMeetingDto>
{
	public UpdateMeetingValidator()
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
		RuleFor(x => x)
			.Must(x => x.AttendeeCollaboratorIds.Count > 0 || x.AttendeeDepartmentIds.Count > 0)
			.WithMessage("At least one attendee (collaborator or department) is required.")
			.OverridePropertyName("Attendees");
		RuleForEach(x => x.Agenda).SetValidator(new AgendaItemValidator());
	}

	private static bool BeValidRrule(string rule)
	{
		try { _ = new RecurrencePattern(rule); return true; }
		catch { return false; }
	}
}

public class SetRsvpValidator : AbstractValidator<SetRsvpDto>
{
	public SetRsvpValidator()
	{
		RuleFor(x => x.Rsvp)
			.IsInEnum()
			.WithMessage("Rsvp must be a valid value.");
		RuleFor(x => x.Rsvp)
			.NotEqual(MeetingRsvp.NoResponse)
			.WithMessage("Rsvp cannot be set to NoResponse; choose Going, Maybe, or Declined.");
	}
}

internal class AgendaItemValidator : AbstractValidator<CreateAgendaItemDto>
{
	public AgendaItemValidator()
	{
		RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
		RuleFor(x => x.Notes).MaximumLength(1000).When(x => x.Notes is not null);
		RuleFor(x => x.DurationMinutes).GreaterThanOrEqualTo(0).When(x => x.DurationMinutes.HasValue);
	}
}
