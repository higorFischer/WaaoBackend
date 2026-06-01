using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Waao.Infra.EF.Migrations
{
    /// <inheritdoc />
    public partial class Phase1_AddTenancy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "xp_transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "weekly_focuses",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "weekly_focus_projects",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "weekly_focus_goals",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "time_off_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "roles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "push_subscriptions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "projects",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "project_connections",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "project_allocations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "project_allocation_events",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "peer_feedbacks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "one_on_ones",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "one_on_one_action_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "notifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "messages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "message_mentions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "message_attachments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "meetings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "meeting_transcripts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "meeting_transcript_lines",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "meeting_attendees",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "meeting_agenda_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "level_definitions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "kudos",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "kudo_recipients",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "feedback",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "feature_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "feature_request_votes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "feature_request_comments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "event_occurrence_overrides",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "epics",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "departments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "courses",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "course_completions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "collaborators",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "collaborator_badges",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "channels",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "channel_members",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "challenges",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "challenge_questions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "challenge_attempts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "challenge_attempt_answers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "career_events",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "cards",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "card_labels",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "card_label_maps",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "card_comments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "card_checklists",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "card_checklist_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "card_activities",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "call_channels",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "calendars",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "calendar_events",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "boards",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "board_members",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "board_columns",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "badges",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "announcements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "announcement_targets",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "tenants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    slug = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    logo_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    accent_color_hex = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenants", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_xp_transactions_tenant_id",
                table: "xp_transactions",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_weekly_focuses_tenant_id",
                table: "weekly_focuses",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_weekly_focus_projects_tenant_id",
                table: "weekly_focus_projects",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_weekly_focus_goals_tenant_id",
                table: "weekly_focus_goals",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_time_off_requests_tenant_id",
                table: "time_off_requests",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_roles_tenant_id",
                table: "roles",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_push_subscriptions_tenant_id",
                table: "push_subscriptions",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_projects_tenant_id",
                table: "projects",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_connections_tenant_id",
                table: "project_connections",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_allocations_tenant_id",
                table: "project_allocations",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_allocation_events_tenant_id",
                table: "project_allocation_events",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_peer_feedbacks_tenant_id",
                table: "peer_feedbacks",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_one_on_ones_tenant_id",
                table: "one_on_ones",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_one_on_one_action_items_tenant_id",
                table: "one_on_one_action_items",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_tenant_id",
                table: "notifications",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_messages_tenant_id",
                table: "messages",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_message_mentions_tenant_id",
                table: "message_mentions",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_message_attachments_tenant_id",
                table: "message_attachments",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_meetings_tenant_id",
                table: "meetings",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_meeting_transcripts_tenant_id",
                table: "meeting_transcripts",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_meeting_transcript_lines_tenant_id",
                table: "meeting_transcript_lines",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_meeting_attendees_tenant_id",
                table: "meeting_attendees",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_meeting_agenda_items_tenant_id",
                table: "meeting_agenda_items",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_level_definitions_tenant_id",
                table: "level_definitions",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_kudos_tenant_id",
                table: "kudos",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_kudo_recipients_tenant_id",
                table: "kudo_recipients",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_feedback_tenant_id",
                table: "feedback",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_feature_requests_tenant_id",
                table: "feature_requests",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_feature_request_votes_tenant_id",
                table: "feature_request_votes",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_feature_request_comments_tenant_id",
                table: "feature_request_comments",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_event_occurrence_overrides_tenant_id",
                table: "event_occurrence_overrides",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_epics_tenant_id",
                table: "epics",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_departments_tenant_id",
                table: "departments",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_courses_tenant_id",
                table: "courses",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_course_completions_tenant_id",
                table: "course_completions",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_collaborators_tenant_id",
                table: "collaborators",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_collaborator_badges_tenant_id",
                table: "collaborator_badges",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_channels_tenant_id",
                table: "channels",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_channel_members_tenant_id",
                table: "channel_members",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_challenges_tenant_id",
                table: "challenges",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_challenge_questions_tenant_id",
                table: "challenge_questions",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_challenge_attempts_tenant_id",
                table: "challenge_attempts",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_challenge_attempt_answers_tenant_id",
                table: "challenge_attempt_answers",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_career_events_tenant_id",
                table: "career_events",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_cards_tenant_id",
                table: "cards",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_card_labels_tenant_id",
                table: "card_labels",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_card_label_maps_tenant_id",
                table: "card_label_maps",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_card_comments_tenant_id",
                table: "card_comments",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_card_checklists_tenant_id",
                table: "card_checklists",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_card_checklist_items_tenant_id",
                table: "card_checklist_items",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_card_activities_tenant_id",
                table: "card_activities",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_call_channels_tenant_id",
                table: "call_channels",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_calendars_tenant_id",
                table: "calendars",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_calendar_events_tenant_id",
                table: "calendar_events",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_boards_tenant_id",
                table: "boards",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_board_members_tenant_id",
                table: "board_members",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_board_columns_tenant_id",
                table: "board_columns",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_badges_tenant_id",
                table: "badges",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_announcements_tenant_id",
                table: "announcements",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_announcement_targets_tenant_id",
                table: "announcement_targets",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_tenants_slug",
                table: "tenants",
                column: "slug",
                unique: true,
                filter: "is_deleted = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tenants");

            migrationBuilder.DropIndex(
                name: "ix_xp_transactions_tenant_id",
                table: "xp_transactions");

            migrationBuilder.DropIndex(
                name: "ix_weekly_focuses_tenant_id",
                table: "weekly_focuses");

            migrationBuilder.DropIndex(
                name: "ix_weekly_focus_projects_tenant_id",
                table: "weekly_focus_projects");

            migrationBuilder.DropIndex(
                name: "ix_weekly_focus_goals_tenant_id",
                table: "weekly_focus_goals");

            migrationBuilder.DropIndex(
                name: "ix_time_off_requests_tenant_id",
                table: "time_off_requests");

            migrationBuilder.DropIndex(
                name: "ix_roles_tenant_id",
                table: "roles");

            migrationBuilder.DropIndex(
                name: "ix_push_subscriptions_tenant_id",
                table: "push_subscriptions");

            migrationBuilder.DropIndex(
                name: "ix_projects_tenant_id",
                table: "projects");

            migrationBuilder.DropIndex(
                name: "ix_project_connections_tenant_id",
                table: "project_connections");

            migrationBuilder.DropIndex(
                name: "ix_project_allocations_tenant_id",
                table: "project_allocations");

            migrationBuilder.DropIndex(
                name: "ix_project_allocation_events_tenant_id",
                table: "project_allocation_events");

            migrationBuilder.DropIndex(
                name: "ix_peer_feedbacks_tenant_id",
                table: "peer_feedbacks");

            migrationBuilder.DropIndex(
                name: "ix_one_on_ones_tenant_id",
                table: "one_on_ones");

            migrationBuilder.DropIndex(
                name: "ix_one_on_one_action_items_tenant_id",
                table: "one_on_one_action_items");

            migrationBuilder.DropIndex(
                name: "ix_notifications_tenant_id",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "ix_messages_tenant_id",
                table: "messages");

            migrationBuilder.DropIndex(
                name: "ix_message_mentions_tenant_id",
                table: "message_mentions");

            migrationBuilder.DropIndex(
                name: "ix_message_attachments_tenant_id",
                table: "message_attachments");

            migrationBuilder.DropIndex(
                name: "ix_meetings_tenant_id",
                table: "meetings");

            migrationBuilder.DropIndex(
                name: "ix_meeting_transcripts_tenant_id",
                table: "meeting_transcripts");

            migrationBuilder.DropIndex(
                name: "ix_meeting_transcript_lines_tenant_id",
                table: "meeting_transcript_lines");

            migrationBuilder.DropIndex(
                name: "ix_meeting_attendees_tenant_id",
                table: "meeting_attendees");

            migrationBuilder.DropIndex(
                name: "ix_meeting_agenda_items_tenant_id",
                table: "meeting_agenda_items");

            migrationBuilder.DropIndex(
                name: "ix_level_definitions_tenant_id",
                table: "level_definitions");

            migrationBuilder.DropIndex(
                name: "ix_kudos_tenant_id",
                table: "kudos");

            migrationBuilder.DropIndex(
                name: "ix_kudo_recipients_tenant_id",
                table: "kudo_recipients");

            migrationBuilder.DropIndex(
                name: "ix_feedback_tenant_id",
                table: "feedback");

            migrationBuilder.DropIndex(
                name: "ix_feature_requests_tenant_id",
                table: "feature_requests");

            migrationBuilder.DropIndex(
                name: "ix_feature_request_votes_tenant_id",
                table: "feature_request_votes");

            migrationBuilder.DropIndex(
                name: "ix_feature_request_comments_tenant_id",
                table: "feature_request_comments");

            migrationBuilder.DropIndex(
                name: "ix_event_occurrence_overrides_tenant_id",
                table: "event_occurrence_overrides");

            migrationBuilder.DropIndex(
                name: "ix_epics_tenant_id",
                table: "epics");

            migrationBuilder.DropIndex(
                name: "ix_departments_tenant_id",
                table: "departments");

            migrationBuilder.DropIndex(
                name: "ix_courses_tenant_id",
                table: "courses");

            migrationBuilder.DropIndex(
                name: "ix_course_completions_tenant_id",
                table: "course_completions");

            migrationBuilder.DropIndex(
                name: "ix_collaborators_tenant_id",
                table: "collaborators");

            migrationBuilder.DropIndex(
                name: "ix_collaborator_badges_tenant_id",
                table: "collaborator_badges");

            migrationBuilder.DropIndex(
                name: "ix_channels_tenant_id",
                table: "channels");

            migrationBuilder.DropIndex(
                name: "ix_channel_members_tenant_id",
                table: "channel_members");

            migrationBuilder.DropIndex(
                name: "ix_challenges_tenant_id",
                table: "challenges");

            migrationBuilder.DropIndex(
                name: "ix_challenge_questions_tenant_id",
                table: "challenge_questions");

            migrationBuilder.DropIndex(
                name: "ix_challenge_attempts_tenant_id",
                table: "challenge_attempts");

            migrationBuilder.DropIndex(
                name: "ix_challenge_attempt_answers_tenant_id",
                table: "challenge_attempt_answers");

            migrationBuilder.DropIndex(
                name: "ix_career_events_tenant_id",
                table: "career_events");

            migrationBuilder.DropIndex(
                name: "ix_cards_tenant_id",
                table: "cards");

            migrationBuilder.DropIndex(
                name: "ix_card_labels_tenant_id",
                table: "card_labels");

            migrationBuilder.DropIndex(
                name: "ix_card_label_maps_tenant_id",
                table: "card_label_maps");

            migrationBuilder.DropIndex(
                name: "ix_card_comments_tenant_id",
                table: "card_comments");

            migrationBuilder.DropIndex(
                name: "ix_card_checklists_tenant_id",
                table: "card_checklists");

            migrationBuilder.DropIndex(
                name: "ix_card_checklist_items_tenant_id",
                table: "card_checklist_items");

            migrationBuilder.DropIndex(
                name: "ix_card_activities_tenant_id",
                table: "card_activities");

            migrationBuilder.DropIndex(
                name: "ix_call_channels_tenant_id",
                table: "call_channels");

            migrationBuilder.DropIndex(
                name: "ix_calendars_tenant_id",
                table: "calendars");

            migrationBuilder.DropIndex(
                name: "ix_calendar_events_tenant_id",
                table: "calendar_events");

            migrationBuilder.DropIndex(
                name: "ix_boards_tenant_id",
                table: "boards");

            migrationBuilder.DropIndex(
                name: "ix_board_members_tenant_id",
                table: "board_members");

            migrationBuilder.DropIndex(
                name: "ix_board_columns_tenant_id",
                table: "board_columns");

            migrationBuilder.DropIndex(
                name: "ix_badges_tenant_id",
                table: "badges");

            migrationBuilder.DropIndex(
                name: "ix_announcements_tenant_id",
                table: "announcements");

            migrationBuilder.DropIndex(
                name: "ix_announcement_targets_tenant_id",
                table: "announcement_targets");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "xp_transactions");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "weekly_focuses");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "weekly_focus_projects");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "weekly_focus_goals");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "time_off_requests");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "push_subscriptions");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "project_connections");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "project_allocations");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "project_allocation_events");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "peer_feedbacks");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "one_on_ones");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "one_on_one_action_items");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "message_mentions");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "message_attachments");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "meetings");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "meeting_transcripts");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "meeting_transcript_lines");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "meeting_attendees");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "meeting_agenda_items");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "level_definitions");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "kudos");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "kudo_recipients");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "feedback");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "feature_requests");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "feature_request_votes");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "feature_request_comments");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "event_occurrence_overrides");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "epics");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "departments");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "courses");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "course_completions");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "collaborators");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "collaborator_badges");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "channels");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "channel_members");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "challenges");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "challenge_questions");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "challenge_attempts");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "challenge_attempt_answers");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "career_events");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "cards");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "card_labels");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "card_label_maps");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "card_comments");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "card_checklists");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "card_checklist_items");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "card_activities");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "call_channels");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "calendars");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "calendar_events");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "boards");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "board_members");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "board_columns");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "badges");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "announcements");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "announcement_targets");
        }
    }
}
