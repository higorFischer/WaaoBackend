using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Waao.Infra.EF.Migrations
{
    /// <inheritdoc />
    public partial class AddCalendar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "calendars",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    color_hex = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false, defaultValue: "#2A6B7E"),
                    scope = table.Column<string>(type: "text", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    department_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_calendars", x => x.id);
                    table.ForeignKey(
                        name: "fk_calendars_collaborators_owner_id",
                        column: x => x.owner_id,
                        principalTable: "collaborators",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_calendars_departments_department_id",
                        column: x => x.department_id,
                        principalTable: "departments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "calendar_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    calendar_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    starts_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ends_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_all_day = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    color_hex = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: true),
                    recurrence_rule = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    recurrence_end_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_calendar_events", x => x.id);
                    table.ForeignKey(
                        name: "fk_calendar_events_calendars_calendar_id",
                        column: x => x.calendar_id,
                        principalTable: "calendars",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_calendar_events_collaborators_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "collaborators",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "event_occurrence_overrides",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    original_start_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_cancelled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    starts_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ends_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_all_day = table.Column<bool>(type: "boolean", nullable: true),
                    color_hex = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_event_occurrence_overrides", x => x.id);
                    table.ForeignKey(
                        name: "fk_event_occurrence_overrides_calendar_events_event_id",
                        column: x => x.event_id,
                        principalTable: "calendar_events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_calendar_events_calendar_id_starts_at_utc_is_deleted",
                table: "calendar_events",
                columns: new[] { "calendar_id", "starts_at_utc", "is_deleted" });

            migrationBuilder.CreateIndex(
                name: "ix_calendar_events_created_by_id",
                table: "calendar_events",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_calendar_events_recurrence_rule",
                table: "calendar_events",
                column: "recurrence_rule",
                filter: "recurrence_rule IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_calendars_department_id",
                table: "calendars",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "ix_calendars_owner_id",
                table: "calendars",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_calendars_scope_is_deleted",
                table: "calendars",
                columns: new[] { "scope", "is_deleted" });

            migrationBuilder.CreateIndex(
                name: "ix_event_occurrence_overrides_event_id",
                table: "event_occurrence_overrides",
                column: "event_id");

            migrationBuilder.CreateIndex(
                name: "ix_event_occurrence_overrides_event_id_original_start_utc",
                table: "event_occurrence_overrides",
                columns: new[] { "event_id", "original_start_utc" },
                unique: true,
                filter: "is_deleted = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "event_occurrence_overrides");

            migrationBuilder.DropTable(
                name: "calendar_events");

            migrationBuilder.DropTable(
                name: "calendars");
        }
    }
}
