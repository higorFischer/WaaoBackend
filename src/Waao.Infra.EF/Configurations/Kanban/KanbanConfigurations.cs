using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Waao.Domain.Models.Entities.Kanban;

namespace Waao.Infra.EF.Configurations.Kanban;

public class BoardConfiguration : IEntityTypeConfiguration<Board>
{
	public void Configure(EntityTypeBuilder<Board> builder)
	{
		builder.HasKey(x => x.Id);
		builder.Property(x => x.Slug).IsRequired().HasMaxLength(80);
		builder.Property(x => x.Title).IsRequired().HasMaxLength(160);
		builder.Property(x => x.Description).HasMaxLength(2_000);
		builder.Property(x => x.ColorHex).HasMaxLength(9);
		builder.Property(x => x.Visibility).HasConversion<string>();

		builder.HasIndex(x => x.Slug).IsUnique().HasFilter("is_deleted = false");

		builder.HasOne(x => x.Owner)
			.WithMany()
			.HasForeignKey(x => x.OwnerId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasQueryFilter(x => !x.IsDeleted);
	}
}

public class BoardMemberConfiguration : IEntityTypeConfiguration<BoardMember>
{
	public void Configure(EntityTypeBuilder<BoardMember> builder)
	{
		builder.HasKey(x => x.Id);
		builder.Property(x => x.Role).HasConversion<string>();

		builder.HasOne(x => x.Board)
			.WithMany(b => b.Members)
			.HasForeignKey(x => x.BoardId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasOne(x => x.Collaborator)
			.WithMany()
			.HasForeignKey(x => x.CollaboratorId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasIndex(x => new { x.BoardId, x.CollaboratorId }).IsUnique().HasFilter("is_deleted = false");
		builder.HasQueryFilter(x => !x.IsDeleted);
	}
}

public class BoardColumnConfiguration : IEntityTypeConfiguration<BoardColumn>
{
	public void Configure(EntityTypeBuilder<BoardColumn> builder)
	{
		builder.HasKey(x => x.Id);
		builder.Property(x => x.Title).IsRequired().HasMaxLength(120);
		builder.Property(x => x.ColorHex).HasMaxLength(9);
		builder.Property(x => x.Rank).HasPrecision(20, 8);

		builder.HasOne(x => x.Board)
			.WithMany(b => b.Columns)
			.HasForeignKey(x => x.BoardId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasIndex(x => new { x.BoardId, x.Rank });
		builder.HasQueryFilter(x => !x.IsDeleted);
	}
}

public class EpicConfiguration : IEntityTypeConfiguration<Epic>
{
	public void Configure(EntityTypeBuilder<Epic> builder)
	{
		builder.HasKey(x => x.Id);
		builder.Property(x => x.Title).IsRequired().HasMaxLength(160);
		builder.Property(x => x.Description).HasMaxLength(4_000);
		builder.Property(x => x.ColorHex).HasMaxLength(9);
		builder.Property(x => x.Rank).HasPrecision(20, 8);

		builder.HasOne(x => x.Board)
			.WithMany(b => b.Epics)
			.HasForeignKey(x => x.BoardId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasQueryFilter(x => !x.IsDeleted);
	}
}

public class CardConfiguration : IEntityTypeConfiguration<Card>
{
	public void Configure(EntityTypeBuilder<Card> builder)
	{
		builder.HasKey(x => x.Id);
		builder.Property(x => x.Title).IsRequired().HasMaxLength(240);
		builder.Property(x => x.Description).HasMaxLength(20_000);
		builder.Property(x => x.Priority).HasConversion<string>();
		builder.Property(x => x.Rank).HasPrecision(20, 8);

		builder.HasOne(x => x.Board)
			.WithMany()
			.HasForeignKey(x => x.BoardId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasOne(x => x.Column)
			.WithMany(c => c.Cards)
			.HasForeignKey(x => x.ColumnId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasOne(x => x.Epic)
			.WithMany(e => e.Cards)
			.HasForeignKey(x => x.EpicId)
			.OnDelete(DeleteBehavior.SetNull);

		builder.HasOne(x => x.Assignee)
			.WithMany()
			.HasForeignKey(x => x.AssigneeId)
			.OnDelete(DeleteBehavior.SetNull);

		builder.HasOne(x => x.Reporter)
			.WithMany()
			.HasForeignKey(x => x.ReporterId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasIndex(x => new { x.ColumnId, x.Rank });
		builder.HasIndex(x => x.AssigneeId);
		builder.HasIndex(x => x.EpicId);
		builder.HasQueryFilter(x => !x.IsDeleted);
	}
}

public class CardLabelConfiguration : IEntityTypeConfiguration<CardLabel>
{
	public void Configure(EntityTypeBuilder<CardLabel> builder)
	{
		builder.HasKey(x => x.Id);
		builder.Property(x => x.Name).IsRequired().HasMaxLength(60);
		builder.Property(x => x.ColorHex).HasMaxLength(9);

		builder.HasOne(x => x.Board)
			.WithMany(b => b.Labels)
			.HasForeignKey(x => x.BoardId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasIndex(x => new { x.BoardId, x.Name }).IsUnique().HasFilter("is_deleted = false");
		builder.HasQueryFilter(x => !x.IsDeleted);
	}
}

public class CardLabelMapConfiguration : IEntityTypeConfiguration<CardLabelMap>
{
	public void Configure(EntityTypeBuilder<CardLabelMap> builder)
	{
		builder.HasKey(x => x.Id);

		builder.HasOne(x => x.Card)
			.WithMany(c => c.LabelMappings)
			.HasForeignKey(x => x.CardId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasOne(x => x.Label)
			.WithMany(l => l.Mappings)
			.HasForeignKey(x => x.LabelId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasIndex(x => new { x.CardId, x.LabelId }).IsUnique().HasFilter("is_deleted = false");
		builder.HasQueryFilter(x => !x.IsDeleted);
	}
}

public class CardCommentConfiguration : IEntityTypeConfiguration<CardComment>
{
	public void Configure(EntityTypeBuilder<CardComment> builder)
	{
		builder.HasKey(x => x.Id);
		builder.Property(x => x.Body).IsRequired().HasMaxLength(10_000);

		builder.HasOne(x => x.Card)
			.WithMany(c => c.Comments)
			.HasForeignKey(x => x.CardId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasOne(x => x.Author)
			.WithMany()
			.HasForeignKey(x => x.AuthorId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasIndex(x => new { x.CardId, x.CreatedAt });
		builder.HasQueryFilter(x => !x.IsDeleted);
	}
}

public class CardChecklistConfiguration : IEntityTypeConfiguration<CardChecklist>
{
	public void Configure(EntityTypeBuilder<CardChecklist> builder)
	{
		builder.HasKey(x => x.Id);
		builder.Property(x => x.Title).IsRequired().HasMaxLength(160);
		builder.Property(x => x.Rank).HasPrecision(20, 8);

		builder.HasOne(x => x.Card)
			.WithMany(c => c.Checklists)
			.HasForeignKey(x => x.CardId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasQueryFilter(x => !x.IsDeleted);
	}
}

public class CardChecklistItemConfiguration : IEntityTypeConfiguration<CardChecklistItem>
{
	public void Configure(EntityTypeBuilder<CardChecklistItem> builder)
	{
		builder.HasKey(x => x.Id);
		builder.Property(x => x.Text).IsRequired().HasMaxLength(500);
		builder.Property(x => x.Rank).HasPrecision(20, 8);

		builder.HasOne(x => x.Checklist)
			.WithMany(c => c.Items)
			.HasForeignKey(x => x.ChecklistId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasQueryFilter(x => !x.IsDeleted);
	}
}

public class CardActivityConfiguration : IEntityTypeConfiguration<CardActivity>
{
	public void Configure(EntityTypeBuilder<CardActivity> builder)
	{
		builder.HasKey(x => x.Id);
		builder.Property(x => x.Kind).HasConversion<string>();
		builder.Property(x => x.Detail).HasMaxLength(500);

		builder.HasOne(x => x.Card)
			.WithMany(c => c.Activities)
			.HasForeignKey(x => x.CardId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasOne(x => x.Actor)
			.WithMany()
			.HasForeignKey(x => x.ActorId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasIndex(x => new { x.CardId, x.CreatedAt });
		builder.HasQueryFilter(x => !x.IsDeleted);
	}
}
