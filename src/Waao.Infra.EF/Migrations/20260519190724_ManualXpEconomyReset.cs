using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Waao.Infra.EF.Migrations
{
    /// <inheritdoc />
    public partial class ManualXpEconomyReset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Destructive economy reset — see docs/superpowers/plans/2026-05-19-manual-xp-economy.md.
            // Hard-clears xp_transactions and badge grants of soft-deleted (non-higor) users,
            // zeroes all balances/levels, soft-deletes non-higor collaborators, backfills onboarding.
            migrationBuilder.AlterColumn<int>(
                name: "current_level",
                table: "collaborators",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<DateTime>(
                name: "onboarding_completed_at",
                table: "collaborators",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql("UPDATE collaborators SET total_xp = 0, current_level = 0;");
            migrationBuilder.Sql("DELETE FROM xp_transactions;");
            migrationBuilder.Sql("UPDATE collaborators SET is_deleted = true, deleted_at = now(), updated_at = now() WHERE lower(email) <> 'higor@waao.com.br' AND is_deleted = false;");
            migrationBuilder.Sql("DELETE FROM collaborator_badges WHERE collaborator_id IN (SELECT id FROM collaborators WHERE is_deleted = true);");
            migrationBuilder.Sql("UPDATE collaborators SET onboarding_completed_at = now() WHERE onboarding_completed_at IS NULL AND is_deleted = false;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data reset (XP wipe, ledger clear, badge purge, user soft-delete, onboarding backfill) is intentionally NOT reversible.
            migrationBuilder.DropColumn(
                name: "onboarding_completed_at",
                table: "collaborators");

            migrationBuilder.AlterColumn<int>(
                name: "current_level",
                table: "collaborators",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 0);
        }
    }
}
