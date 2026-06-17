using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Waao.Infra.EF.Migrations
{
    /// <inheritdoc />
    public partial class AddDesignFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "design_flows",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_design_flows", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "design_steps",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    flow_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    position_x = table.Column<double>(type: "double precision", nullable: false),
                    position_y = table.Column<double>(type: "double precision", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_design_steps", x => x.id);
                    table.ForeignKey(
                        name: "fk_design_steps_design_flows_flow_id",
                        column: x => x.flow_id,
                        principalTable: "design_flows",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "design_assets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    step_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    content_type = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    r2key = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    show_full_by_default = table.Column<bool>(type: "boolean", nullable: false),
                    uploaded_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_design_assets", x => x.id);
                    table.ForeignKey(
                        name: "fk_design_assets_design_steps_step_id",
                        column: x => x.step_id,
                        principalTable: "design_steps",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "design_step_edges",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    flow_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_step_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_step_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_design_step_edges", x => x.id);
                    table.ForeignKey(
                        name: "fk_design_step_edges_design_flows_flow_id",
                        column: x => x.flow_id,
                        principalTable: "design_flows",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_design_step_edges_design_steps_source_step_id",
                        column: x => x.source_step_id,
                        principalTable: "design_steps",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_design_step_edges_design_steps_target_step_id",
                        column: x => x.target_step_id,
                        principalTable: "design_steps",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_design_assets_step_id",
                table: "design_assets",
                column: "step_id");

            migrationBuilder.CreateIndex(
                name: "ix_design_assets_tenant_id",
                table: "design_assets",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_design_flows_status_is_deleted",
                table: "design_flows",
                columns: new[] { "status", "is_deleted" });

            migrationBuilder.CreateIndex(
                name: "ix_design_flows_tenant_id",
                table: "design_flows",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_design_step_edges_flow_id",
                table: "design_step_edges",
                column: "flow_id");

            migrationBuilder.CreateIndex(
                name: "ix_design_step_edges_flow_id_source_step_id_target_step_id",
                table: "design_step_edges",
                columns: new[] { "flow_id", "source_step_id", "target_step_id" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_design_step_edges_source_step_id",
                table: "design_step_edges",
                column: "source_step_id");

            migrationBuilder.CreateIndex(
                name: "ix_design_step_edges_target_step_id",
                table: "design_step_edges",
                column: "target_step_id");

            migrationBuilder.CreateIndex(
                name: "ix_design_step_edges_tenant_id",
                table: "design_step_edges",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_design_steps_flow_id",
                table: "design_steps",
                column: "flow_id");

            migrationBuilder.CreateIndex(
                name: "ix_design_steps_tenant_id",
                table: "design_steps",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "design_assets");

            migrationBuilder.DropTable(
                name: "design_step_edges");

            migrationBuilder.DropTable(
                name: "design_steps");

            migrationBuilder.DropTable(
                name: "design_flows");
        }
    }
}
