using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Waao.Infra.EF.Migrations
{
    /// <inheritdoc />
    public partial class AddCoursesAndChallenges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "challenges",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    category = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    suggested_xp = table.Column<int>(type: "integer", nullable: true),
                    pass_percent = table.Column<int>(type: "integer", nullable: false, defaultValue: 70),
                    is_published = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_challenges", x => x.id);
                    table.ForeignKey(
                        name: "fk_challenges_collaborators_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "collaborators",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "courses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    provider = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    material_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    duration_minutes = table.Column<int>(type: "integer", nullable: true),
                    suggested_xp = table.Column<int>(type: "integer", nullable: true),
                    category = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    is_published = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_courses", x => x.id);
                    table.ForeignKey(
                        name: "fk_courses_collaborators_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "collaborators",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "challenge_attempts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    challenge_id = table.Column<Guid>(type: "uuid", nullable: false),
                    collaborator_id = table.Column<Guid>(type: "uuid", nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    submitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    score_pct = table.Column<int>(type: "integer", nullable: false),
                    passed = table.Column<bool>(type: "boolean", nullable: false),
                    xp_awarded = table.Column<int>(type: "integer", nullable: true),
                    xp_awarded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xp_awarded_by_admin_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_challenge_attempts", x => x.id);
                    table.ForeignKey(
                        name: "fk_challenge_attempts_challenges_challenge_id",
                        column: x => x.challenge_id,
                        principalTable: "challenges",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_challenge_attempts_collaborators_collaborator_id",
                        column: x => x.collaborator_id,
                        principalTable: "collaborators",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "challenge_questions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    challenge_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order = table.Column<int>(type: "integer", nullable: false),
                    prompt = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    option_a = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    option_b = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    option_c = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    option_d = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    correct_option = table.Column<char>(type: "char(1)", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_challenge_questions", x => x.id);
                    table.ForeignKey(
                        name: "fk_challenge_questions_challenges_challenge_id",
                        column: x => x.challenge_id,
                        principalTable: "challenges",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "course_completions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    course_id = table.Column<Guid>(type: "uuid", nullable: false),
                    collaborator_id = table.Column<Guid>(type: "uuid", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xp_awarded = table.Column<int>(type: "integer", nullable: true),
                    xp_awarded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xp_awarded_by_admin_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_course_completions", x => x.id);
                    table.ForeignKey(
                        name: "fk_course_completions_collaborators_collaborator_id",
                        column: x => x.collaborator_id,
                        principalTable: "collaborators",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_course_completions_courses_course_id",
                        column: x => x.course_id,
                        principalTable: "courses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "challenge_attempt_answers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    attempt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    selected_option = table.Column<char>(type: "char(1)", nullable: false),
                    is_correct = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_challenge_attempt_answers", x => x.id);
                    table.ForeignKey(
                        name: "fk_challenge_attempt_answers_challenge_attempts_attempt_id",
                        column: x => x.attempt_id,
                        principalTable: "challenge_attempts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_challenge_attempt_answers_challenge_questions_question_id",
                        column: x => x.question_id,
                        principalTable: "challenge_questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_challenge_attempt_answers_attempt_id",
                table: "challenge_attempt_answers",
                column: "attempt_id");

            migrationBuilder.CreateIndex(
                name: "ix_challenge_attempt_answers_question_id",
                table: "challenge_attempt_answers",
                column: "question_id");

            migrationBuilder.CreateIndex(
                name: "ix_challenge_attempts_challenge_id",
                table: "challenge_attempts",
                column: "challenge_id");

            migrationBuilder.CreateIndex(
                name: "ix_challenge_attempts_collaborator_id_challenge_id",
                table: "challenge_attempts",
                columns: new[] { "collaborator_id", "challenge_id" });

            migrationBuilder.CreateIndex(
                name: "ix_challenge_attempts_collaborator_id_is_deleted",
                table: "challenge_attempts",
                columns: new[] { "collaborator_id", "is_deleted" });

            migrationBuilder.CreateIndex(
                name: "ix_challenge_attempts_submitted_at_passed",
                table: "challenge_attempts",
                columns: new[] { "submitted_at", "passed" },
                filter: "passed = true AND xp_awarded_at IS NULL AND is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_challenge_questions_challenge_id",
                table: "challenge_questions",
                column: "challenge_id");

            migrationBuilder.CreateIndex(
                name: "ix_challenge_questions_challenge_id_order",
                table: "challenge_questions",
                columns: new[] { "challenge_id", "order" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_challenges_category",
                table: "challenges",
                column: "category");

            migrationBuilder.CreateIndex(
                name: "ix_challenges_created_by_id",
                table: "challenges",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_challenges_is_published_is_deleted",
                table: "challenges",
                columns: new[] { "is_published", "is_deleted" });

            migrationBuilder.CreateIndex(
                name: "ix_course_completions_collaborator_id_is_deleted",
                table: "course_completions",
                columns: new[] { "collaborator_id", "is_deleted" });

            migrationBuilder.CreateIndex(
                name: "ix_course_completions_course_id",
                table: "course_completions",
                column: "course_id");

            migrationBuilder.CreateIndex(
                name: "ix_course_completions_course_id_collaborator_id",
                table: "course_completions",
                columns: new[] { "course_id", "collaborator_id" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_course_completions_xp_awarded_at",
                table: "course_completions",
                column: "xp_awarded_at",
                filter: "xp_awarded_at IS NULL AND is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_courses_category",
                table: "courses",
                column: "category");

            migrationBuilder.CreateIndex(
                name: "ix_courses_created_by_id",
                table: "courses",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_courses_is_published_is_deleted",
                table: "courses",
                columns: new[] { "is_published", "is_deleted" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "challenge_attempt_answers");

            migrationBuilder.DropTable(
                name: "course_completions");

            migrationBuilder.DropTable(
                name: "challenge_attempts");

            migrationBuilder.DropTable(
                name: "challenge_questions");

            migrationBuilder.DropTable(
                name: "courses");

            migrationBuilder.DropTable(
                name: "challenges");
        }
    }
}
