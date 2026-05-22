using FluentAssertions;
using Waao.Services.Messaging;
using Xunit;

namespace Waao.Tests.Notifications;

public class MentionParserTests
{
	[Fact]
	public void ExtractCollaboratorIds_ValidToken_ReturnsId()
	{
		var id = Guid.CreateVersion7();
		var body = $"Hello @[Ana]({id}) how are you?";

		var result = MentionParser.ExtractCollaboratorIds(body);

		result.Should().ContainSingle().Which.Should().Be(id);
	}

	[Fact]
	public void ExtractCollaboratorIds_MultipleTokens_ReturnsAll()
	{
		var id1 = Guid.CreateVersion7();
		var id2 = Guid.CreateVersion7();
		var body = $"Hey @[Ana]({id1}) and @[Bob]({id2}), please review.";

		var result = MentionParser.ExtractCollaboratorIds(body);

		result.Should().HaveCount(2);
		result.Should().Contain(id1);
		result.Should().Contain(id2);
	}

	[Fact]
	public void ExtractCollaboratorIds_DuplicateMention_DeDupes()
	{
		var id = Guid.CreateVersion7();
		var body = $"@[Ana]({id}) and @[Ana]({id}) again.";

		var result = MentionParser.ExtractCollaboratorIds(body);

		result.Should().ContainSingle().Which.Should().Be(id);
	}

	[Fact]
	public void ExtractCollaboratorIds_MalformedToken_Ignored()
	{
		var body = "Hello @[Ana](not-a-guid) and @[Bob] and @(bare-id).";

		var result = MentionParser.ExtractCollaboratorIds(body);

		result.Should().BeEmpty();
	}

	[Fact]
	public void ExtractCollaboratorIds_EmptyBody_ReturnsEmpty()
	{
		var result = MentionParser.ExtractCollaboratorIds(string.Empty);

		result.Should().BeEmpty();
	}

	[Fact]
	public void ExtractCollaboratorIds_NoMentions_ReturnsEmpty()
	{
		var result = MentionParser.ExtractCollaboratorIds("Just a plain message with no mentions.");

		result.Should().BeEmpty();
	}
}
