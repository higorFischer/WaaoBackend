using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Waao.Infra.EF.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectDepartment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "department_id",
                table: "projects",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_projects_department_id",
                table: "projects",
                column: "department_id");

            migrationBuilder.AddForeignKey(
                name: "fk_projects_departments_department_id",
                table: "projects",
                column: "department_id",
                principalTable: "departments",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_projects_departments_department_id",
                table: "projects");

            migrationBuilder.DropIndex(
                name: "ix_projects_department_id",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "department_id",
                table: "projects");
        }
    }
}
