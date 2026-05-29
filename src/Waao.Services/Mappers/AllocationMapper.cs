using Waao.Domain.Models.Entities;
using Waao.Domain.Models.Entities.Allocation;
using Waao.Services.Abstractions.Dtos.Allocation;

namespace Waao.Services.Mappers;

public static class AllocationMapper
{
	public static CollaboratorChipDto ToChip(Collaborator c) => new()
	{
		Id = c.Id,
		FullName = c.FullName,
		PhotoUrl = c.PhotoUrl,
		RoleTitle = c.Role != null ? c.Role.Title : null,
		DepartmentName = c.Department != null ? c.Department.Name : null,
	};

	public static AllocationDto ToDto(ProjectAllocation a) => new()
	{
		Id = a.Id,
		ProjectId = a.ProjectId,
		Note = a.Note,
		Position = a.Position,
		AllocatedAt = a.AllocatedAt,
		Collaborator = ToChip(a.Collaborator),
	};

	public static ProjectWithAllocationsDto ToDto(Project p) => new()
	{
		Id = p.Id,
		Title = p.Title,
		Description = p.Description,
		ColorHex = p.ColorHex,
		Position = p.Position,
		PositionX = p.PositionX,
		PositionY = p.PositionY,
		Allocations = p.Allocations
			.OrderBy(a => a.Position)
			.Select(ToDto)
			.ToList(),
	};

	public static ProjectConnectionDto ToDto(ProjectConnection c) => new()
	{
		Id = c.Id,
		SourceProjectId = c.SourceProjectId,
		TargetProjectId = c.TargetProjectId,
		Label = c.Label,
	};
}
