using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Waao.Infra.EF.Migrations
{
    /// <inheritdoc />
    public partial class AddPeerFeedback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "peer_feedbacks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    giver_id = table.Column<Guid>(type: "uuid", nullable: false),
                    giver_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    recipient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    category = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    message = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    is_anonymous = table.Column<bool>(type: "boolean", nullable: false),
                    acknowledged = table.Column<bool>(type: "boolean", nullable: false),
                    acknowledged_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_peer_feedbacks", x => x.id);
                    table.ForeignKey(
                        name: "fk_peer_feedbacks_collaborators_giver_id",
                        column: x => x.giver_id,
                        principalTable: "collaborators",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_peer_feedbacks_collaborators_recipient_id",
                        column: x => x.recipient_id,
                        principalTable: "collaborators",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_peer_feedbacks_giver_id_created_at",
                table: "peer_feedbacks",
                columns: new[] { "giver_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_peer_feedbacks_recipient_id_created_at",
                table: "peer_feedbacks",
                columns: new[] { "recipient_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "peer_feedbacks");
        }
    }
}
