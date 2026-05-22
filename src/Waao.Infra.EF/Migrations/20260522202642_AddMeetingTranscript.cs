using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Waao.Infra.EF.Migrations
{
    /// <inheritdoc />
    public partial class AddMeetingTranscript : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "meeting_transcripts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    meeting_id = table.Column<Guid>(type: "uuid", nullable: false),
                    generated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_meeting_transcripts", x => x.id);
                    table.ForeignKey(
                        name: "fk_meeting_transcripts_meetings_meeting_id",
                        column: x => x.meeting_id,
                        principalTable: "meetings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "meeting_transcript_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    transcript_id = table.Column<Guid>(type: "uuid", nullable: false),
                    speaker_collaborator_id = table.Column<Guid>(type: "uuid", nullable: true),
                    speaker_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    offset_seconds = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_meeting_transcript_lines", x => x.id);
                    table.ForeignKey(
                        name: "fk_meeting_transcript_lines_collaborators_speaker_collaborator",
                        column: x => x.speaker_collaborator_id,
                        principalTable: "collaborators",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_meeting_transcript_lines_meeting_transcripts_transcript_id",
                        column: x => x.transcript_id,
                        principalTable: "meeting_transcripts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_meeting_transcript_lines_speaker_collaborator_id",
                table: "meeting_transcript_lines",
                column: "speaker_collaborator_id");

            migrationBuilder.CreateIndex(
                name: "ix_meeting_transcript_lines_transcript_id",
                table: "meeting_transcript_lines",
                column: "transcript_id");

            migrationBuilder.CreateIndex(
                name: "ix_meeting_transcripts_meeting_id",
                table: "meeting_transcripts",
                column: "meeting_id",
                unique: true,
                filter: "is_deleted = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "meeting_transcript_lines");

            migrationBuilder.DropTable(
                name: "meeting_transcripts");
        }
    }
}
