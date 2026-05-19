using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Waao.Infra.EF.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "email_verification_token",
                table: "collaborators",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "email_verification_token_expires_at",
                table: "collaborators",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "email_verified",
                table: "collaborators",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "email_verified_at",
                table: "collaborators",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_verification_email_sent_at",
                table: "collaborators",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_collaborators_email_verification_token",
                table: "collaborators",
                column: "email_verification_token",
                filter: "email_verification_token IS NOT NULL");

            // Existing rows predate verification — grandfather them as verified so no one is locked out.
            migrationBuilder.Sql("UPDATE collaborators SET email_verified = true WHERE email_verified = false;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data backfill (grandfathering existing users verified) is not reversed.
            migrationBuilder.DropIndex(
                name: "ix_collaborators_email_verification_token",
                table: "collaborators");

            migrationBuilder.DropColumn(
                name: "email_verification_token",
                table: "collaborators");

            migrationBuilder.DropColumn(
                name: "email_verification_token_expires_at",
                table: "collaborators");

            migrationBuilder.DropColumn(
                name: "email_verified",
                table: "collaborators");

            migrationBuilder.DropColumn(
                name: "email_verified_at",
                table: "collaborators");

            migrationBuilder.DropColumn(
                name: "last_verification_email_sent_at",
                table: "collaborators");
        }
    }
}
