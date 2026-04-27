-- Vendor dashboard/task visibility fix
-- Run this once on the Planner PostgreSQL database.

CREATE TABLE IF NOT EXISTS planner_candidate_submission (
    submission_id SERIAL PRIMARY KEY,
    planner_id INT NOT NULL REFERENCES planner_task(id) ON DELETE CASCADE,
    vendor_id INT NOT NULL REFERENCES vendor(vendor_id),
    candidate_name VARCHAR(255) NOT NULL DEFAULT '',
    contact_detail VARCHAR(255) NOT NULL DEFAULT '',
    visa_type VARCHAR(100) NOT NULL DEFAULT '',
    resume_file VARCHAR(255) NOT NULL DEFAULT '',
    dbiz_resume_file VARCHAR(255) NOT NULL DEFAULT '',
    candidate_status VARCHAR(50) NOT NULL DEFAULT 'Draft',
    is_submitted BOOLEAN NOT NULL DEFAULT FALSE,
    created_on TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_on TIMESTAMP NULL
);

ALTER TABLE planner_candidate_submission ADD COLUMN IF NOT EXISTS dbiz_resume_file VARCHAR(255) NOT NULL DEFAULT '';

ALTER TABLE vendor ADD COLUMN IF NOT EXISTS uen_no VARCHAR(50) NOT NULL DEFAULT '';
ALTER TABLE vendor ADD COLUMN IF NOT EXISTS poc_name VARCHAR(255) NOT NULL DEFAULT '';
ALTER TABLE vendor ADD COLUMN IF NOT EXISTS poc_email VARCHAR(255) NOT NULL DEFAULT '';
ALTER TABLE vendor ADD COLUMN IF NOT EXISTS poc_phone VARCHAR(50) NOT NULL DEFAULT '';
ALTER TABLE vendor ADD COLUMN IF NOT EXISTS sourcing_location VARCHAR(255) NOT NULL DEFAULT '';
ALTER TABLE vendor ADD COLUMN IF NOT EXISTS serving_location VARCHAR(255) NOT NULL DEFAULT '';

ALTER TABLE planner_task ADD COLUMN IF NOT EXISTS seniority_level VARCHAR(50) NOT NULL DEFAULT '';
ALTER TABLE planner_task ADD COLUMN IF NOT EXISTS internal_notes TEXT NOT NULL DEFAULT '';
ALTER TABLE planner_task ADD COLUMN IF NOT EXISTS vendor_comment TEXT NOT NULL DEFAULT '';

CREATE TABLE IF NOT EXISTS planner_contact (
    contact_id SERIAL PRIMARY KEY,
    planner_id INT NOT NULL REFERENCES planner_task(id) ON DELETE CASCADE,
    name VARCHAR(255) NOT NULL DEFAULT '',
    title VARCHAR(255) NOT NULL DEFAULT '',
    email VARCHAR(255) NOT NULL DEFAULT '',
    phone VARCHAR(50) NOT NULL DEFAULT '',
    agency VARCHAR(255) NOT NULL DEFAULT '',
    is_primary BOOLEAN NOT NULL DEFAULT FALSE
);

CREATE INDEX IF NOT EXISTS idx_planner_contact_planner
    ON planner_contact(planner_id);

CREATE INDEX IF NOT EXISTS idx_planner_vendor_assignment_planner_vendor
    ON planner_vendor_assignment(planner_id, vendor_id);
CREATE INDEX IF NOT EXISTS idx_planner_candidate_submission_planner_vendor
    ON planner_candidate_submission(planner_id, vendor_id);
CREATE INDEX IF NOT EXISTS idx_planner_candidate_submission_vendor_submitted
    ON planner_candidate_submission(vendor_id, is_submitted);

-- Keep existing assigned rows normalized for the vendor dashboard.
UPDATE planner_vendor_assignment
SET status = 'Assigned'
WHERE status IS NULL OR trim(status) = '';
