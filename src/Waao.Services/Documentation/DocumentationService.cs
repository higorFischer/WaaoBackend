using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Waao.Services.Abstractions.Dtos.Documentation;
using Waao.Services.Abstractions.Services;

namespace Waao.Services.Documentation;

/// <summary>Testable graph-building logic, independent of the git clone step.</summary>
public static class DocumentationGraphBuilder
{
	private static readonly Regex WikilinkRegex = new(@"\[\[([^\]]+)\]\]", RegexOptions.Compiled);

	public static async Task<DocGraphDto> BuildAsync(string localPath, string[] hiddenFolders, CancellationToken ct)
	{
		var rootLen = localPath.TrimEnd('/', '\\').Length + 1;

		// --- enumerate nodes ---
		var nodes = new List<DocGraphNodeDto>();
		var pathIndex = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);      // rel-path → rel-path (identity, normalised)
		var basenameIndex = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);  // lowercased stem → rel-path (last-wins for duplicates)

		foreach (var file in Directory.EnumerateFiles(localPath, "*.md", SearchOption.AllDirectories))
		{
			ct.ThrowIfCancellationRequested();
			if (IsHidden(file, hiddenFolders)) continue;

			var rel = file[rootLen..].Replace('\\', '/');
			var stem = Path.GetFileNameWithoutExtension(rel);
			var folder = Path.GetDirectoryName(rel)?.Replace('\\', '/') ?? string.Empty;

			nodes.Add(new DocGraphNodeDto { Id = rel, Label = stem, Folder = folder, LinkCount = 0 });
			pathIndex[rel] = rel;
			basenameIndex[stem.ToLowerInvariant()] = rel;
		}

		// --- enumerate edges ---
		// Use a HashSet of normalised "source\0target" strings for case-insensitive de-duplication
		var edgeSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var edgeList = new List<(string Source, string Target)>();

		foreach (var node in nodes)
		{
			ct.ThrowIfCancellationRequested();
			var filePath = Path.Combine(localPath, node.Id.Replace('/', Path.DirectorySeparatorChar));
			var content = await File.ReadAllTextAsync(filePath, ct);

			foreach (Match m in WikilinkRegex.Matches(content))
			{
				var token = m.Groups[1].Value;

				// strip alias after |
				var pipeIdx = token.IndexOf('|');
				if (pipeIdx >= 0) token = token[..pipeIdx];

				// strip heading anchor after #
				var hashIdx = token.IndexOf('#');
				if (hashIdx >= 0) token = token[..hashIdx];

				token = token.Trim();
				if (string.IsNullOrEmpty(token)) continue;

				string? resolved;
				if (token.Contains('/'))
				{
					// path-style: try direct match, also try appending .md
					var candidate = token.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ? token : token + ".md";
					pathIndex.TryGetValue(candidate, out resolved);
				}
				else
				{
					basenameIndex.TryGetValue(token.ToLowerInvariant(), out resolved);
				}

				if (resolved is null) continue;          // unresolved — drop
				if (string.Equals(resolved, node.Id, StringComparison.OrdinalIgnoreCase)) continue;  // self-link — drop

				var key = $"{node.Id}\0{resolved}";
				if (edgeSet.Add(key))
					edgeList.Add((node.Id, resolved));
			}
		}

		// --- compute LinkCount ---
		var linkCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		foreach (var (src, tgt) in edgeList)
		{
			linkCounts[src] = linkCounts.GetValueOrDefault(src) + 1;
			linkCounts[tgt] = linkCounts.GetValueOrDefault(tgt) + 1;
		}

		var finalNodes = nodes
			.Select(n => n with { LinkCount = linkCounts.GetValueOrDefault(n.Id) })
			.ToList();

		var edges = edgeList
			.Select(e => new DocGraphEdgeDto { Source = e.Source, Target = e.Target })
			.ToList();

		return new DocGraphDto { Nodes = finalNodes, Edges = edges };
	}

	private static bool IsHidden(string path, string[] hiddenFolders)
		=> hiddenFolders.Any(h =>
			path.Contains($"{Path.DirectorySeparatorChar}{h}{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
			|| path.EndsWith($"{Path.DirectorySeparatorChar}{h}", StringComparison.OrdinalIgnoreCase));
}

