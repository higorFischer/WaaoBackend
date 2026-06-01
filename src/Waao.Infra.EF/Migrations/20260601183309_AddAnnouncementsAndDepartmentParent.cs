using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Waao.Infra.EF.Migrations
{
    /// <inheritdoc />
    public partial class AddAnnouncementsAndDepartmentParent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "parent_department_id",
                table: "departments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "announcements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    image_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    logo_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    starts_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ends_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    recurrence_kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    recurrence_until_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    audience = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    department_id = table.Column<Guid>(type: "uuid", nullable: true),
                    target_role_kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    countdown_to_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    countdown_label = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    accent_color_hex = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    effect = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_announcements", x => x.id);
                    table.ForeignKey(
                        name: "fk_announcements_departments_department_id",
                        column: x => x.department_id,
                        principalTable: "departments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "announcement_targets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    announcement_id = table.Column<Guid>(type: "uuid", nullable: false),
                    collaborator_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_announcement_targets", x => x.id);
                    table.ForeignKey(
                        name: "fk_announcement_targets_announcements_announcement_id",
                        column: x => x.announcement_id,
                        principalTable: "announcements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_announcement_targets_collaborators_collaborator_id",
                        column: x => x.collaborator_id,
                        principalTable: "collaborators",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_departments_parent_department_id",
                table: "departments",
                column: "parent_department_id");

            migrationBuilder.CreateIndex(
                name: "ix_announcement_targets_announcement_id_collaborator_id",
                table: "announcement_targets",
                columns: new[] { "announcement_id", "collaborator_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_announcement_targets_collaborator_id",
                table: "announcement_targets",
                column: "collaborator_id");

            migrationBuilder.CreateIndex(
                name: "ix_announcements_department_id",
                table: "announcements",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "ix_announcements_is_archived_starts_at_utc_ends_at_utc",
                table: "announcements",
                columns: new[] { "is_archived", "starts_at_utc", "ends_at_utc" });

            migrationBuilder.AddForeignKey(
                name: "fk_departments_departments_parent_department_id",
                table: "departments",
                column: "parent_department_id",
                principalTable: "departments",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_departments_departments_parent_department_id",
                table: "departments");

            migrationBuilder.DropTable(
                name: "announcement_targets");

            migrationBuilder.DropTable(
                name: "announcements");

            migrationBuilder.DropIndex(
                name: "ix_departments_parent_department_id",
                table: "departments");

            migrationBuilder.DropColumn(
                name: "parent_department_id",
                table: "departments");
        }
    }
}
