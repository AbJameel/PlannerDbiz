-- 20260427 Full DB + API alignment for DBiz Planner
-- Safe to run multiple times. It does not drop existing data.
-- Fixes runtime errors such as:
--   42703: column "seniority_level" does not exist
--   42703: column "internal_notes" does not exist

CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- ---------------------------------------------------------------------
-- 1) User/Role compatibility for current API AuthRepository
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS public.user_role_master (
    user_role_code VARCHAR(50) PRIMARY KEY,
    user_role_name VARCHAR(100) NOT NULL,
    description TEXT,
    is_active BOOLEAN DEFAULT TRUE,
    created_on TIMESTAMP DEFAULT NOW()
);

INSERT INTO public.user_role_master (user_role_code, user_role_name, description)
VALUES
('SUPER_ADMIN', 'Super Admin', 'Full system access'),
('RECRUITER', 'Recruiter', 'Internal recruiter access'),
('VENDOR', 'Vendor', 'Vendor portal access')
ON CONFLICT (user_role_code) DO NOTHING;

ALTER TABLE public.app_user ADD COLUMN IF NOT EXISTS user_name VARCHAR(100);
ALTER TABLE public.app_user ADD COLUMN IF NOT EXISTS user_role_code VARCHAR(50);
ALTER TABLE public.app_user ADD COLUMN IF NOT EXISTS last_login_on TIMESTAMP NULL;

-- If the older schema has role_code, copy it into user_role_code.
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'app_user' AND column_name = 'role_code'
    ) THEN
        UPDATE public.app_user
        SET user_role_code = COALESCE(NULLIF(user_role_code, ''), role_code)
        WHERE user_role_code IS NULL OR user_role_code = '';
    END IF;
END $$;

UPDATE public.app_user
SET user_name = split_part(email, '@', 1)
WHERE (user_name IS NULL OR trim(user_name) = '') AND email IS NOT NULL;

UPDATE public.app_user
SET user_role_code = 'SUPER_ADMIN'
WHERE user_role_code IS NULL OR trim(user_role_code) = '';

ALTER TABLE public.app_user ALTER COLUMN user_name SET DEFAULT '';
ALTER TABLE public.app_user ALTER COLUMN user_role_code SET DEFAULT 'RECRUITER';

CREATE UNIQUE INDEX IF NOT EXISTS ux_app_user_email_lower ON public.app_user (lower(email));
CREATE INDEX IF NOT EXISTS idx_app_user_user_name_lower ON public.app_user (lower(user_name));

-- ---------------------------------------------------------------------
-- 2) Planner task columns expected by PlannerApi and MailboxWorker
-- ---------------------------------------------------------------------
ALTER TABLE public.planner_task ADD COLUMN IF NOT EXISTS requirement_title TEXT NOT NULL DEFAULT '';
ALTER TABLE public.planner_task ADD COLUMN IF NOT EXISTS seniority_level VARCHAR(50) NOT NULL DEFAULT '';
ALTER TABLE public.planner_task ADD COLUMN IF NOT EXISTS category VARCHAR(100) NOT NULL DEFAULT 'General';
ALTER TABLE public.planner_task ADD COLUMN IF NOT EXISTS budget_max NUMERIC(12,2) NULL;
ALTER TABLE public.planner_task ADD COLUMN IF NOT EXISTS contact_phone VARCHAR(50) NOT NULL DEFAULT '';
ALTER TABLE public.planner_task ADD COLUMN IF NOT EXISTS internal_notes TEXT NOT NULL DEFAULT '';
ALTER TABLE public.planner_task ADD COLUMN IF NOT EXISTS vendor_comment TEXT NOT NULL DEFAULT '';
ALTER TABLE public.planner_task ADD COLUMN IF NOT EXISTS secondary_skills_json JSONB NOT NULL DEFAULT '[]'::jsonb;
ALTER TABLE public.planner_task ADD COLUMN IF NOT EXISTS experience_required VARCHAR(100) NOT NULL DEFAULT '';
ALTER TABLE public.planner_task ADD COLUMN IF NOT EXISTS location VARCHAR(255) NOT NULL DEFAULT '';
ALTER TABLE public.planner_task ADD COLUMN IF NOT EXISTS work_mode VARCHAR(100) NOT NULL DEFAULT '';
ALTER TABLE public.planner_task ADD COLUMN IF NOT EXISTS employment_type VARCHAR(100) NOT NULL DEFAULT '';
ALTER TABLE public.planner_task ADD COLUMN IF NOT EXISTS recruiter_override_comment TEXT NOT NULL DEFAULT '';
ALTER TABLE public.planner_task ADD COLUMN IF NOT EXISTS notes TEXT NOT NULL DEFAULT '';

