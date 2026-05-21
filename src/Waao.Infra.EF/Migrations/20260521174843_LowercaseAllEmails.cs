using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Waao.Infra.EF.Migrations
{
    /// <inheritdoc />
    public partial class LowercaseAllEmails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // One-shot normalization: every collaborator email is stored lowercase.
            // Application code lowercases on write (AuthService, AdminService) — this catches
            // any drift from older rows. Idempotent: re-running does nothing when already lower.
            migrationBuilder.Sql("UPDATE collaborators SET email = lower(email) WHERE email <> lower(email);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Case-folding is one-way; no down migration.
        }
    }
}
