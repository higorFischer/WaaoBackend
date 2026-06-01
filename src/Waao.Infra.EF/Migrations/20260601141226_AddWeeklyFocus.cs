using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Waao.Infra.EF.Migrations
{
    /// <inheritdoc />
    public partial class AddWeeklyFocus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "weekly_focuses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    iso_year = table.Column<int>(type: "integer", nullable: false),
                    iso_week = table.Column<int>(type: "integer", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    is_published = table.Column<bool>(type: "boolean", nullable: false),
                    published_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_weekly_focuses", x => x.id);
                    table.ForeignKey(
                        name: "fk_weekly_focuses_collaborators_owner_id",
                        column: x => x.owner_id,
                        principalTable: "collaborators",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "weekly_focus_goals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    weekly_focus_id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    is_done = table.Column<bool>(type: "boolean", nullable: false),
                    done_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    position = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_weekly_focus_goals", x => x.id);
                    table.ForeignKey(
                        name: "fk_weekly_focus_goals_weekly_focuses_weekly_focus_id",
                        column: x => x.weekly_focus_id,
                        principalTable: "weekly_focuses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "weekly_focus_projects",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    weekly_focus_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    project_color_hex = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_weekly_focus_projects", x => x.id);
                    table.ForeignKey(
                        name: "fk_weekly_focus_projects_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_weekly_focus_projects_weekly_focuses_weekly_focus_id",
                        column: x => x.weekly_focus_id,
                        principalTable: "weekly_focuses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_weekly_focus_goals_weekly_focus_id",
                table: "weekly_focus_goals",
                column: "weekly_focus_id");

            migrationBuilder.CreateIndex(
                name: "ix_weekly_focus_projects_project_id",
                table: "weekly_focus_projects",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_weekly_focus_projects_weekly_focus_id_project_id",
                table: "weekly_focus_projects",
                columns: new[] { "weekly_focus_id", "project_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_weekly_focuses_is_published_start_date_end_date",
                table: "weekly_focuses",
                columns: new[] { "is_published", "start_date", "end_date" });

            migrationBuilder.CreateIndex(
                name: "ix_weekly_focuses_iso_year_iso_week",
                table: "weekly_focuses",
                columns: new[] { "iso_year", "iso_week" });

            migrationBuilder.CreateIndex(
                name: "ix_weekly_focuses_owner_id",
                table: "weekly_focuses",
                column: "owner_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "weekly_focus_goals");

            migrationBuilder.DropTable(
                name: "weekly_focus_projects");

            migrationBuilder.DropTable(
                name: "weekly_focuses");
        }
    }
}
