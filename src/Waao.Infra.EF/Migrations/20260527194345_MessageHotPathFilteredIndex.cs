using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Waao.Infra.EF.Migrations
{
    /// <inheritdoc />
    public partial class MessageHotPathFilteredIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_messages_channel_id_created_at",
                table: "messages");

            migrationBuilder.CreateIndex(
                name: "ix_messages_channel_id_created_at",
                table: "messages",
                columns: new[] { "channel_id", "created_at" },
                filter: "is_deleted = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_messages_channel_id_created_at",
                table: "messages");

            migrationBuilder.CreateIndex(
                name: "ix_messages_channel_id_created_at",
                table: "messages",
                columns: new[] { "channel_id", "created_at" });
        }
    }
}
