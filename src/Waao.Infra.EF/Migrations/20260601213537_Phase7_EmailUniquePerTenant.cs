using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Waao.Infra.EF.Migrations
{
    /// <inheritdoc />
    public partial class Phase7_EmailUniquePerTenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_collaborators_email",
                table: "collaborators");

            migrationBuilder.CreateIndex(
                name: "ix_collaborators_email_tenant_id",
                table: "collaborators",
                columns: new[] { "email", "tenant_id" },
                unique: true,
                filter: "is_deleted = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_collaborators_email_tenant_id",
                table: "collaborators");

            migrationBuilder.CreateIndex(
                name: "ix_collaborators_email",
                table: "collaborators",
                column: "email",
                unique: true,
                filter: "is_deleted = false");
        }
    }
}
