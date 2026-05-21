using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Waao.Infra.EF.Migrations
{
    /// <inheritdoc />
    public partial class EnsureHigorSuperAdmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Force-fix the super admin row.
            // Idempotent: re-running on an already-correct row is a no-op (rows match → 0 affected).
            // Status is stored as string (HasConversion<string>); RoleKind likewise.
            migrationBuilder.Sql(@"
                UPDATE collaborators
                SET role_kind        = 'Admin',
                    status           = 'Active',
                    is_deleted       = false,
                    deleted_at       = NULL,
                    email_verified   = true,
                    email_verified_at = COALESCE(email_verified_at, now()),
                    onboarding_completed_at = COALESCE(onboarding_completed_at, now()),
                    updated_at       = now()
                WHERE lower(email) = 'higor@waao.com.br';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally non-reversible — super admin enforcement is a one-way safety net.
        }
    }
}