-- Keep old notes and new internal_notes in sync for existing rows.
UPDATE public.planner_task
SET internal_notes = COALESCE(NULLIF(internal_notes, ''), notes, '')
WHERE internal_notes IS NULL OR internal_notes = '';

UPDATE public.planner_task
SET notes = COALESCE(NULLIF(notes, ''), internal_notes, '')
WHERE notes IS NULL OR notes = '';

UPDATE public.planner_task
SET requirement_title = COALESCE(NULLIF(requirement_title, ''), role, planner_no, '')
WHERE requirement_title IS NULL OR requirement_title = '';

UPDATE public.planner_task
SET budget_max = budget
WHERE budget_max IS NULL AND budget IS NOT NULL AND budget > 0;

-- ---------------------------------------------------------------------
-- 3) Contacts split from task header, as required by latest demo notes
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS public.planner_contact (
    contact_id SERIAL PRIMARY KEY,
    planner_id INT NOT NULL REFERENCES public.planner_task(id) ON DELETE CASCADE,
    name VARCHAR(255) NOT NULL DEFAULT '',
    title VARCHAR(255) NOT NULL DEFAULT '',
    email VARCHAR(255) NOT NULL DEFAULT '',
    phone VARCHAR(50) NOT NULL DEFAULT '',
    agency VARCHAR(255) NOT NULL DEFAULT '',
    is_primary BOOLEAN NOT NULL DEFAULT FALSE
);

ALTER TABLE public.planner_contact ADD COLUMN IF NOT EXISTS contact_level VARCHAR(100) NOT NULL DEFAULT '\;
CREATE INDEX IF NOT EXISTS idx_planner_contact_planner ON public.planner_contact(planner_id);

-- Seed one primary contact per existing planner_task if no contact rows exist.
INSERT INTO public.planner_contact (planner_id, name, email, phone, is_primary)
SELECT t.id, COALESCE(t.contact_name, ''), COALESCE(t.contact_email, ''), COALESCE(t.contact_phone, ''), TRUE
FROM public.planner_task t
WHERE NOT EXISTS (
    SELECT 1 FROM public.planner_contact pc WHERE pc.planner_id = t.id
);

-- ---------------------------------------------------------------------
-- 4) Vendor assignment + vendor comments/candidate replies
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS public.planner_vendor_assignment (
    planner_vendor_assignment_id SERIAL PRIMARY KEY,
    planner_id INT NOT NULL REFERENCES public.planner_task(id) ON DELETE CASCADE,
    vendor_id INT NOT NULL REFERENCES public.vendor(vendor_id),
    assignment_note TEXT,
    assigned_by_name VARCHAR(255),
    assigned_on TIMESTAMP DEFAULT NOW(),
    status VARCHAR(50) DEFAULT 'Assigned'
);

CREATE INDEX IF NOT EXISTS idx_planner_vendor_assignment_planner_vendor
    ON public.planner_vendor_assignment(planner_id, vendor_id);
CREATE INDEX IF NOT EXISTS idx_planner_vendor_assignment_vendor
    ON public.planner_vendor_assignment(vendor_id);

CREATE TABLE IF NOT EXISTS public.planner_candidate_submission (
    submission_id SERIAL PRIMARY KEY,
    planner_id INT NOT NULL REFERENCES public.planner_task(id) ON DELETE CASCADE,
    vendor_id INT NOT NULL REFERENCES public.vendor(vendor_id),
    candidate_name VARCHAR(255) NOT NULL DEFAULT '',
    contact_detail VARCHAR(255) NOT NULL DEFAULT '',
    visa_type VARCHAR(100) NOT NULL DEFAULT '',
    resume_file VARCHAR(255) NOT NULL DEFAULT '',
    candidate_status VARCHAR(50) NOT NULL DEFAULT 'Draft',
    is_submitted BOOLEAN NOT NULL DEFAULT FALSE,
    created_on TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_on TIMESTAMP NULL
);

ALTER TABLE public.planner_candidate_submission ADD COLUMN IF NOT EXISTS contact_name VARCHAR(255) NOT NULL DEFAULT '';
ALTER TABLE public.planner_candidate_submission ADD COLUMN IF NOT EXISTS candidate_skills TEXT NOT NULL DEFAULT '';
ALTER TABLE public.planner_candidate_submission ADD COLUMN IF NOT EXISTS updated_on TIMESTAMP NULL;

CREATE INDEX IF NOT EXISTS idx_planner_candidate_submission_planner
    ON public.planner_candidate_submission(planner_id);
CREATE INDEX IF NOT EXISTS idx_planner_candidate_submission_vendor
    ON public.planner_candidate_submission(vendor_id);
CREATE INDEX IF NOT EXISTS idx_planner_candidate_submission_planner_vendor
    ON public.planner_candidate_submission(planner_id, vendor_id);
