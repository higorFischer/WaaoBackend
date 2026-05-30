using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Waao.Infra.EF.Migrations
{
    /// <inheritdoc />
    public partial class AddKudos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "kudos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    giver_id = table.Column<Guid>(type: "uuid", nullable: false),
                    giver_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    giver_photo_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    value = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_kudos", x => x.id);
                    table.ForeignKey(
                        name: "fk_kudos_collaborators_giver_id",
                        column: x => x.giver_id,
                        principalTable: "collaborators",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "kudo_recipients",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kudo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    collaborator_id = table.Column<Guid>(type: "uuid", nullable: false),
                    collaborator_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    collaborator_photo_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_kudo_recipients", x => x.id);
                    table.ForeignKey(
                        name: "fk_kudo_recipients_collaborators_collaborator_id",
                        column: x => x.collaborator_id,
                        principalTable: "collaborators",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_kudo_recipients_kudos_kudo_id",
                        column: x => x.kudo_id,
                        principalTable: "kudos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_kudo_recipients_collaborator_id",
                table: "kudo_recipients",
                column: "collaborator_id");

            migrationBuilder.CreateIndex(
                name: "ix_kudo_recipients_kudo_id",
                table: "kudo_recipients",
                column: "kudo_id");

            migrationBuilder.CreateIndex(
                name: "ix_kudos_created_at",
                table: "kudos",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_kudos_giver_id",
                table: "kudos",
                column: "giver_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "kudo_recipients");

            migrationBuilder.DropTable(
                name: "kudos");
        }
    }
}
