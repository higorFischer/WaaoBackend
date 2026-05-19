using FluentValidation;
using Waao.Services.Abstractions.Dtos.Kanban;

namespace Waao.Services.Validation.Kanban;

public class CreateBoardValidator : AbstractValidator<CreateBoardDto>
{
	public CreateBoardValidator()
	{
		RuleFor(x => x.Slug)
			.NotEmpty().MaximumLength(80)
			.Matches("^[a-z0-9][a-z0-9-]*$")
			.WithMessage("Slug must be lowercase alphanumeric with optional dashes.");
		RuleFor(x => x.Title).NotEmpty().MaximumLength(160);
		RuleFor(x => x.Description).MaximumLength(2_000);
	}
}

public class UpdateBoardValidator : AbstractValidator<UpdateBoardDto>
{
	public UpdateBoardValidator()
	{
		RuleFor(x => x.Title).NotEmpty().MaximumLength(160);
		RuleFor(x => x.Description).MaximumLength(2_000);
	}
}

public class CreateColumnValidator : AbstractValidator<CreateColumnDto>
{
	public CreateColumnValidator()
	{
		RuleFor(x => x.Title).NotEmpty().MaximumLength(120);
		RuleFor(x => x.WipLimit).GreaterThanOrEqualTo(0).When(x => x.WipLimit.HasValue);
	}
}

public class UpdateColumnValidator : AbstractValidator<UpdateColumnDto>
{
	public UpdateColumnValidator()
	{
		RuleFor(x => x.Title).NotEmpty().MaximumLength(120);
		RuleFor(x => x.WipLimit).GreaterThanOrEqualTo(0).When(x => x.WipLimit.HasValue);
	}
}

public class CreateEpicValidator : AbstractValidator<CreateEpicDto>
{
	public CreateEpicValidator()
	{
		RuleFor(x => x.Title).NotEmpty().MaximumLength(160);
		RuleFor(x => x.Description).MaximumLength(4_000);
	}
}

public class CreateLabelValidator : AbstractValidator<CreateLabelDto>
{
	public CreateLabelValidator()
	{
		RuleFor(x => x.Name).NotEmpty().MaximumLength(60);
	}
}

public class CreateCardValidator : AbstractValidator<CreateCardDto>
{
	public CreateCardValidator()
	{
		RuleFor(x => x.ColumnId).NotEmpty();
		RuleFor(x => x.Title).NotEmpty().MaximumLength(240);
		RuleFor(x => x.Description).MaximumLength(20_000);
		RuleFor(x => x.StoryPoints).InclusiveBetween(0, 100).When(x => x.StoryPoints.HasValue);
	}
}

public class UpdateCardValidator : AbstractValidator<UpdateCardDto>
{
	public UpdateCardValidator()
	{
		RuleFor(x => x.Title).NotEmpty().MaximumLength(240);
		RuleFor(x => x.Description).MaximumLength(20_000);
		RuleFor(x => x.StoryPoints).InclusiveBetween(0, 100).When(x => x.StoryPoints.HasValue);
	}
}

public class MoveCardValidator : AbstractValidator<MoveCardDto>
{
	public MoveCardValidator()
	{
		RuleFor(x => x.TargetColumnId).NotEmpty();
	}
}

public class CreateCommentValidator : AbstractValidator<CreateCommentDto>
{
	public CreateCommentValidator()
	{
		RuleFor(x => x.Body).NotEmpty().MaximumLength(10_000);
	}
}

public class UpdateCommentValidator : AbstractValidator<UpdateCommentDto>
{
	public UpdateCommentValidator()
	{
		RuleFor(x => x.Body).NotEmpty().MaximumLength(10_000);
	}
}

public class CreateChecklistValidator : AbstractValidator<CreateChecklistDto>
{
	public CreateChecklistValidator() { RuleFor(x => x.Title).NotEmpty().MaximumLength(160); }
}

public class CreateChecklistItemValidator : AbstractValidator<CreateChecklistItemDto>
{
	public CreateChecklistItemValidator() { RuleFor(x => x.Text).NotEmpty().MaximumLength(500); }
}

public class UpdateChecklistItemValidator : AbstractValidator<UpdateChecklistItemDto>
{
	public UpdateChecklistItemValidator() { RuleFor(x => x.Text).NotEmpty().MaximumLength(500); }
}

public class AddBoardMemberValidator : AbstractValidator<AddBoardMemberDto>
{
	public AddBoardMemberValidator() { RuleFor(x => x.CollaboratorId).NotEmpty(); }
}
