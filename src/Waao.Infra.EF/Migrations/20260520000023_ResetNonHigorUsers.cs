using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Waao.Infra.EF.Migrations
{
    /// <inheritdoc />
    public partial class ResetNonHigorUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Operational reset — wipes derived competências (radar feed) and removes any
            // self-registered users that accumulated after the manual XP economy reset.
            // Idempotent: re-running on an already-clean DB is a no-op (deletes 0 rows,
            // updates the higor row to the same zero state).
            migrationBuilder.Sql("DELETE FROM xp_transactions;");
            migrationBuilder.Sql("DELETE FROM collaborator_badges;");
            migrationBuilder.Sql("UPDATE collaborators SET is_deleted = true, deleted_at = now(), updated_at = now() WHERE lower(email) <> 'higor@waao.com.br' AND is_deleted = false;");
            migrationBuilder.Sql("UPDATE collaborators SET total_xp = 0, current_level = 0, updated_at = now() WHERE lower(email) = 'higor@waao.com.br';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data reset (XP wipe, badge purge, user soft-delete) is intentionally NOT reversible.
        }
    }
}
