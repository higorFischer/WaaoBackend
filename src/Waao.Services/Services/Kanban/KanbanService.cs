using Microsoft.EntityFrameworkCore;
using Waao.Domain.Models.Entities;
using Waao.Domain.Models.Entities.Kanban;
using Waao.Domain.Models.Enums;
using Waao.Domain.Models.Rules;
using Waao.Infra.EF;
using Waao.Services.Abstractions.Dtos;
using Waao.Services.Abstractions.Dtos.Kanban;
using Waao.Services.Abstractions.Services;
using Waao.Services.Gamification;

namespace Waao.Services.Services.Kanban;

public sealed class KanbanService(
	WaaoDbContext Db,
	GamificationEngine Gamification,
	BadgeEvaluator Badges) : IKanbanService
{
	private const decimal RankStep = 1024m;

	// =====================================================================
	// BOARDS
	// =====================================================================

	public async Task<IReadOnlyList<BoardSummaryDto>> ListBoardsAsync(Guid currentCollaboratorId, CancellationToken ct = default)
	{
		var collaborator = await Db.Collaborators
			.Include(c => c.Role)
			.FirstOrDefaultAsync(c => c.Id == currentCollaboratorId, ct);
		var seniorityOrder = collaborator?.Role?.SeniorityOrder;
		var isAdmin = collaborator?.RoleKind == CollaboratorRoleKind.Admin;

		var boards = await Db.Boards
			.Include(b => b.Owner)
			.Include(b => b.Members)
			.Where(b => !b.IsArchived && (
				isAdmin ||
				b.Visibility == BoardVisibility.Public ||
				b.OwnerId == currentCollaboratorId ||
				b.Members.Any(m => m.CollaboratorId == currentCollaboratorId) ||
				(b.MinSeniorityOrder != null && seniorityOrder != null && seniorityOrder >= b.MinSeniorityOrder)))
			.OrderBy(b => b.Title)
			.ToListAsync(ct);

		var counts = await Db.Cards
			.Where(c => !c.IsArchived)
			.GroupBy(c => c.BoardId)
			.Select(g => new { BoardId = g.Key, Count = g.Count() })
			.ToDictionaryAsync(x => x.BoardId, x => x.Count, ct);

		return boards.Select(b => new BoardSummaryDto
		{
			Id = b.Id, Slug = b.Slug, Title = b.Title, Description = b.Description, ColorHex = b.ColorHex,
			Visibility = b.Visibility, MinSeniorityOrder = b.MinSeniorityOrder,
			OwnerId = b.OwnerId, OwnerName = b.Owner?.FullName,
			CardCount = counts.GetValueOrDefault(b.Id, 0),
			MemberCount = b.Members.Count,
			IsArchived = b.IsArchived,
		}).ToList();
	}

	public async Task<BoardDetailDto?> GetBoardAsync(string slug, Guid currentCollaboratorId, CancellationToken ct = default)
	{
		var board = await Db.Boards
			.Include(b => b.Members).ThenInclude(m => m.Collaborator)
			.Include(b => b.Columns)
			.Include(b => b.Epics)
			.Include(b => b.Labels)
			.FirstOrDefaultAsync(b => b.Slug == slug, ct);
		if (board is null) return null;
		var collaborator = await Db.Collaborators.Include(c => c.Role).FirstOrDefaultAsync(c => c.Id == currentCollaboratorId, ct);
		await EnsureCanViewAsync(board, currentCollaboratorId, collaborator?.Role?.SeniorityOrder, collaborator?.RoleKind == CollaboratorRoleKind.Admin, ct);

		var cards = await Db.Cards
			.Include(c => c.Assignee)
			.Include(c => c.Epic)
			.Include(c => c.LabelMappings).ThenInclude(m => m.Label)
			.Include(c => c.Comments)
			.Include(c => c.Checklists).ThenInclude(cl => cl.Items)
			.Where(c => c.BoardId == board.Id && !c.IsArchived)
			.ToListAsync(ct);

		var cardsByColumn = cards.GroupBy(c => c.ColumnId).ToDictionary(g => g.Key, g => g.OrderBy(c => c.Rank).ToList());
		var doneCountByEpic = cards.Where(c => c.CompletedAt.HasValue && c.EpicId.HasValue)
			.GroupBy(c => c.EpicId!.Value).ToDictionary(g => g.Key, g => g.Count());
		var totalByEpic = cards.Where(c => c.EpicId.HasValue)
			.GroupBy(c => c.EpicId!.Value).ToDictionary(g => g.Key, g => g.Count());

		return new BoardDetailDto
		{
			Id = board.Id, Slug = board.Slug, Title = board.Title, Description = board.Description,
			ColorHex = board.ColorHex, Visibility = board.Visibility, MinSeniorityOrder = board.MinSeniorityOrder,
			OwnerId = board.OwnerId, IsArchived = board.IsArchived,
			Members = board.Members.Select(m => new BoardMemberDto
			{
				Id = m.Id, CollaboratorId = m.CollaboratorId, FullName = m.Collaborator.FullName,
				PhotoUrl = m.Collaborator.PhotoUrl, Role = m.Role,
			}).ToList(),
			Labels = board.Labels.Select(MapLabel).ToList(),
			Epics = board.Epics.OrderBy(e => e.Rank).Select(e => new EpicDto
			{
				Id = e.Id, Title = e.Title, Description = e.Description, ColorHex = e.ColorHex, Rank = e.Rank,
				IsArchived = e.IsArchived,
				CardCount = totalByEpic.GetValueOrDefault(e.Id, 0),
				DoneCount = doneCountByEpic.GetValueOrDefault(e.Id, 0),
			}).ToList(),
			Columns = board.Columns.OrderBy(c => c.Rank).Select(col => new BoardColumnDto
			{
				Id = col.Id, Title = col.Title, Rank = col.Rank, WipLimit = col.WipLimit, ColorHex = col.ColorHex,
				IsDone = col.IsDone,
				Cards = cardsByColumn.GetValueOrDefault(col.Id, []).Select(MapCardSummary).ToList(),
			}).ToList(),
		};
	}

	public async Task<BoardSummaryDto> CreateBoardAsync(CreateBoardDto dto, Guid currentCollaboratorId, CancellationToken ct = default)
	{
		var slugTaken = await Db.Boards.AnyAsync(b => b.Slug == dto.Slug, ct);
		if (slugTaken) throw new InvalidOperationException($"Board slug '{dto.Slug}' is already in use.");

		var board = new Board
		{
			Id = Guid.CreateVersion7(),
			Slug = dto.Slug, Title = dto.Title, Description = dto.Description,
			ColorHex = dto.ColorHex, Visibility = dto.Visibility, MinSeniorityOrder = dto.MinSeniorityOrder,
			OwnerId = currentCollaboratorId,
		};
		Db.Boards.Add(board);
		Db.BoardMembers.Add(new BoardMember
		{
			Id = Guid.CreateVersion7(), BoardId = board.Id, CollaboratorId = currentCollaboratorId,
			Role = BoardMemberRole.Owner,
		});

		if (dto.SeedDefaultColumns)
		{
			(string Title, decimal Rank, bool IsDone)[] cols = [("Backlog", 1024, false), ("To do", 2048, false), ("In progress", 3072, false), ("Done", 4096, true)];
			foreach (var (title, rank, isDone) in cols)
			{
				Db.BoardColumns.Add(new BoardColumn
				{
					Id = Guid.CreateVersion7(), BoardId = board.Id, Title = title, Rank = rank, IsDone = isDone,
				});
			}
		}

		await Db.SaveChangesAsync(ct);

		return new BoardSummaryDto
		{
			Id = board.Id, Slug = board.Slug, Title = board.Title, Description = board.Description,
			ColorHex = board.ColorHex, Visibility = board.Visibility, MinSeniorityOrder = board.MinSeniorityOrder,
			OwnerId = board.OwnerId, CardCount = 0, MemberCount = 1, IsArchived = false,
		};
	}

	public async Task<BoardSummaryDto> UpdateBoardAsync(Guid boardId, UpdateBoardDto dto, Guid currentCollaboratorId, CancellationToken ct = default)
	{
		var isAdmin = await IsAdminAsync(currentCollaboratorId, ct);
		var board = await LoadBoardWriteAsync(boardId, currentCollaboratorId, BoardMemberRole.Editor, isAdmin, ct);
		board.Title = dto.Title;
		board.Description = dto.Description;
		board.ColorHex = dto.ColorHex;
		board.Visibility = dto.Visibility;
		board.MinSeniorityOrder = dto.MinSeniorityOrder;
		board.IsArchived = dto.IsArchived;
		board.UpdatedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);
		return new BoardSummaryDto
		{
			Id = board.Id, Slug = board.Slug, Title = board.Title, Description = board.Description,
			ColorHex = board.ColorHex, Visibility = board.Visibility, MinSeniorityOrder = board.MinSeniorityOrder,
			OwnerId = board.OwnerId, IsArchived = board.IsArchived,
		};
	}

	public async Task DeleteBoardAsync(Guid boardId, Guid currentCollaboratorId, CancellationToken ct = default)
	{
		var isAdmin = await IsAdminAsync(currentCollaboratorId, ct);
		var board = await LoadBoardWriteAsync(boardId, currentCollaboratorId, BoardMemberRole.Owner, isAdmin, ct);
		board.IsDeleted = true;
		board.DeletedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);
	}

	public async Task AddMemberAsync(Guid boardId, AddBoardMemberDto dto, Guid currentCollaboratorId, CancellationToken ct = default)
	{
		var isAdmin = await IsAdminAsync(currentCollaboratorId, ct);
		var board = await LoadBoardWriteAsync(boardId, currentCollaboratorId, BoardMemberRole.Owner, isAdmin, ct);
		var existing = await Db.BoardMembers.FirstOrDefaultAsync(m => m.BoardId == board.Id && m.CollaboratorId == dto.CollaboratorId, ct);
		if (existing is not null)
		{
			existing.Role = dto.Role;
		}
		else
		{
			Db.BoardMembers.Add(new BoardMember
			{
				Id = Guid.CreateVersion7(), BoardId = board.Id, CollaboratorId = dto.CollaboratorId, Role = dto.Role,
			});
		}
		await Db.SaveChangesAsync(ct);
	}

	public async Task RemoveMemberAsync(Guid boardId, Guid memberId, Guid currentCollaboratorId, CancellationToken ct = default)
	{
		var isAdmin = await IsAdminAsync(currentCollaboratorId, ct);
		await LoadBoardWriteAsync(boardId, currentCollaboratorId, BoardMemberRole.Owner, isAdmin, ct);
		var member = await Db.BoardMembers.FirstOrDefaultAsync(m => m.Id == memberId && m.BoardId == boardId, ct)
			?? throw new KeyNotFoundException("Member not found.");
		member.IsDeleted = true;
		member.DeletedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);
	}

	// =====================================================================
	// COLUMNS / EPICS / LABELS
	// =====================================================================

	public async Task<BoardColumnDto> CreateColumnAsync(Guid boardId, CreateColumnDto dto, Guid currentCollaboratorId, CancellationToken ct = default)
	{
		await LoadBoardWriteAsync(boardId, currentCollaboratorId, BoardMemberRole.Editor, await IsAdminAsync(currentCollaboratorId, ct), ct);
		var existing = await Db.BoardColumns.Where(c => c.BoardId == boardId).OrderBy(c => c.Rank).ToListAsync(ct);
		var rank = ComputeRankAfter(existing.Select(c => c.Rank), dto.AfterColumnId is null ? null : existing.Find(c => c.Id == dto.AfterColumnId)?.Rank);

		var column = new BoardColumn
		{
			Id = Guid.CreateVersion7(), BoardId = boardId, Title = dto.Title, ColorHex = dto.ColorHex,
			WipLimit = dto.WipLimit, IsDone = dto.IsDone, Rank = rank,
		};
		Db.BoardColumns.Add(column);
		await Db.SaveChangesAsync(ct);
		return new BoardColumnDto
		{
			Id = column.Id, Title = column.Title, Rank = column.Rank, WipLimit = column.WipLimit,
			ColorHex = column.ColorHex, IsDone = column.IsDone, Cards = [],
		};
	}

	public async Task<BoardColumnDto> UpdateColumnAsync(Guid columnId, UpdateColumnDto dto, Guid currentCollaboratorId, CancellationToken ct = default)
	{
		var column = await Db.BoardColumns.Include(c => c.Board).FirstOrDefaultAsync(c => c.Id == columnId, ct)
			?? throw new KeyNotFoundException($"Column {columnId} not found.");
		await EnsureCanWriteAsync(column.Board, currentCollaboratorId, BoardMemberRole.Editor, await IsAdminAsync(currentCollaboratorId, ct), ct);
		column.Title = dto.Title;
		column.ColorHex = dto.ColorHex;
		column.WipLimit = dto.WipLimit;
		column.IsDone = dto.IsDone;
		column.UpdatedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);
		return new BoardColumnDto
		{
			Id = column.Id, Title = column.Title, Rank = column.Rank, WipLimit = column.WipLimit,
			ColorHex = column.ColorHex, IsDone = column.IsDone, Cards = [],
		};
	}

	public async Task DeleteColumnAsync(Guid columnId, Guid currentCollaboratorId, CancellationToken ct = default)
	{
		var column = await Db.BoardColumns.Include(c => c.Board).FirstOrDefaultAsync(c => c.Id == columnId, ct)
			?? throw new KeyNotFoundException($"Column {columnId} not found.");
		await EnsureCanWriteAsync(column.Board, currentCollaboratorId, BoardMemberRole.Editor, await IsAdminAsync(currentCollaboratorId, ct), ct);
		column.IsDeleted = true;
		column.DeletedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);
	}

	public async Task<EpicDto> CreateEpicAsync(Guid boardId, CreateEpicDto dto, Guid currentCollaboratorId, CancellationToken ct = default)
	{
		await LoadBoardWriteAsync(boardId, currentCollaboratorId, BoardMemberRole.Editor, await IsAdminAsync(currentCollaboratorId, ct), ct);
		var maxRank = await Db.Epics.Where(e => e.BoardId == boardId).Select(e => (decimal?)e.Rank).MaxAsync(ct) ?? 0m;
		var epic = new Epic
		{
			Id = Guid.CreateVersion7(), BoardId = boardId, Title = dto.Title, Description = dto.Description,
			ColorHex = dto.ColorHex, Rank = maxRank + RankStep,
		};
		Db.Epics.Add(epic);
		await Db.SaveChangesAsync(ct);
		return new EpicDto { Id = epic.Id, Title = epic.Title, Description = epic.Description, ColorHex = epic.ColorHex, Rank = epic.Rank };
	}

	public async Task<CardLabelDto> CreateLabelAsync(Guid boardId, CreateLabelDto dto, Guid currentCollaboratorId, CancellationToken ct = default)
	{
		await LoadBoardWriteAsync(boardId, currentCollaboratorId, BoardMemberRole.Editor, await IsAdminAsync(currentCollaboratorId, ct), ct);
		var label = new CardLabel { Id = Guid.CreateVersion7(), BoardId = boardId, Name = dto.Name, ColorHex = dto.ColorHex };
		Db.CardLabels.Add(label);
		await Db.SaveChangesAsync(ct);
		return MapLabel(label);
	}

	// =====================================================================
	// CARDS
	// =====================================================================

	public async Task<CardDetailDto> CreateCardAsync(CreateCardDto dto, Guid currentCollaboratorId, CancellationToken ct = default)
	{
		var column = await Db.BoardColumns.Include(c => c.Board).FirstOrDefaultAsync(c => c.Id == dto.ColumnId, ct)
			?? throw new KeyNotFoundException($"Column {dto.ColumnId} not found.");
		await EnsureCanWriteAsync(column.Board, currentCollaboratorId, BoardMemberRole.Editor, await IsAdminAsync(currentCollaboratorId, ct), ct);

		var maxRank = await Db.Cards.Where(c => c.ColumnId == column.Id && !c.IsArchived).Select(c => (decimal?)c.Rank).MaxAsync(ct) ?? 0m;
		var card = new Card
		{
			Id = Guid.CreateVersion7(),
			BoardId = column.BoardId, ColumnId = column.Id, EpicId = dto.EpicId,
			Title = dto.Title, Description = dto.Description, Priority = dto.Priority,
			AssigneeId = dto.AssigneeId, ReporterId = currentCollaboratorId,
			DueDate = dto.DueDate, StoryPoints = dto.StoryPoints,
			Rank = maxRank + RankStep,
			CompletedAt = column.IsDone ? DateTime.UtcNow : null,
		};
		Db.Cards.Add(card);

		if (dto.LabelIds is not null)
		{
			foreach (var lid in dto.LabelIds.Distinct())
			{
				Db.CardLabelMaps.Add(new CardLabelMap { Id = Guid.CreateVersion7(), CardId = card.Id, LabelId = lid });
			}
		}

		Db.CardActivities.Add(new CardActivity
		{
			Id = Guid.CreateVersion7(), CardId = card.Id, ActorId = currentCollaboratorId,
			Kind = CardActivityKind.Created, Detail = card.Title,
		});

		await Db.SaveChangesAsync(ct);
		return await GetCardOrThrowAsync(card.Id, currentCollaboratorId, ct);
	}

	public async Task<CardDetailDto?> GetCardAsync(Guid cardId, Guid currentCollaboratorId, CancellationToken ct = default)
	{
		var card = await LoadCardForReadAsync(cardId, currentCollaboratorId, ct);
		return card is null ? null : await MapCardDetailAsync(card, ct);
	}

	public async Task<CardDetailDto> UpdateCardAsync(Guid cardId, UpdateCardDto dto, Guid currentCollaboratorId, CancellationToken ct = default)
	{
		var card = await LoadCardForWriteAsync(cardId, currentCollaboratorId, BoardMemberRole.Editor, ct);

		var changes = new List<string>();
		if (card.Title != dto.Title) { changes.Add($"renamed to “{dto.Title}”"); card.Title = dto.Title; }
		if (card.Description != dto.Description) { card.Description = dto.Description; changes.Add("updated description"); }
		if (card.Priority != dto.Priority) { changes.Add($"priority → {dto.Priority}"); card.Priority = dto.Priority; }
		if (card.EpicId != dto.EpicId) { changes.Add(dto.EpicId is null ? "removed from epic" : "moved to epic"); card.EpicId = dto.EpicId; }
		if (card.AssigneeId != dto.AssigneeId)
		{
			changes.Add(dto.AssigneeId is null ? "unassigned" : "assigned");
			Db.CardActivities.Add(new CardActivity
			{
				Id = Guid.CreateVersion7(), CardId = card.Id, ActorId = currentCollaboratorId,
				Kind = dto.AssigneeId is null ? CardActivityKind.Unassigned : CardActivityKind.Assigned,
			});
			card.AssigneeId = dto.AssigneeId;
		}
		if (card.DueDate != dto.DueDate) { changes.Add("changed due date"); card.DueDate = dto.DueDate; }
		if (card.StoryPoints != dto.StoryPoints) { changes.Add($"points → {dto.StoryPoints}"); card.StoryPoints = dto.StoryPoints; }
		card.UpdatedAt = DateTime.UtcNow;

		if (changes.Count > 0)
		{
			Db.CardActivities.Add(new CardActivity
			{
				Id = Guid.CreateVersion7(), CardId = card.Id, ActorId = currentCollaboratorId,
				Kind = CardActivityKind.Updated, Detail = string.Join(", ", changes),
			});
		}

		await Db.SaveChangesAsync(ct);
		return await GetCardOrThrowAsync(card.Id, currentCollaboratorId, ct);
	}

	public async Task<CardMovedResultDto> MoveCardAsync(Guid cardId, MoveCardDto dto, Guid currentCollaboratorId, CancellationToken ct = default)
	{
		var card = await LoadCardForWriteAsync(cardId, currentCollaboratorId, BoardMemberRole.Editor, ct);
		var sourceColumn = await Db.BoardColumns.FirstAsync(c => c.Id == card.ColumnId, ct);
		var targetColumn = await Db.BoardColumns.Include(c => c.Board).FirstOrDefaultAsync(c => c.Id == dto.TargetColumnId, ct)
			?? throw new KeyNotFoundException($"Target column {dto.TargetColumnId} not found.");
		if (targetColumn.BoardId != card.BoardId)
			throw new InvalidOperationException("Cannot move a card across boards.");

		// compute new rank within target column
		var siblings = await Db.Cards
			.Where(c => c.ColumnId == targetColumn.Id && c.Id != card.Id && !c.IsArchived)
			.OrderBy(c => c.Rank)
			.Select(c => new { c.Id, c.Rank })
			.ToListAsync(ct);
		decimal newRank = siblings.Count == 0 ? RankStep : ComputeRankBetween(
			before: dto.BeforeCardId is null ? null : siblings.Find(s => s.Id == dto.BeforeCardId.Value)?.Rank,
			after:  dto.AfterCardId  is null ? null : siblings.Find(s => s.Id == dto.AfterCardId.Value)?.Rank,
			tail:   siblings.LastOrDefault()?.Rank);

		var enteringDoneColumn = !sourceColumn.IsDone && targetColumn.IsDone;
		var leavingDoneColumn  = sourceColumn.IsDone && !targetColumn.IsDone;

		card.ColumnId = targetColumn.Id;
		card.Rank = newRank;
		card.UpdatedAt = DateTime.UtcNow;

		Db.CardActivities.Add(new CardActivity
		{
			Id = Guid.CreateVersion7(), CardId = card.Id, ActorId = currentCollaboratorId,
			Kind = CardActivityKind.Moved, Detail = $"{sourceColumn.Title} → {targetColumn.Title}",
		});

		var xpAwarded = 0;
		var newBadges = new List<BadgeDto>();
		var completed = false;

		if (enteringDoneColumn && !card.CompletedAt.HasValue)
		{
			card.CompletedAt = DateTime.UtcNow;
			completed = true;
			var beneficiary = card.AssigneeId ?? card.ReporterId;
			xpAwarded = XpRules.XpForCardCompleted(card.Priority, card.StoryPoints);
			if (xpAwarded > 0)
			{
				await Gamification.RecordAsync(
					beneficiary, xpAwarded, XpSource.KanbanCard,
					$"Card completed: {card.Title}", card.Id, nameof(Card), ct);
			}

			// Check if epic just completed (all non-archived cards in it are done)
			if (card.EpicId.HasValue)
			{
				var remaining = await Db.Cards.AnyAsync(c => c.EpicId == card.EpicId && c.Id != card.Id && !c.IsArchived && !c.CompletedAt.HasValue, ct);
				if (!remaining)
				{
					var epic = await Db.Epics.FirstAsync(e => e.Id == card.EpicId.Value, ct);
					await Gamification.RecordAsync(
						beneficiary, XpRules.XpForEpicCompleted, XpSource.KanbanCard,
						$"Epic completed: {epic.Title}", epic.Id, nameof(Epic), ct);
					xpAwarded += XpRules.XpForEpicCompleted;
				}
			}
		}
		else if (leavingDoneColumn)
		{
			card.CompletedAt = null;
		}

		await Db.SaveChangesAsync(ct);

		if (completed)
		{
			var beneficiary = card.AssigneeId ?? card.ReporterId;
			var awarded = await Badges.EvaluateAsync(beneficiary, ct);
			await Db.SaveChangesAsync(ct);
			newBadges = awarded.Select(b => new BadgeDto
			{
				Id = b.Id, Code = b.Code, Name = b.Name, Description = b.Description,
				IconEmoji = b.IconEmoji, IconUrl = b.IconUrl,
				Category = b.Category, Rarity = b.Rarity, XpReward = b.XpReward, UnlockRule = b.UnlockRule,
			}).ToList();
		}

		// reload with includes
		var refreshed = await Db.Cards
			.Include(c => c.Assignee).Include(c => c.Epic)
			.Include(c => c.LabelMappings).ThenInclude(m => m.Label)
			.Include(c => c.Comments)
			.Include(c => c.Checklists).ThenInclude(cl => cl.Items)
			.FirstAsync(c => c.Id == card.Id, ct);

		return new CardMovedResultDto
		{
			Card = MapCardSummary(refreshed),
			Completed = completed,
			XpAwarded = xpAwarded,
			NewBadges = newBadges,
		};
	}

	public async Task ArchiveCardAsync(Guid cardId, Guid currentCollaboratorId, CancellationToken ct = default)
	{
		var card = await LoadCardForWriteAsync(cardId, currentCollaboratorId, BoardMemberRole.Editor, ct);
		card.IsArchived = true;
		card.UpdatedAt = DateTime.UtcNow;
		Db.CardActivities.Add(new CardActivity
		{
			Id = Guid.CreateVersion7(), CardId = card.Id, ActorId = currentCollaboratorId,
			Kind = CardActivityKind.Archived,
		});
		await Db.SaveChangesAsync(ct);
	}

	public async Task AddLabelToCardAsync(Guid cardId, Guid labelId, Guid currentCollaboratorId, CancellationToken ct = default)
	{
		var card = await LoadCardForWriteAsync(cardId, currentCollaboratorId, BoardMemberRole.Editor, ct);
		var exists = await Db.CardLabelMaps.AnyAsync(m => m.CardId == card.Id && m.LabelId == labelId, ct);
		if (exists) return;
		Db.CardLabelMaps.Add(new CardLabelMap { Id = Guid.CreateVersion7(), CardId = card.Id, LabelId = labelId });
		Db.CardActivities.Add(new CardActivity
		{
			Id = Guid.CreateVersion7(), CardId = card.Id, ActorId = currentCollaboratorId, Kind = CardActivityKind.LabelAdded,
		});
		await Db.SaveChangesAsync(ct);
	}

	public async Task RemoveLabelFromCardAsync(Guid cardId, Guid labelId, Guid currentCollaboratorId, CancellationToken ct = default)
	{
		var card = await LoadCardForWriteAsync(cardId, currentCollaboratorId, BoardMemberRole.Editor, ct);
		var map = await Db.CardLabelMaps.FirstOrDefaultAsync(m => m.CardId == card.Id && m.LabelId == labelId, ct);
		if (map is null) return;
		map.IsDeleted = true; map.DeletedAt = DateTime.UtcNow;
		Db.CardActivities.Add(new CardActivity
		{
			Id = Guid.CreateVersion7(), CardId = card.Id, ActorId = currentCollaboratorId, Kind = CardActivityKind.LabelRemoved,
		});
		await Db.SaveChangesAsync(ct);
	}

	// =====================================================================
	// COMMENTS / CHECKLISTS
	// =====================================================================

	public async Task<CardCommentDto> CreateCommentAsync(Guid cardId, CreateCommentDto dto, Guid currentCollaboratorId, CancellationToken ct = default)
	{
		var card = await LoadCardForWriteAsync(cardId, currentCollaboratorId, BoardMemberRole.Commenter, ct);
		var comment = new CardComment
		{
			Id = Guid.CreateVersion7(), CardId = card.Id, AuthorId = currentCollaboratorId, Body = dto.Body,
		};
		Db.CardComments.Add(comment);
		Db.CardActivities.Add(new CardActivity
		{
			Id = Guid.CreateVersion7(), CardId = card.Id, ActorId = currentCollaboratorId, Kind = CardActivityKind.Commented,
		});

		// Tiny XP nudge for commenting on someone else's card (self-comments don't count)
		if (card.ReporterId != currentCollaboratorId && card.AssigneeId != currentCollaboratorId)
		{
			await Gamification.RecordAsync(
				currentCollaboratorId, XpRules.XpForCommentOnOthersCard, XpSource.KanbanCard,
				$"Commented on “{card.Title}”", card.Id, nameof(Card), ct);
		}

		await Db.SaveChangesAsync(ct);
		var author = await Db.Collaborators.FirstAsync(c => c.Id == currentCollaboratorId, ct);
		return new CardCommentDto
		{
			Id = comment.Id, AuthorId = author.Id, AuthorName = author.FullName, AuthorPhotoUrl = author.PhotoUrl,
			Body = comment.Body, CreatedAt = comment.CreatedAt, IsEdited = false,
		};
	}

	public async Task<CardCommentDto> UpdateCommentAsync(Guid commentId, UpdateCommentDto dto, Guid currentCollaboratorId, CancellationToken ct = default)
	{
		var comment = await Db.CardComments.Include(c => c.Author).FirstOrDefaultAsync(c => c.Id == commentId, ct)
			?? throw new KeyNotFoundException("Comment not found.");
		if (comment.AuthorId != currentCollaboratorId)
			throw new UnauthorizedAccessException("Only the author can edit this comment.");
		comment.Body = dto.Body;
		comment.IsEdited = true;
		comment.UpdatedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);
		return new CardCommentDto
		{
			Id = comment.Id, AuthorId = comment.AuthorId, AuthorName = comment.Author.FullName,
			AuthorPhotoUrl = comment.Author.PhotoUrl, Body = comment.Body, CreatedAt = comment.CreatedAt, IsEdited = true,
		};
	}

	public async Task DeleteCommentAsync(Guid commentId, Guid currentCollaboratorId, CancellationToken ct = default)
	{
		var comment = await Db.CardComments.FirstOrDefaultAsync(c => c.Id == commentId, ct)
			?? throw new KeyNotFoundException("Comment not found.");
		if (comment.AuthorId != currentCollaboratorId)
			throw new UnauthorizedAccessException("Only the author can delete this comment.");
		comment.IsDeleted = true; comment.DeletedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);
	}

	public async Task<CardChecklistDto> CreateChecklistAsync(Guid cardId, CreateChecklistDto dto, Guid currentCollaboratorId, CancellationToken ct = default)
	{
		var card = await LoadCardForWriteAsync(cardId, currentCollaboratorId, BoardMemberRole.Editor, ct);
		var maxRank = await Db.CardChecklists.Where(c => c.CardId == card.Id).Select(c => (decimal?)c.Rank).MaxAsync(ct) ?? 0m;
		var checklist = new CardChecklist
		{
			Id = Guid.CreateVersion7(), CardId = card.Id, Title = dto.Title, Rank = maxRank + RankStep,
		};
		Db.CardChecklists.Add(checklist);
		Db.CardActivities.Add(new CardActivity
		{
			Id = Guid.CreateVersion7(), CardId = card.Id, ActorId = currentCollaboratorId,
			Kind = CardActivityKind.ChecklistChanged, Detail = $"added “{checklist.Title}”",
		});
		await Db.SaveChangesAsync(ct);
		return new CardChecklistDto { Id = checklist.Id, Title = checklist.Title, Rank = checklist.Rank, Items = [] };
	}

	public async Task<CardChecklistItemDto> CreateChecklistItemAsync(Guid checklistId, CreateChecklistItemDto dto, Guid currentCollaboratorId, CancellationToken ct = default)
	{
		var checklist = await Db.CardChecklists.Include(c => c.Card).FirstOrDefaultAsync(c => c.Id == checklistId, ct)
			?? throw new KeyNotFoundException("Checklist not found.");
		await EnsureCanWriteAsync(await Db.Boards.FirstAsync(b => b.Id == checklist.Card.BoardId, ct), currentCollaboratorId, BoardMemberRole.Editor, await IsAdminAsync(currentCollaboratorId, ct), ct);
		var maxRank = await Db.CardChecklistItems.Where(i => i.ChecklistId == checklist.Id).Select(i => (decimal?)i.Rank).MaxAsync(ct) ?? 0m;
		var item = new CardChecklistItem
		{
			Id = Guid.CreateVersion7(), ChecklistId = checklist.Id, Text = dto.Text, Rank = maxRank + RankStep,
		};
		Db.CardChecklistItems.Add(item);
		await Db.SaveChangesAsync(ct);
		return new CardChecklistItemDto { Id = item.Id, Text = item.Text, Done = item.Done, Rank = item.Rank };
	}

	public async Task<CardChecklistItemDto> UpdateChecklistItemAsync(Guid itemId, UpdateChecklistItemDto dto, Guid currentCollaboratorId, CancellationToken ct = default)
	{
		var item = await Db.CardChecklistItems.Include(i => i.Checklist).ThenInclude(c => c.Card).FirstOrDefaultAsync(i => i.Id == itemId, ct)
			?? throw new KeyNotFoundException("Checklist item not found.");
		await EnsureCanWriteAsync(await Db.Boards.FirstAsync(b => b.Id == item.Checklist.Card.BoardId, ct), currentCollaboratorId, BoardMemberRole.Editor, await IsAdminAsync(currentCollaboratorId, ct), ct);
		item.Text = dto.Text;
		item.Done = dto.Done;
		item.UpdatedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);
		return new CardChecklistItemDto { Id = item.Id, Text = item.Text, Done = item.Done, Rank = item.Rank };
	}

	// =====================================================================
	// HELPERS
	// =====================================================================

	private async Task<CardDetailDto> GetCardOrThrowAsync(Guid cardId, Guid currentCollaboratorId, CancellationToken ct)
		=> await GetCardAsync(cardId, currentCollaboratorId, ct) ?? throw new KeyNotFoundException("Card not found.");

	private async Task<Card?> LoadCardForReadAsync(Guid cardId, Guid currentCollaboratorId, CancellationToken ct)
	{
		var card = await Db.Cards
			.Include(c => c.Board).ThenInclude(b => b.Members)
			.Include(c => c.Assignee).Include(c => c.Reporter).Include(c => c.Epic)
			.Include(c => c.LabelMappings).ThenInclude(m => m.Label)
			.Include(c => c.Comments).ThenInclude(c => c.Author)
			.Include(c => c.Checklists).ThenInclude(cl => cl.Items)
			.Include(c => c.Activities).ThenInclude(a => a.Actor)
			.FirstOrDefaultAsync(c => c.Id == cardId, ct);
		if (card is null) return null;
		var collaborator = await Db.Collaborators.Include(c => c.Role).FirstOrDefaultAsync(c => c.Id == currentCollaboratorId, ct);
		await EnsureCanViewAsync(card.Board, currentCollaboratorId, collaborator?.Role?.SeniorityOrder, collaborator?.RoleKind == CollaboratorRoleKind.Admin, ct);
		return card;
	}

	private async Task<Card> LoadCardForWriteAsync(Guid cardId, Guid currentCollaboratorId, BoardMemberRole min, CancellationToken ct)
	{
		var card = await Db.Cards.Include(c => c.Board).ThenInclude(b => b.Members).FirstOrDefaultAsync(c => c.Id == cardId, ct)
			?? throw new KeyNotFoundException($"Card {cardId} not found.");
		await EnsureCanWriteAsync(card.Board, currentCollaboratorId, min, await IsAdminAsync(currentCollaboratorId, ct), ct);
		return card;
	}

	private async Task<Board> LoadBoardWriteAsync(Guid boardId, Guid currentCollaboratorId, BoardMemberRole min, bool isAdmin, CancellationToken ct)
	{
		var board = await Db.Boards.Include(b => b.Members).FirstOrDefaultAsync(b => b.Id == boardId, ct)
			?? throw new KeyNotFoundException($"Board {boardId} not found.");
		await EnsureCanWriteAsync(board, currentCollaboratorId, min, isAdmin, ct);
		return board;
	}

	private Task EnsureCanViewAsync(Board board, Guid currentCollaboratorId, int? seniorityOrder, bool isAdmin, CancellationToken _)
	{
		if (isAdmin) return Task.CompletedTask;
		if (board.Visibility == BoardVisibility.Public) return Task.CompletedTask;
		if (board.OwnerId == currentCollaboratorId) return Task.CompletedTask;
		if (board.Members.Any(m => m.CollaboratorId == currentCollaboratorId)) return Task.CompletedTask;
		if (board.MinSeniorityOrder.HasValue && seniorityOrder.HasValue && seniorityOrder.Value >= board.MinSeniorityOrder.Value) return Task.CompletedTask;
		throw new UnauthorizedAccessException("You do not have access to this board.");
	}

	private Task EnsureCanWriteAsync(Board board, Guid currentCollaboratorId, BoardMemberRole min, bool isAdmin, CancellationToken _)
	{
		if (isAdmin) return Task.CompletedTask;
		if (board.OwnerId == currentCollaboratorId) return Task.CompletedTask;
		var membership = board.Members.FirstOrDefault(m => m.CollaboratorId == currentCollaboratorId);
		if (membership is null || (int)membership.Role < (int)min)
			throw new UnauthorizedAccessException("You do not have permission to perform this action on this board.");
		return Task.CompletedTask;
	}

	private async Task<bool> IsAdminAsync(Guid collaboratorId, CancellationToken ct)
	{
		var roleKind = await Db.Collaborators
			.Where(c => c.Id == collaboratorId)
			.Select(c => (CollaboratorRoleKind?)c.RoleKind)
			.FirstOrDefaultAsync(ct);
		return roleKind == CollaboratorRoleKind.Admin;
	}

	private static decimal ComputeRankAfter(IEnumerable<decimal> existing, decimal? after)
	{
		var ordered = existing.OrderBy(r => r).ToList();
		if (after is null)
			return ordered.Count == 0 ? RankStep : ordered.Last() + RankStep;
		var idx = ordered.IndexOf(after.Value);
		if (idx < 0 || idx == ordered.Count - 1) return (after.Value) + RankStep;
		return (ordered[idx] + ordered[idx + 1]) / 2m;
	}

	private static decimal ComputeRankBetween(decimal? before, decimal? after, decimal? tail)
	{
		if (before is null && after is null) return (tail ?? 0m) + RankStep;
		if (before is null) return after!.Value - RankStep;
		if (after is null)  return before.Value + RankStep;
		return (before.Value + after.Value) / 2m;
	}

	private static CardLabelDto MapLabel(CardLabel l) => new() { Id = l.Id, Name = l.Name, ColorHex = l.ColorHex };

	private static CardSummaryDto MapCardSummary(Card c) => new()
	{
		Id = c.Id, ColumnId = c.ColumnId, EpicId = c.EpicId,
		EpicTitle = c.Epic?.Title, EpicColorHex = c.Epic?.ColorHex,
		Title = c.Title, Priority = c.Priority, Rank = c.Rank,
		AssigneeId = c.AssigneeId, AssigneeName = c.Assignee?.FullName, AssigneePhotoUrl = c.Assignee?.PhotoUrl,
		DueDate = c.DueDate, StoryPoints = c.StoryPoints,
		CommentCount = c.Comments?.Count(x => !x.IsDeleted) ?? 0,
		ChecklistTotal = c.Checklists?.SelectMany(cl => cl.Items).Count(i => !i.IsDeleted) ?? 0,
		ChecklistDone  = c.Checklists?.SelectMany(cl => cl.Items).Count(i => !i.IsDeleted && i.Done) ?? 0,
		Labels = (c.LabelMappings ?? []).Where(m => !m.IsDeleted).Select(m => MapLabel(m.Label)).ToList(),
	};

	private async Task<CardDetailDto> MapCardDetailAsync(Card c, CancellationToken ct)
	{
		await Task.CompletedTask;
		var summary = MapCardSummary(c);
		return new CardDetailDto
		{
			Id = summary.Id, ColumnId = summary.ColumnId, EpicId = summary.EpicId,
			EpicTitle = summary.EpicTitle, EpicColorHex = summary.EpicColorHex,
			Title = summary.Title, Priority = summary.Priority, Rank = summary.Rank,
			AssigneeId = summary.AssigneeId, AssigneeName = summary.AssigneeName, AssigneePhotoUrl = summary.AssigneePhotoUrl,
			DueDate = summary.DueDate, StoryPoints = summary.StoryPoints,
			CommentCount = summary.CommentCount, ChecklistDone = summary.ChecklistDone, ChecklistTotal = summary.ChecklistTotal,
			Labels = summary.Labels,
			Description = c.Description,
			ReporterId = c.ReporterId, ReporterName = c.Reporter?.FullName,
			CompletedAt = c.CompletedAt, IsArchived = c.IsArchived,
			Comments = (c.Comments ?? []).Where(x => !x.IsDeleted).OrderBy(x => x.CreatedAt).Select(cm => new CardCommentDto
			{
				Id = cm.Id, AuthorId = cm.AuthorId, AuthorName = cm.Author?.FullName ?? "",
				AuthorPhotoUrl = cm.Author?.PhotoUrl, Body = cm.Body, CreatedAt = cm.CreatedAt, IsEdited = cm.IsEdited,
			}).ToList(),
			Checklists = (c.Checklists ?? []).Where(x => !x.IsDeleted).OrderBy(cl => cl.Rank).Select(cl => new CardChecklistDto
			{
				Id = cl.Id, Title = cl.Title, Rank = cl.Rank,
				Items = (cl.Items ?? []).Where(i => !i.IsDeleted).OrderBy(i => i.Rank).Select(i => new CardChecklistItemDto
				{
					Id = i.Id, Text = i.Text, Done = i.Done, Rank = i.Rank,
				}).ToList(),
			}).ToList(),
			Activities = (c.Activities ?? []).Where(x => !x.IsDeleted).OrderByDescending(a => a.CreatedAt).Take(50).Select(a => new CardActivityDto
			{
				Id = a.Id, ActorId = a.ActorId, ActorName = a.Actor?.FullName ?? "", Kind = a.Kind,
				Detail = a.Detail, At = a.CreatedAt,
			}).ToList(),
		};
	}
}
