using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Waao.Infra.EF.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectParent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "parent_project_id",
                table: "projects",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_projects_parent_project_id",
                table: "projects",
                column: "parent_project_id");

            migrationBuilder.AddForeignKey(
                name: "fk_projects_projects_parent_project_id",
                table: "projects",
                column: "parent_project_id",
                principalTable: "projects",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_projects_projects_parent_project_id",
                table: "projects");

            migrationBuilder.DropIndex(
                name: "ix_projects_parent_project_id",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "parent_project_id",
                table: "projects");
        }
    }
}