public record DocumentationOptions
{
	public string RemoteUrl { get; init; } = "https://github.com/higorFischer/WaaoDocs.git";
	public string LocalPath { get; init; } = "/var/lib/waao/docs";
	public string Branch { get; init; } = "main";
	public TimeSpan PullTtl { get; init; } = TimeSpan.FromMinutes(5);
	public string[] AllowedExtensions { get; init; } = [".md", ".pdf"];
	public string[] HiddenFolders { get; init; } = [".git", ".obsidian", "bin", "template"];
}

public class DocumentationService(
	IOptions<DocumentationOptions> Options,
	ILogger<DocumentationService> Logger
) : IDocumentationService
{
	private readonly DocumentationOptions Config = Options.Value;
	private readonly SemaphoreSlim Lock = new(1, 1);
	private DateTime _lastPullUtc = DateTime.MinValue;

	private static readonly Regex FrontmatterRegex = new(@"\A---\s*\n(.*?)\n---\s*\n", RegexOptions.Singleline | RegexOptions.Compiled);
	private static readonly Regex FrontmatterKeyValue = new(@"^([A-Za-z0-9_-]+)\s*:\s*(.+?)\s*$", RegexOptions.Multiline | RegexOptions.Compiled);

	public async Task<DocTreeNodeDto> GetTreeAsync(CancellationToken ct = default)
	{
		await EnsureClonedAsync(ct);
		var root = new DirectoryInfo(Config.LocalPath);
		return BuildTree(root, root.FullName);
	}

	public async Task<DocFileDto?> GetFileAsync(string relativePath, CancellationToken ct = default)
	{
		await EnsureClonedAsync(ct);
		var safe = SafeJoin(Config.LocalPath, relativePath);
		if (safe is null || !File.Exists(safe)) return null;

		var info = new FileInfo(safe);
		var content = await File.ReadAllTextAsync(safe, ct);
		var frontmatter = ParseFrontmatter(content);

		return new DocFileDto
		{
			Path = relativePath.Replace('\\', '/'),
			Content = content,
			Frontmatter = frontmatter,
			LastModifiedUtc = info.LastWriteTimeUtc,
			SizeBytes = info.Length,
		};
	}

	public async Task<IReadOnlyList<DocSearchHitDto>> SearchAsync(string query, int maxResults = 50, CancellationToken ct = default)
	{
		await EnsureClonedAsync(ct);
		if (string.IsNullOrWhiteSpace(query)) return [];

		var results = new List<DocSearchHitDto>();
		var rootLen = Config.LocalPath.Length + 1;
		foreach (var file in Directory.EnumerateFiles(Config.LocalPath, "*.md", SearchOption.AllDirectories))
		{
			ct.ThrowIfCancellationRequested();
			if (IsHidden(file)) continue;
			var rel = file[rootLen..].Replace('\\', '/');
			int lineNum = 0;
			using var reader = new StreamReader(file);
			while (await reader.ReadLineAsync(ct) is { } line)
			{
				lineNum++;
				if (line.Contains(query, StringComparison.OrdinalIgnoreCase))
				{
					results.Add(new DocSearchHitDto { Path = rel, LineNumber = lineNum, Snippet = Truncate(line, 200) });
					if (results.Count >= maxResults) return results;
				}
			}
		}
		return results;
	}

	public async Task<DocGraphDto> GetGraphAsync(CancellationToken ct = default)
	{
		await EnsureClonedAsync(ct);
		return await DocumentationGraphBuilder.BuildAsync(Config.LocalPath, Config.HiddenFolders, ct);
	}

	public async Task<DocRefreshResultDto> RefreshAsync(CancellationToken ct = default)
	{
		await Lock.WaitAsync(ct);
		try
		{
			var dotGit = Path.Combine(Config.LocalPath, ".git");
			var existed = Directory.Exists(dotGit);

			// Heal broken cache: .git exists but HEAD is invalid (failed prior clone)
			if (existed)
			{
				try { await RunGitAsync("rev-parse HEAD", Config.LocalPath, ct); }
				catch (InvalidOperationException ex)
				{
					Logger.LogWarning(ex, "Cache at {Path} is broken — wiping and re-cloning.", Config.LocalPath);
					Directory.Delete(Config.LocalPath, recursive: true);
					existed = false;
				}
			}

			if (!existed)
			{
				var parent = Path.GetDirectoryName(Path.GetFullPath(Config.LocalPath));
				if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
				await RunGitAsync($"clone --branch {Config.Branch} --depth 50 {Config.RemoteUrl} \"{Config.LocalPath}\"", workDir: null, ct);
			}
			else
			{
				await RunGitAsync("fetch origin", Config.LocalPath, ct);
				await RunGitAsync($"reset --hard origin/{Config.Branch}", Config.LocalPath, ct);
			}
			_lastPullUtc = DateTime.UtcNow;
			var sha = (await RunGitAsync("rev-parse --short HEAD", Config.LocalPath, ct)).Trim();
			var fileCount = Directory.EnumerateFiles(Config.LocalPath, "*.*", SearchOption.AllDirectories).Count();
			Logger.LogInformation("Documentation refreshed. SHA={Sha} Files={Files}", sha, fileCount);
			return new DocRefreshResultDto
			{
				Status = existed ? "pulled" : "cloned",
				CommitSha = sha,
				PulledAtUtc = _lastPullUtc,
				FileCount = fileCount,
			};
		}
		finally { Lock.Release(); }
	}

	private async Task EnsureClonedAsync(CancellationToken ct)
	{
		if (Directory.Exists(Path.Combine(Config.LocalPath, ".git"))
		    && DateTime.UtcNow - _lastPullUtc < Config.PullTtl)
			return;
		await RefreshAsync(ct);
	}

	private DocTreeNodeDto BuildTree(DirectoryInfo dir, string root)
	{
		var children = new List<DocTreeNodeDto>();
		foreach (var sub in dir.EnumerateDirectories().OrderBy(d => d.Name))
		{
			if (Config.HiddenFolders.Contains(sub.Name)) continue;
			children.Add(BuildTree(sub, root));
		}
		foreach (var f in dir.EnumerateFiles().OrderBy(f => f.Name))
		{
			if (!Config.AllowedExtensions.Contains(f.Extension.ToLowerInvariant())) continue;
			if (f.Name.StartsWith('.')) continue;
			children.Add(new DocTreeNodeDto
			{
				Name = f.Name,
				Path = Path.GetRelativePath(root, f.FullName).Replace('\\', '/'),
				IsFolder = false,
				Children = [],
			});
		}
		return new DocTreeNodeDto
		{
			Name = dir.FullName == root ? "" : dir.Name,
			Path = dir.FullName == root ? "" : Path.GetRelativePath(root, dir.FullName).Replace('\\', '/'),
			IsFolder = true,
			Children = children,
		};
	}

	private bool IsHidden(string path)
		=> Config.HiddenFolders.Any(h => path.Contains($"{Path.DirectorySeparatorChar}{h}{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
		                              || path.EndsWith($"{Path.DirectorySeparatorChar}{h}", StringComparison.OrdinalIgnoreCase));

	private static IReadOnlyDictionary<string, string> ParseFrontmatter(string content)
	{
		var match = FrontmatterRegex.Match(content);
		if (!match.Success) return new Dictionary<string, string>();
		var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		foreach (Match kv in FrontmatterKeyValue.Matches(match.Groups[1].Value))
		{
			dict[kv.Groups[1].Value] = kv.Groups[2].Value.Trim().Trim('"');
		}
		return dict;
	}

	private static string? SafeJoin(string root, string rel)
	{
		if (string.IsNullOrEmpty(rel)) return null;
		if (rel.Contains("..")) return null;
		var full = Path.GetFullPath(Path.Combine(root, rel));
		return full.StartsWith(Path.GetFullPath(root), StringComparison.Ordinal) ? full : null;
	}

	private static string Truncate(string s, int n) => s.Length <= n ? s : s[..n] + "…";

	private static async Task<string> RunGitAsync(string args, string? workDir, CancellationToken ct)
	{
		var psi = new ProcessStartInfo
		{
			FileName = "git",
			Arguments = args,
			WorkingDirectory = workDir ?? Environment.CurrentDirectory,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
		};
		using var proc = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start git");
		var stdout = await proc.StandardOutput.ReadToEndAsync(ct);
		var stderr = await proc.StandardError.ReadToEndAsync(ct);
		await proc.WaitForExitAsync(ct);
		if (proc.ExitCode != 0) throw new InvalidOperationException($"git {args} failed: {stderr}");
		return stdout;
	}
}
