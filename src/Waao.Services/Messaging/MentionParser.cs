using System.Text.RegularExpressions;

namespace Waao.Services.Messaging;

/// <summary>
/// Parses @[Display Name](collaboratorId) mention tokens from message bodies.
/// </summary>
public static class MentionParser
{
	// Matches @[Any Name](valid-guid-36-chars)
	private static readonly Regex MentionRegex = new(
		@"@\[[^\]]+\]\(([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})\)",
		RegexOptions.Compiled);

	/// <summary>
	/// Extracts distinct collaborator Guids from @[Name](id) tokens in the message body.
	/// Malformed tokens are silently ignored. Result is de-duplicated.
	/// </summary>
	public static IReadOnlyList<Guid> ExtractCollaboratorIds(string body)
	{
		if (string.IsNullOrEmpty(body))
			return [];

		return MentionRegex
			.Matches(body)
			.Select(m => Guid.Parse(m.Groups[1].Value))
			.Distinct()
			.ToList();
	}
}
