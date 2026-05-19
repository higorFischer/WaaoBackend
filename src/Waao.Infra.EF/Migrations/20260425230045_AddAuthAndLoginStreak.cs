using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Waao.Infra.EF.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthAndLoginStreak : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "current_login_streak_days",
                table: "collaborators",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_login_at",
                table: "collaborators",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "last_login_date",
                table: "collaborators",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "longest_login_streak_days",
                table: "collaborators",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "password_hash",
                table: "collaborators",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "role_kind",
                table: "collaborators",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "current_login_streak_days",
                table: "collaborators");

            migrationBuilder.DropColumn(
                name: "last_login_at",
                table: "collaborators");

            migrationBuilder.DropColumn(
                name: "last_login_date",
                table: "collaborators");

            migrationBuilder.DropColumn(
                name: "longest_login_streak_days",
                table: "collaborators");

            migrationBuilder.DropColumn(
                name: "password_hash",
                table: "collaborators");

            migrationBuilder.DropColumn(
                name: "role_kind",
                table: "collaborators");
        }
    }
}
