using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Waao.Infra.EF.Migrations
{
    /// <inheritdoc />
    public partial class AddManualBadges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "awarded_by_id",
                table: "collaborator_badges",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "expires_at",
                table: "collaborator_badges",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "color_hex",
                table: "badges",
                type: "character varying(9)",
                maxLength: 9,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_manual",
                table: "badges",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ix_collaborator_badges_awarded_by_id",
                table: "collaborator_badges",
                column: "awarded_by_id",
                filter: "awarded_by_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_collaborator_badges_awarded_by_id",
                table: "collaborator_badges");

            migrationBuilder.DropColumn(
                name: "awarded_by_id",
                table: "collaborator_badges");

            migrationBuilder.DropColumn(
                name: "expires_at",
                table: "collaborator_badges");

            migrationBuilder.DropColumn(
                name: "color_hex",
                table: "badges");

            migrationBuilder.DropColumn(
                name: "is_manual",
                table: "badges");
        }
    }
}
