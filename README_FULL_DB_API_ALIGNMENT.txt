DBiz Planner - Full DB + API Alignment Patch
Generated: 27 Apr 2026

Main fix:
- Adds missing planner_task columns used by PlannerApi and Dbiz.MailboxWorker:
  seniority_level, internal_notes, vendor_comment, contact_phone, secondary_skills_json,
  experience_required, location, work_mode, employment_type, recruiter_override_comment.
- Keeps old notes column and new internal_notes column synced for existing rows.
- Adds planner_contact for split/multiple contacts.
- Adds planner_vendor_assignment and planner_candidate_submission if missing.
- Adds vendor UEN/POC/location columns.
- Adds AuthRepository-compatible user_role_master/app_user columns.
- Adds MailboxWorker support tables planner, planner_email, planner_activity, planner_attachment.

How to run:
1. Back up your PostgreSQL database.
2. Open pgAdmin Query Tool on your Planner DB.
3. Run:
   PlannerApi/Sql/20260427_full_db_api_alignment.sql
4. Restart PlannerApi.
5. Refresh UI.

This script is idempotent and uses IF NOT EXISTS where possible.
