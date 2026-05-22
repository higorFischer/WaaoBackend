using System.Text.RegularExpressions;

namespace Waao.Services.Messaging;

/// <summary>
/// Parses @[Display Name](collaboratorId) mention tokens from message bodies.
/// </summary>
public static class MentionParser
{
	// Matches @[Any Name](valid-guid-36-chars) — group 1 = name, group 2 = id.
	private static readonly Regex MentionRegex = new(
		@"@\[([^\]]+)\]\(([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})\)",
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
			.Select(m => Guid.Parse(m.Groups[2].Value))
			.Distinct()
			.ToList();
	}

	/// <summary>
	/// Renders a message body as plain text — @[Name](id) tokens collapse to @Name,
	/// so notification bodies and channel previews never expose raw ids.
	/// </summary>
	public static string ToPlainText(string body)
	{
		if (string.IsNullOrEmpty(body))
			return body;

		return MentionRegex.Replace(body, m => $"@{m.Groups[1].Value}");
	}
}
