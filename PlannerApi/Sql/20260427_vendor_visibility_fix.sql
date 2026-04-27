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
    candidate_status VARCHAR(50) NOT NULL DEFAULT 'Draft',
    is_submitted BOOLEAN NOT NULL DEFAULT FALSE,
    created_on TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_on TIMESTAMP NULL
);

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
