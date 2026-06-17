using System.Text;
using FluentAssertions;
using Waao.Domain.Models.Enums;
using Waao.Infra.EF;
using Waao.Services.Abstractions.Dtos.Design;
using Waao.Services.Abstractions.Services;
using Waao.Services.Services.Design;
using Waao.Services.Validation.Design;
using Waao.Tests.Support;
using Xunit;

namespace Waao.Tests.Design;

public class DesignFlowServiceTests
{
	private static DesignFlowService Build(WaaoDbContext db, IR2StorageService? storage = null)
		=> new(
			db,
			storage ?? new EnabledR2(),
			new CreateDesignFlowValidator(),
			new UpdateDesignFlowValidator(),
			new CreateDesignStepValidator(),
			new UpdateDesignStepValidator(),
			new CreateDesignEdgeValidator());

	[Fact]
	public async Task CreateFlow_ThenList_ReturnsFlowWithZeroSteps()
	{
		var db = TestDb.New();
		var svc = Build(db);

		var flow = await svc.CreateFlowAsync(new CreateDesignFlowDto { Name = "Brand Launch", Description = "v1 identity" });
		flow.Status.Should().Be(DesignFlowStatus.Active);
		flow.StepCount.Should().Be(0);

		var list = await svc.GetFlowsAsync();
		list.Should().ContainSingle().Which.Name.Should().Be("Brand Launch");
	}

	[Fact]
	public async Task Board_ReflectsStepsAndEdges()
	{
		var db = TestDb.New();
		var svc = Build(db);
		var flow = await svc.CreateFlowAsync(new CreateDesignFlowDto { Name = "Flow" });

		var a = await svc.CreateStepAsync(flow.Id, new CreateDesignStepDto { Title = "Research", PositionX = 0, PositionY = 0 });
		var b = await svc.CreateStepAsync(flow.Id, new CreateDesignStepDto { Title = "Concept", PositionX = 280, PositionY = 0 });
		await svc.CreateEdgeAsync(flow.Id, new CreateDesignEdgeDto { SourceStepId = a.Id, TargetStepId = b.Id });

		var board = await svc.GetBoardAsync(flow.Id);
		board.Flow.StepCount.Should().Be(2);
		board.Steps.Should().HaveCount(2);
		board.Edges.Should().ContainSingle();
		board.Edges[0].SourceStepId.Should().Be(a.Id);
		board.Edges[0].TargetStepId.Should().Be(b.Id);
	}

	[Fact]
	public async Task CreateEdge_IsIdempotent_OnSamePair()
	{
		var db = TestDb.New();
		var svc = Build(db);
		var flow = await svc.CreateFlowAsync(new CreateDesignFlowDto { Name = "Flow" });
		var a = await svc.CreateStepAsync(flow.Id, new CreateDesignStepDto { Title = "A", PositionX = 0, PositionY = 0 });
		var b = await svc.CreateStepAsync(flow.Id, new CreateDesignStepDto { Title = "B", PositionX = 1, PositionY = 1 });

		var first = await svc.CreateEdgeAsync(flow.Id, new CreateDesignEdgeDto { SourceStepId = a.Id, TargetStepId = b.Id });
		var second = await svc.CreateEdgeAsync(flow.Id, new CreateDesignEdgeDto { SourceStepId = a.Id, TargetStepId = b.Id });

		second.Id.Should().Be(first.Id);
		var board = await svc.GetBoardAsync(flow.Id);
		board.Edges.Should().ContainSingle();
	}

	[Fact]
	public async Task DeleteStep_RemovesStepAndItsEdges()
	{
		var db = TestDb.New();
		var svc = Build(db);
		var flow = await svc.CreateFlowAsync(new CreateDesignFlowDto { Name = "Flow" });
		var a = await svc.CreateStepAsync(flow.Id, new CreateDesignStepDto { Title = "A", PositionX = 0, PositionY = 0 });
		var b = await svc.CreateStepAsync(flow.Id, new CreateDesignStepDto { Title = "B", PositionX = 1, PositionY = 1 });
		await svc.CreateEdgeAsync(flow.Id, new CreateDesignEdgeDto { SourceStepId = a.Id, TargetStepId = b.Id });

		await svc.DeleteStepAsync(a.Id);

		var board = await svc.GetBoardAsync(flow.Id);
		board.Steps.Should().ContainSingle().Which.Id.Should().Be(b.Id);
		board.Edges.Should().BeEmpty();
	}