CREATE INDEX IF NOT EXISTS idx_planner_candidate_submission_vendor_submitted
    ON public.planner_candidate_submission(vendor_id, is_submitted);

-- Normalize blank assignment statuses.
UPDATE public.planner_vendor_assignment
SET status = 'Assigned'
WHERE status IS NULL OR trim(status) = '';

-- ---------------------------------------------------------------------
-- 5) Vendor master extra columns from latest demo notes
-- ---------------------------------------------------------------------
ALTER TABLE public.vendor ADD COLUMN IF NOT EXISTS uen_no VARCHAR(50) NOT NULL DEFAULT '';
ALTER TABLE public.vendor ADD COLUMN IF NOT EXISTS poc_name VARCHAR(255) NOT NULL DEFAULT '';
ALTER TABLE public.vendor ADD COLUMN IF NOT EXISTS poc_email VARCHAR(255) NOT NULL DEFAULT '';
ALTER TABLE public.vendor ADD COLUMN IF NOT EXISTS poc_phone VARCHAR(50) NOT NULL DEFAULT '';
ALTER TABLE public.vendor ADD COLUMN IF NOT EXISTS sourcing_location VARCHAR(255) NOT NULL DEFAULT '';
ALTER TABLE public.vendor ADD COLUMN IF NOT EXISTS serving_location VARCHAR(255) NOT NULL DEFAULT '';

-- Make old active boolean readable as Active in API vendor mapper.
UPDATE public.vendor SET is_active = TRUE WHERE is_active IS NULL;

-- ---------------------------------------------------------------------
-- 6) MailboxWorker support tables
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS public.planner (
    planner_id SERIAL PRIMARY KEY,
    planner_no VARCHAR(50),
    client_name VARCHAR(255),
    requirement_title TEXT,
    role_name VARCHAR(255),
    budget_min NUMERIC(12,2),
    budget_max NUMERIC(12,2),
    currency VARCHAR(10),
    sla_date TIMESTAMP,
    contact_name VARCHAR(255),
    contact_email VARCHAR(255),
    requirement_summary TEXT,
    requirement_asked TEXT,
    gaps TEXT,
    status VARCHAR(50) DEFAULT 'New',
    source_email_id VARCHAR(255),
    conversation_id VARCHAR(255),
    created_on TIMESTAMP DEFAULT NOW(),
    updated_on TIMESTAMP
);

CREATE TABLE IF NOT EXISTS public.planner_email (
    planner_email_id SERIAL PRIMARY KEY,
    planner_id INT REFERENCES public.planner(planner_id) ON DELETE CASCADE,
    message_id VARCHAR(255),
    conversation_id VARCHAR(255),
    subject TEXT,
    from_email VARCHAR(255),
    received_on TIMESTAMP,
    email_body TEXT,
    is_processed BOOLEAN DEFAULT FALSE,
    created_on TIMESTAMP DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS public.planner_activity (
    activity_id SERIAL PRIMARY KEY,
    planner_id INT REFERENCES public.planner(planner_id) ON DELETE CASCADE,
    action_type VARCHAR(100),
    action_by VARCHAR(255),
    remarks TEXT,
    created_on TIMESTAMP DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS public.planner_attachment (
    attachment_id SERIAL PRIMARY KEY,
    planner_id INT REFERENCES public.planner(planner_id) ON DELETE CASCADE,
    file_name TEXT,
    content_type VARCHAR(255),
    file_size BIGINT,
    extracted_text TEXT,
    created_on TIMESTAMP DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_planner_email_message_id ON public.planner_email(message_id);
CREATE INDEX IF NOT EXISTS idx_planner_task_status ON public.planner_task(status);
CREATE INDEX IF NOT EXISTS idx_planner_task_received_on ON public.planner_task(received_on DESC);
CREATE INDEX IF NOT EXISTS idx_planner_task_sla_date ON public.planner_task(sla_date);

-- ---------------------------------------------------------------------
-- 7) Seed users/vendors for local testing if missing
-- ---------------------------------------------------------------------
INSERT INTO public.app_user (user_name, full_name, email, password_hash, user_role_code, is_active, is_first_login, is_locked)
VALUES ('admin', 'System Administrator', 'admin@dbiz.com', 'plain:Admin@123', 'SUPER_ADMIN', TRUE, FALSE, FALSE)
ON CONFLICT DO NOTHING;

INSERT INTO public.app_user (user_name, full_name, email, password_hash, user_role_code, vendor_id, is_active, is_first_login, is_locked)
VALUES ('vendor1', 'Vendor User One', 'vendor1@techsource.com', 'plain:Admin@123', 'VENDOR', 1, TRUE, FALSE, FALSE)
ON CONFLICT DO NOTHING;
