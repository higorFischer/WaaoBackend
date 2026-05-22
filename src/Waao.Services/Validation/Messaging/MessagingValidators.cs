using FluentValidation;
using Waao.Domain.Models.Enums;
using Waao.Services.Abstractions.Dtos.Messaging;

namespace Waao.Services.Validation.Messaging;

public class CreateChannelValidator : AbstractValidator<CreateChannelDto>
{
	public CreateChannelValidator()
	{
		RuleFor(x => x.Name)
			.NotEmpty().WithMessage("Channel name is required.")
			.MaximumLength(120).WithMessage("Channel name must not exceed 120 characters.");

		RuleFor(x => x.Description)
			.MaximumLength(500).WithMessage("Description must not exceed 500 characters.")
			.When(x => x.Description is not null);

		RuleFor(x => x.Kind)
			.Must(k => k == ChannelKind.Public || k == ChannelKind.Private)
			.WithMessage("Kind must be Public or Private. Use the DM endpoint for direct messages.");
	}
}

public class PostMessageValidator : AbstractValidator<PostMessageDto>
{
	public PostMessageValidator()
	{
		RuleFor(x => x.Body)
			.NotEmpty().WithMessage("Message body is required.")
			.MaximumLength(4000).WithMessage("Message body must not exceed 4000 characters.");
	}
}