	[Fact]
	public async Task UpdateStep_PatchesOnlyProvidedFields()
	{
		var db = TestDb.New();
		var svc = Build(db);
		var flow = await svc.CreateFlowAsync(new CreateDesignFlowDto { Name = "Flow" });
		var step = await svc.CreateStepAsync(flow.Id, new CreateDesignStepDto { Title = "Original", PositionX = 10, PositionY = 20 });

		var updated = await svc.UpdateStepAsync(step.Id, new UpdateDesignStepDto { Status = DesignStepStatus.Done, PositionX = 99 });

		updated.Title.Should().Be("Original");
		updated.Status.Should().Be(DesignStepStatus.Done);
		updated.PositionX.Should().Be(99);
		updated.PositionY.Should().Be(20);
	}

	[Fact]
	public async Task AddAsset_UploadsToStorage_PersistsUrlAndInferredKind()
	{
		var db = TestDb.New();
		var svc = Build(db);
		var flow = await svc.CreateFlowAsync(new CreateDesignFlowDto { Name = "Flow" });
		var step = await svc.CreateStepAsync(flow.Id, new CreateDesignStepDto { Title = "Assets", PositionX = 0, PositionY = 0 });

		var bytes = Encoding.UTF8.GetBytes("%PDF-1.7 fake");
		using var ms = new MemoryStream(bytes);
		var uploader = Guid.CreateVersion7();

		var asset = await svc.AddAssetAsync(step.Id, ms, "brand-guide.pdf", "application/pdf", bytes.Length, uploader);

		asset.Kind.Should().Be(DesignAssetKind.Pdf);
		asset.Url.Should().StartWith("https://r2.test/");
		asset.FileName.Should().Be("brand-guide.pdf");
		asset.SizeBytes.Should().Be(bytes.Length);
		asset.UploadedById.Should().Be(uploader);
		asset.ShowFullByDefault.Should().BeTrue();

		var board = await svc.GetBoardAsync(flow.Id);
		board.Steps.Single().Assets.Should().ContainSingle();
	}

	[Fact]
	public async Task UpdateAsset_TogglesShowFullByDefault()
	{
		var db = TestDb.New();
		var svc = Build(db);
		var flow = await svc.CreateFlowAsync(new CreateDesignFlowDto { Name = "Flow" });
		var step = await svc.CreateStepAsync(flow.Id, new CreateDesignStepDto { Title = "S", PositionX = 0, PositionY = 0 });
		using var ms = new MemoryStream([1, 2, 3]);
		var asset = await svc.AddAssetAsync(step.Id, ms, "icon.svg", "image/svg+xml", 3, Guid.CreateVersion7());
		asset.Kind.Should().Be(DesignAssetKind.Icon);
		asset.ShowFullByDefault.Should().BeFalse();

		var toggled = await svc.UpdateAssetAsync(asset.Id, new UpdateDesignAssetDto { ShowFullByDefault = true });
		toggled.ShowFullByDefault.Should().BeTrue();
	}

	[Theory]
	[InlineData("application/pdf", "report", DesignAssetKind.Pdf)]
	[InlineData("application/octet-stream", "report.pdf", DesignAssetKind.Pdf)]
	[InlineData("image/svg+xml", "logo", DesignAssetKind.Icon)]
	[InlineData("application/octet-stream", "favicon.ico", DesignAssetKind.Icon)]
	[InlineData("image/png", "shot.png", DesignAssetKind.Image)]
	[InlineData("application/octet-stream", "photo.jpg", DesignAssetKind.Image)]
	[InlineData("application/zip", "bundle.zip", DesignAssetKind.Other)]
	public void InferKind_MapsContentTypeAndExtension(string mime, string fileName, DesignAssetKind expected)
	{
		var ext = Path.GetExtension(fileName).TrimStart('.');
		DesignFlowService.InferKind(mime, ext).Should().Be(expected);
	}

	/// <summary>Storage double that reports enabled and returns a deterministic public URL.</summary>
	private sealed class EnabledR2 : IR2StorageService
	{
		public bool IsEnabled => true;
		public bool HasPrivateBucket => false;
		public Task<string> UploadAsync(string key, Stream content, string contentType, CancellationToken ct = default)
			=> Task.FromResult($"https://r2.test/{key}");
		public Task<string> UploadPrivateAsync(string key, Stream content, string contentType, CancellationToken ct = default)
			=> Task.FromResult(key);
		public string GetPresignedUrl(string key, TimeSpan ttl) => $"https://r2.test/private/{key}";
	}
}
