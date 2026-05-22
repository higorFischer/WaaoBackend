using FluentAssertions;
using Waao.Services.Documentation;
using Xunit;

namespace Waao.Tests.Documentation;

/// <summary>
/// Tests for <see cref="DocumentationGraphBuilder"/> using a local fixture directory.
/// Calls BuildAsync directly — no git clone required.
/// </summary>
public class DocumentationGraphTests : IAsyncLifetime
{
	private string _fixturePath = string.Empty;

	public async Task InitializeAsync()
	{
		_fixturePath = Path.Combine(Path.GetTempPath(), $"waao-graph-fixture-{Guid.CreateVersion7()}");
		Directory.CreateDirectory(_fixturePath);

		// alpha.md links to beta (basename) and gamma/delta (path)
		await File.WriteAllTextAsync(
			Path.Combine(_fixturePath, "alpha.md"),
			"# Alpha\n\nSee [[beta]] and [[gamma/delta]] and [[Ghost]] and also [[beta|the alias]].");

		// beta.md links back to alpha and uses a heading anchor
		await File.WriteAllTextAsync(
			Path.Combine(_fixturePath, "beta.md"),
			"# Beta\n\nBack to [[alpha#introduction]].");

		// gamma/delta.md — no outgoing links
		Directory.CreateDirectory(Path.Combine(_fixturePath, "gamma"));
		await File.WriteAllTextAsync(
			Path.Combine(_fixturePath, "gamma", "delta.md"),
			"# Delta\n\nNo links here.");

		// A file in a hidden folder — must be excluded
		Directory.CreateDirectory(Path.Combine(_fixturePath, ".obsidian"));
		await File.WriteAllTextAsync(
			Path.Combine(_fixturePath, ".obsidian", "hidden.md"),
			"# Hidden\n\nShould not appear.");
	}

	public Task DisposeAsync()
	{
		if (Directory.Exists(_fixturePath))
			Directory.Delete(_fixturePath, recursive: true);
		return Task.CompletedTask;
	}

	[Fact]
	public async Task BuildAsync_Returns_CorrectNodeCount()
	{
		var result = await DocumentationGraphBuilder.BuildAsync(
			_fixturePath,
			[".git", ".obsidian", "bin", "template"],
			CancellationToken.None);

		// 3 visible .md files: alpha, beta, gamma/delta. Hidden .obsidian/hidden excluded.
		result.Nodes.Should().HaveCount(3);
	}

	[Fact]
	public async Task BuildAsync_NodeIds_AreForwardSlashNormalized()
	{
		var result = await DocumentationGraphBuilder.BuildAsync(
			_fixturePath,
			[".git", ".obsidian"],
			CancellationToken.None);

		result.Nodes.Should().AllSatisfy(n => n.Id.Should().NotContain("\\"));
	}

	[Fact]
	public async Task BuildAsync_ResolvedEdges_ExcludeUnresolvedGhost()
	{
		var result = await DocumentationGraphBuilder.BuildAsync(
			_fixturePath,
			[".git", ".obsidian", "bin", "template"],
			CancellationToken.None);

		// [[Ghost]] should produce no edge
		result.Edges.Should().NotContain(e => e.Target.Contains("ghost", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task BuildAsync_AliasLink_ResolvesCorrectly()
	{
		var result = await DocumentationGraphBuilder.BuildAsync(
			_fixturePath,
			[".git", ".obsidian", "bin", "template"],
			CancellationToken.None);

		// [[beta|the alias]] in alpha.md should resolve to beta — de-duped with the plain [[beta]] link
		var alphaToBeta = result.Edges.Where(e =>
			e.Source.Equals("alpha.md", StringComparison.OrdinalIgnoreCase) &&
			e.Target.Equals("beta.md", StringComparison.OrdinalIgnoreCase)).ToList();

		// de-duped: only one edge alpha→beta regardless of how many times [[beta]] appears
		alphaToBeta.Should().HaveCount(1);
	}

	[Fact]
	public async Task BuildAsync_HeadingAnchorLink_ResolvesCorrectly()
	{
		var result = await DocumentationGraphBuilder.BuildAsync(
			_fixturePath,
			[".git", ".obsidian", "bin", "template"],
			CancellationToken.None);

		// [[alpha#introduction]] in beta.md should resolve to alpha
		result.Edges.Should().Contain(e =>
			e.Source.Equals("beta.md", StringComparison.OrdinalIgnoreCase) &&
			e.Target.Equals("alpha.md", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task BuildAsync_PathStyleLink_ResolvesCorrectly()
	{
		var result = await DocumentationGraphBuilder.BuildAsync(
			_fixturePath,
			[".git", ".obsidian", "bin", "template"],
			CancellationToken.None);

		// [[gamma/delta]] in alpha.md should resolve to gamma/delta.md
		result.Edges.Should().Contain(e =>
			e.Source.Equals("alpha.md", StringComparison.OrdinalIgnoreCase) &&
			e.Target.Equals("gamma/delta.md", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task BuildAsync_LinkCount_ReflectsEdgesTouching()
	{
		var result = await DocumentationGraphBuilder.BuildAsync(
			_fixturePath,
			[".git", ".obsidian", "bin", "template"],
			CancellationToken.None);

		// alpha is touched by: alpha→beta, alpha→gamma/delta, beta→alpha = 3 edges touch alpha
		var alpha = result.Nodes.Single(n => n.Id == "alpha.md");
		alpha.LinkCount.Should().Be(3);

		// gamma/delta.md is touched by alpha→gamma/delta only = 1
		var delta = result.Nodes.Single(n => n.Id == "gamma/delta.md");
		delta.LinkCount.Should().Be(1);
	}

	[Fact]
	public async Task BuildAsync_TotalResolvedEdgeCount_IsCorrect()
	{
		var result = await DocumentationGraphBuilder.BuildAsync(
			_fixturePath,
			[".git", ".obsidian", "bin", "template"],
			CancellationToken.None);

		// alpha→beta (de-duped [[beta]] + [[beta|alias]] = 1), alpha→gamma/delta, beta→alpha = 3 edges
		result.Edges.Should().HaveCount(3);
	}
}
