using Waao.Domain.Models.Entities.Team;
using Waao.Services.Abstractions.Dtos.Team;

namespace Waao.Services.Mappers.Team;

public static class ManagerNoteMapper
{
	public static ManagerNoteDto ToDto(ManagerNote n) => new()
	{
		Id = n.Id,
		CollaboratorId = n.CollaboratorId,
		AuthorId = n.AuthorId,
		AuthorName = n.AuthorName,
		Body = n.Body,
		Pinned = n.Pinned,
		CreatedAt = n.CreatedAt,
		UpdatedAt = n.UpdatedAt,
	};
}
