using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Waao.Infra.EF.Migrations
{
    /// <inheritdoc />
    public partial class AddRecordingStartedAtToTranscriptLine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "recording_started_at_utc",
                table: "meeting_transcript_lines",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_meeting_transcript_lines_transcript_id_recording_started_at",
                table: "meeting_transcript_lines",
                columns: new[] { "transcript_id", "recording_started_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_meeting_transcript_lines_transcript_id_recording_started_at",
                table: "meeting_transcript_lines");

            migrationBuilder.DropColumn(
                name: "recording_started_at_utc",
                table: "meeting_transcript_lines");
        }
    }
}
