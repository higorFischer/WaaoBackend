using FluentValidation;
using Waao.Services.Abstractions.Dtos.Allocation;

namespace Waao.Services.Validation.Allocation;

public class CreateProjectValidator : AbstractValidator<CreateProjectDto>
{
	public CreateProjectValidator()
	{
		RuleFor(x => x.Title).NotEmpty().MaximumLength(120);
		RuleFor(x => x.Description).MaximumLength(1000).When(x => x.Description is not null);
		RuleFor(x => x.ColorHex).MaximumLength(9).When(x => x.ColorHex is not null);
	}
}

public class UpdateProjectValidator : AbstractValidator<UpdateProjectDto>
{
	public UpdateProjectValidator()
	{
		RuleFor(x => x.Title).NotEmpty().MaximumLength(120);
		RuleFor(x => x.Description).MaximumLength(1000).When(x => x.Description is not null);
		RuleFor(x => x.ColorHex).NotEmpty().MaximumLength(9);
	}
}

public class CreateAllocationValidator : AbstractValidator<CreateAllocationDto>
{
	public CreateAllocationValidator()
	{
		RuleFor(x => x.ProjectId).NotEmpty();
		RuleFor(x => x.CollaboratorId).NotEmpty();
		RuleFor(x => x.Note).MaximumLength(500).When(x => x.Note is not null);
	}
}

public class UpdateNoteValidator : AbstractValidator<UpdateNoteDto>
{
	public UpdateNoteValidator()
	{
		RuleFor(x => x.Note).MaximumLength(500).When(x => x.Note is not null);
	}
}

public class CreateConnectionValidator : AbstractValidator<CreateConnectionDto>
{
	public CreateConnectionValidator()
	{
		RuleFor(x => x.SourceProjectId).NotEmpty();
		RuleFor(x => x.TargetProjectId).NotEmpty();
		RuleFor(x => x.TargetProjectId).NotEqual(x => x.SourceProjectId).WithMessage("A project cannot connect to itself.");
		RuleFor(x => x.Label).MaximumLength(120).When(x => x.Label is not null);
	}
}

public class UpdatePositionValidator : AbstractValidator<UpdatePositionDto>
{
	public UpdatePositionValidator()
	{
		RuleFor(x => x.X).Must(double.IsFinite).WithMessage("X must be finite.");
		RuleFor(x => x.Y).Must(double.IsFinite).WithMessage("Y must be finite.");
	}
}

public class SetParentValidator : AbstractValidator<SetParentDto>
{
	public SetParentValidator()
	{
		RuleFor(x => x.X).Must(double.IsFinite).WithMessage("X must be finite.");
		RuleFor(x => x.Y).Must(double.IsFinite).WithMessage("Y must be finite.");
	}
}
