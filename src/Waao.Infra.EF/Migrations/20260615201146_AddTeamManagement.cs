using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Waao.Infra.EF.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "manager_notes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    collaborator_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    body = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    pinned = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_manager_notes", x => x.id);
                    table.ForeignKey(
                        name: "fk_manager_notes_collaborators_collaborator_id",
                        column: x => x.collaborator_id,
                        principalTable: "collaborators",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "skills",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    category = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_skills", x => x.id);
                    table.ForeignKey(
                        name: "fk_skills_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "collaborator_skills",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    collaborator_id = table.Column<Guid>(type: "uuid", nullable: false),
                    skill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    level = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    assessed_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assessed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_collaborator_skills", x => x.id);
                    table.ForeignKey(
                        name: "fk_collaborator_skills_collaborators_collaborator_id",
                        column: x => x.collaborator_id,
                        principalTable: "collaborators",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_collaborator_skills_skills_skill_id",
                        column: x => x.skill_id,
                        principalTable: "skills",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_collaborator_skills_collaborator_id",
                table: "collaborator_skills",
                column: "collaborator_id");

            migrationBuilder.CreateIndex(
                name: "ix_collaborator_skills_collaborator_id_skill_id",
                table: "collaborator_skills",
                columns: new[] { "collaborator_id", "skill_id" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_collaborator_skills_skill_id",
                table: "collaborator_skills",
                column: "skill_id");

            migrationBuilder.CreateIndex(
                name: "ix_collaborator_skills_tenant_id",
                table: "collaborator_skills",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_manager_notes_collaborator_id_pinned",
                table: "manager_notes",
                columns: new[] { "collaborator_id", "pinned" });

            migrationBuilder.CreateIndex(
                name: "ix_manager_notes_tenant_id",
                table: "manager_notes",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_skills_is_archived_is_deleted",
                table: "skills",
                columns: new[] { "is_archived", "is_deleted" });

            migrationBuilder.CreateIndex(
                name: "ix_skills_tenant_id",
                table: "skills",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "collaborator_skills");

            migrationBuilder.DropTable(
                name: "manager_notes");

            migrationBuilder.DropTable(
                name: "skills");
        }
    }
}
