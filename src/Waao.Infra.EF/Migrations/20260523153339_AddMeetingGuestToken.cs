using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Waao.Infra.EF.Migrations
{
    /// <inheritdoc />
    public partial class AddMeetingGuestToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "guest_token",
                table: "meetings",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            // Backfill: assign a unique opaque token to every existing meeting row.
            // Uses two UUID v4s stripped of dashes — at least 32 chars, guaranteed unique.
            migrationBuilder.Sql(
                @"UPDATE meetings SET guest_token = replace(gen_random_uuid()::text, '-', '') || replace(gen_random_uuid()::text, '-', '') WHERE guest_token = '';");

            // Drop the column default so new rows must supply a value explicitly.
            migrationBuilder.AlterColumn<string>(
                name: "guest_token",
                table: "meetings",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldDefaultValue: "");

            migrationBuilder.CreateIndex(
                name: "ix_meetings_guest_token",
                table: "meetings",
                column: "guest_token",
                unique: true,
                filter: "is_deleted = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_meetings_guest_token",
                table: "meetings");

            migrationBuilder.DropColumn(
                name: "guest_token",
                table: "meetings");
        }
    }
}
