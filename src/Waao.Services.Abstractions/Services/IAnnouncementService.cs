using Waao.Services.Abstractions.Dtos.Announcements;

namespace Waao.Services.Abstractions.Services;

public interface IAnnouncementService
{
	Task<IReadOnlyList<AnnouncementDto>> ListAllAsync(CancellationToken ct = default);
	Task<IReadOnlyList<AnnouncementDto>> ListActiveForMeAsync(Guid callerId, CancellationToken ct = default);
	Task<AnnouncementDto> CreateAsync(CreateAnnouncementDto dto, Guid creatorId, CancellationToken ct = default);
	Task<AnnouncementDto> UpdateAsync(Guid id, UpdateAnnouncementDto dto, CancellationToken ct = default);
	Task ArchiveAsync(Guid id, CancellationToken ct = default);
}
