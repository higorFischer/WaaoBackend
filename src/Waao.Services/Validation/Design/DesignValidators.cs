using FluentValidation;
using Waao.Services.Abstractions.Dtos.Design;

namespace Waao.Services.Validation.Design;

public class CreateDesignFlowValidator : AbstractValidator<CreateDesignFlowDto>
{
	public CreateDesignFlowValidator()
	{
		RuleFor(x => x.Name).NotEmpty().MaximumLength(160);
		RuleFor(x => x.Description).MaximumLength(2000).When(x => x.Description is not null);
	}
}

public class UpdateDesignFlowValidator : AbstractValidator<UpdateDesignFlowDto>
{
	public UpdateDesignFlowValidator()
	{
		RuleFor(x => x.Name).NotEmpty().MaximumLength(160);
		RuleFor(x => x.Description).MaximumLength(2000).When(x => x.Description is not null);
		RuleFor(x => x.Status).IsInEnum();
	}
}

public class CreateDesignStepValidator : AbstractValidator<CreateDesignStepDto>
{
	public CreateDesignStepValidator()
	{
		RuleFor(x => x.Title).NotEmpty().MaximumLength(160);
		RuleFor(x => x.Description).MaximumLength(2000).When(x => x.Description is not null);
		RuleFor(x => x.PositionX).Must(double.IsFinite).WithMessage("PositionX must be finite.");
		RuleFor(x => x.PositionY).Must(double.IsFinite).WithMessage("PositionY must be finite.");
	}
}

public class UpdateDesignStepValidator : AbstractValidator<UpdateDesignStepDto>
{
	public UpdateDesignStepValidator()
	{
		RuleFor(x => x.Title).NotEmpty().MaximumLength(160).When(x => x.Title is not null);
		RuleFor(x => x.Description).MaximumLength(2000).When(x => x.Description is not null);
		RuleFor(x => x.Status).IsInEnum().When(x => x.Status is not null);
		RuleFor(x => x.PositionX!.Value).Must(double.IsFinite).WithMessage("PositionX must be finite.").When(x => x.PositionX is not null);
		RuleFor(x => x.PositionY!.Value).Must(double.IsFinite).WithMessage("PositionY must be finite.").When(x => x.PositionY is not null);
	}
}

public class CreateDesignEdgeValidator : AbstractValidator<CreateDesignEdgeDto>
{
	public CreateDesignEdgeValidator()
	{
		RuleFor(x => x.SourceStepId).NotEmpty();
		RuleFor(x => x.TargetStepId).NotEmpty();
		RuleFor(x => x.TargetStepId).NotEqual(x => x.SourceStepId).WithMessage("A step cannot connect to itself.");
	}
}
