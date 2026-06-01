using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Waao.Infra.EF.Migrations
{
    /// <inheritdoc />
    public partial class AddOneOnOnes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "one_on_ones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    manager_id = table.Column<Guid>(type: "uuid", nullable: false),
                    manager_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    report_id = table.Column<Guid>(type: "uuid", nullable: false),
                    report_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    scheduled_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    agenda = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    notes = table.Column<string>(type: "character varying(16000)", maxLength: 16000, nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_one_on_ones", x => x.id);
                    table.ForeignKey(
                        name: "fk_one_on_ones_collaborators_manager_id",
                        column: x => x.manager_id,
                        principalTable: "collaborators",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_one_on_ones_collaborators_report_id",
                        column: x => x.report_id,
                        principalTable: "collaborators",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "one_on_one_action_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    one_on_one_id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    is_done = table.Column<bool>(type: "boolean", nullable: false),
                    done_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    assigned_to_id = table.Column<Guid>(type: "uuid", nullable: true),
                    assigned_to_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    position = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_one_on_one_action_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_one_on_one_action_items_one_on_ones_one_on_one_id",
                        column: x => x.one_on_one_id,
                        principalTable: "one_on_ones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_one_on_one_action_items_assigned_to_id_is_done",
                table: "one_on_one_action_items",
                columns: new[] { "assigned_to_id", "is_done" });

            migrationBuilder.CreateIndex(
                name: "ix_one_on_one_action_items_one_on_one_id",
                table: "one_on_one_action_items",
                column: "one_on_one_id");

            migrationBuilder.CreateIndex(
                name: "ix_one_on_ones_manager_id_scheduled_date",
                table: "one_on_ones",
                columns: new[] { "manager_id", "scheduled_date" });

            migrationBuilder.CreateIndex(
                name: "ix_one_on_ones_report_id_scheduled_date",
                table: "one_on_ones",
                columns: new[] { "report_id", "scheduled_date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "one_on_one_action_items");

            migrationBuilder.DropTable(
                name: "one_on_ones");
        }
    }
}
