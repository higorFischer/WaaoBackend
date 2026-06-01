using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Waao.Infra.EF.Migrations
{
    /// <inheritdoc />
    public partial class AddBoardProject : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "project_id",
                table: "boards",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_boards_project_id",
                table: "boards",
                column: "project_id");

            migrationBuilder.AddForeignKey(
                name: "fk_boards_projects_project_id",
                table: "boards",
                column: "project_id",
                principalTable: "projects",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_boards_projects_project_id",
                table: "boards");

            migrationBuilder.DropIndex(
                name: "ix_boards_project_id",
                table: "boards");

            migrationBuilder.DropColumn(
                name: "project_id",
                table: "boards");
        }
    }
}
