using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Waao.Infra.EF.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// Phase 1 of the multi-tenancy rollout:
    ///  1. Seeds the two known tenants with stable IDs (see TenantConstants.cs).
    ///  2. Backfills every existing row to the WAAO tenant so nothing is orphaned.
    ///
    /// Idempotent: re-running is safe (ON CONFLICT + WHERE tenant_id IS NULL guards).
    /// Down() removes only the seeded tenant rows; columns + index teardown belongs
    /// to the preceding Phase1_AddTenancy migration.
    /// </summary>
    public partial class Phase1_SeedTenantsAndBackfill : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                INSERT INTO tenants (id, name, slug, accent_color_hex, is_active, created_at, is_deleted)
                VALUES
                    ('00000000-0000-0000-0000-00000000a0a0', 'WAAO',    'waao',    '#2A6B7E', TRUE, NOW(), FALSE),
                    ('00000000-0000-0000-0000-00000000b0b0', 'Liberty', 'liberty', '#0EA5E9', TRUE, NOW(), FALSE)
                ON CONFLICT (id) DO NOTHING;

                UPDATE announcement_targets         SET tenant_id = '00000000-0000-0000-0000-00000000a0a0' WHERE tenant_id IS NULL;
                UPDATE announcements                SET tenant_id = '00000000-0000-0000-0000-00000000a0a0' WHERE tenant_id IS NULL;
                UPDATE badges                       SET tenant_id = '00000000-0000-0000-0000-00000000a0a0' WHERE tenant_id IS NULL;
                UPDATE board_columns                SET tenant_id = '00000000-0000-0000-0000-00000000a0a0' WHERE tenant_id IS NULL;
                UPDATE board_members                SET tenant_id = '00000000-0000-0000-0000-00000000a0a0' WHERE tenant_id IS NULL;
                UPDATE boards                       SET tenant_id = '00000000-0000-0000-0000-00000000a0a0' WHERE tenant_id IS NULL;
                UPDATE calendar_events              SET tenant_id = '00000000-0000-0000-0000-00000000a0a0' WHERE tenant_id IS NULL;
                UPDATE calendars                    SET tenant_id = '00000000-0000-0000-0000-00000000a0a0' WHERE tenant_id IS NULL;
                UPDATE call_channels                SET tenant_id = '00000000-0000-0000-0000-00000000a0a0' WHERE tenant_id IS NULL;
                UPDATE card_activities              SET tenant_id = '00000000-0000-0000-0000-00000000a0a0' WHERE tenant_id IS NULL;
                UPDATE card_checklist_items         SET tenant_id = '00000000-0000-0000-0000-00000000a0a0' WHERE tenant_id IS NULL;
                UPDATE card_checklists              SET tenant_id = '00000000-0000-0000-0000-00000000a0a0' WHERE tenant_id IS NULL;
                UPDATE card_comments                SET tenant_id = '00000000-0000-0000-0000-00000000a0a0' WHERE tenant_id IS NULL;
                UPDATE card_label_maps              SET tenant_id = '00000000-0000-0000-0000-00000000a0a0' WHERE tenant_id IS NULL;
                UPDATE card_labels                  SET tenant_id = '00000000-0000-0000-0000-00000000a0a0' WHERE tenant_id IS NULL;
                UPDATE cards                        SET tenant_id = '00000000-0000-0000-0000-00000000a0a0' WHERE tenant_id IS NULL;
                UPDATE career_events                SET tenant_id = '00000000-0000-0000-0000-00000000a0a0' WHERE tenant_id IS NULL;
                UPDATE challenge_attempt_answers    SET tenant_id = '00000000-0000-0000-0000-00000000a0a0' WHERE tenant_id IS NULL;
                UPDATE challenge_attempts           SET tenant_id = '00000000-0000-0000-0000-00000000a0a0' WHERE tenant_id IS NULL;
                UPDATE challenge_questions          SET tenant_id = '00000000-0000-0000-0000-00000000a0a0' WHERE tenant_id IS NULL;
                UPDATE challenges                   SET tenant_id = '00000000-0000-0000-0000-00000000a0a0' WHERE tenant_id IS NULL;
                UPDATE channel_members              SET tenant_id = '00000000-0000-0000-0000-00000000a0a0' WHERE tenant_id IS NULL;
                UPDATE channels                     SET tenant_id = '00000000-0000-0000-0000-00000000a0a0' WHERE tenant_id IS NULL;
                UPDATE collaborator_badges          SET tenant_id = '00000000-0000-0000-0000-00000000a0a0' WHERE tenant_id IS NULL;
                UPDATE collaborators                SET tenant_id = '00000000-0000-0000-0000-00000000a0a0' WHERE tenant_id IS NULL;
                UPDATE course_completions           SET tenant_id = '00000000-0000-0000-0000-00000000a0a0' WHERE tenant_id IS NULL;
                UPDATE courses                      SET tenant_id = '00000000-0000-0000-0000-00000000a0a0' WHERE tenant_id IS NULL;
                UPDATE departments                  SET tenant_id = '00000000-0000-0000-0000-00000000a0a0' WHERE tenant_id IS NULL;
                UPDATE epics                        SET tenant_id = '00000000-0000-0000-0000-00000000a0a0' WHERE tenant_id IS NULL;
                UPDATE event_occurrence_overrides   SET tenant_id = '00000000-0000-0000-0000-00000000a0a0' WHERE tenant_id IS NULL;
                UPDATE feature_request_comments     SET tenant_id = '00000000-0000-0000-0000-00000000a0a0' WHERE tenant_id IS NULL;
                UPDATE feature_request_votes        SET tenant_id = '00000000-0000-0000-0000-00000000a0a0' WHERE tenant_id IS NULL;
                UPDATE feature_requests             SET tenant_id = '00000000-0000-0000-0000-00000000a0a0' WHERE tenant_id IS NULL;
                UPDATE feedback                     SET tenant_id = '00000000-0000-0000-0000-00000000a0a0' WHERE tenant_id IS NULL;
                UPDATE kudo_recipients              SET tenant_id = '00000000-0000-0000-0000-00000000a0a0' WHERE tenant_id IS NULL;
                UPDATE kudos                        SET tenant_id = '00000000-0000-0000-0000-00000000a0a0' WHERE tenant_id IS NULL;
                UPDATE level_definitions            SET tenant_id = '00000000-0000-0000-0000-00000000a0a0' WHERE tenant_id IS NULL;
                UPDATE meeting_agenda_items         SET tenant_id = '00000000-0000-0000-0000-00000000a0a0' WHERE tenant_id IS NULL;
                UPDATE meeting_attendees            SET tenant_id = '00000000-0000-0000-0000-00000000a0a0' WHERE tenant_id IS NULL;
                UPDATE meeting_transcript_lines     SET tenant_id = '00000000-0000-0000-0000-00000000a0a0' WHERE tenant_id IS NULL;
                UPDATE meeting_transcripts          SET tenant_id = '00000000-0000-0000-0000-00000000a0a0' WHERE tenant_id IS NULL;
                UPDATE meetings                     SET tenant_id = '00000000-0000-0000-0000-00000000a0a0' WHERE tenant_id IS NULL;
                UPDATE message_attachments          SET tenant_id = '00000000-0000-0000-0000-00000000a0a0' WHERE tenant_id IS NULL;
                UPDATE message_mentions             SET tenant_id = '00000000-0000-0000-0000-00000000a0a0' WHERE tenant_id IS NULL;
                UPDATE messages                     SET tenant_id = '00000000-0000-0000-0000-00000000a0a0' WHERE tenant_id IS NULL;
                UPDATE notifications                SET tenant_id = '00000000-0000-0000-0000-00000000a0a0' WHERE tenant_id IS NULL;
                UPDATE one_on_one_action_items      SET tenant_id = '00000000-0000-0000-0000-00000000a0a0' WHERE tenant_id IS NULL;
                UPDATE one_on_ones                  SET tenant_id = '00000000-0000-0000-0000-00000000a0a0' WHERE tenant_id IS NULL;
                UPDATE peer_feedbacks               SET tenant_id = '00000000-0000-0000-0000-00000000a0a0' WHERE tenant_id IS NULL;
                UPDATE project_allocation_events    SET tenant_id = '00000000-0000-0000-0000-00000000a0a0' WHERE tenant_id IS NULL;
                UPDATE project_allocations          SET tenant_id = '00000000-0000-0000-0000-00000000a0a0' WHERE tenant_id IS NULL;
                UPDATE project_connections          SET tenant_id = '00000000-0000-0000-0000-00000000a0a0' WHERE tenant_id IS NULL;
                UPDATE projects                     SET tenant_id = '00000000-0000-0000-0000-00000000a0a0' WHERE tenant_id IS NULL;
                UPDATE push_subscriptions           SET tenant_id = '00000000-0000-0000-0000-00000000a0a0' WHERE tenant_id IS NULL;
                UPDATE roles                        SET tenant_id = '00000000-0000-0000-0000-00000000a0a0' WHERE tenant_id IS NULL;
                UPDATE time_off_requests            SET tenant_id = '00000000-0000-0000-0000-00000000a0a0' WHERE tenant_id IS NULL;
                UPDATE weekly_focus_goals           SET tenant_id = '00000000-0000-0000-0000-00000000a0a0' WHERE tenant_id IS NULL;
                UPDATE weekly_focus_projects        SET tenant_id = '00000000-0000-0000-0000-00000000a0a0' WHERE tenant_id IS NULL;
                UPDATE weekly_focuses               SET tenant_id = '00000000-0000-0000-0000-00000000a0a0' WHERE tenant_id IS NULL;
                UPDATE xp_transactions              SET tenant_id = '00000000-0000-0000-0000-00000000a0a0' WHERE tenant_id IS NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DELETE FROM tenants WHERE id IN (
                    '00000000-0000-0000-0000-00000000a0a0',
                    '00000000-0000-0000-0000-00000000b0b0'
                );
            ");
        }
    }
}
