using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Waao.Infra.EF.Migrations
{
    /// <inheritdoc />
    public partial class AddMessageReplyAndEdit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "edited_at_utc",
                table: "messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "parent_message_id",
                table: "messages",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_messages_parent_message_id",
                table: "messages",
                column: "parent_message_id");

            migrationBuilder.AddForeignKey(
                name: "fk_messages_messages_parent_message_id",
                table: "messages",
                column: "parent_message_id",
                principalTable: "messages",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_messages_messages_parent_message_id",
                table: "messages");

            migrationBuilder.DropIndex(
                name: "ix_messages_parent_message_id",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "edited_at_utc",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "parent_message_id",
                table: "messages");
        }
    }
}
