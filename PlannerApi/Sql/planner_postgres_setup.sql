CREATE DATABASE plannerdb;
-- Connect to plannerdb before running the rest.

CREATE EXTENSION IF NOT EXISTS "pgcrypto";

CREATE TABLE IF NOT EXISTS planner_task (
    id SERIAL PRIMARY KEY,
    planner_no VARCHAR(50) NOT NULL UNIQUE,
    client_name VARCHAR(255) NOT NULL,
    role VARCHAR(255) NOT NULL,
    priority VARCHAR(50) NOT NULL,
    budget NUMERIC(12,2) NOT NULL DEFAULT 0,
    currency VARCHAR(10) NOT NULL DEFAULT 'SGD',
    received_on TIMESTAMP NOT NULL DEFAULT NOW(),
    sla_date TIMESTAMP NOT NULL,
    status VARCHAR(100) NOT NULL DEFAULT 'New',
    open_positions INTEGER NOT NULL DEFAULT 1,
    source_type VARCHAR(100) NOT NULL DEFAULT 'Mailbox',
    contact_name VARCHAR(255) NOT NULL DEFAULT '',
    contact_email VARCHAR(255) NOT NULL DEFAULT '',
    requirement_asked TEXT NOT NULL DEFAULT '',
    skills_json JSONB NOT NULL DEFAULT '[]'::jsonb,
    gaps_json JSONB NOT NULL DEFAULT '[]'::jsonb,
    timeline_json JSONB NOT NULL DEFAULT '[]'::jsonb,
    recommended_candidate_ids_json JSONB NOT NULL DEFAULT '[]'::jsonb,
    assigned_vendor_ids_json JSONB NOT NULL DEFAULT '[]'::jsonb
);

CREATE TABLE IF NOT EXISTS rule_master (
    rule_id SERIAL PRIMARY KEY,
    rule_name VARCHAR(255) NOT NULL UNIQUE,
    rule_type VARCHAR(100) NOT NULL,
    condition_json JSONB NOT NULL,
    message TEXT NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE
);

CREATE TABLE IF NOT EXISTS vendor (
    vendor_id SERIAL PRIMARY KEY,
    vendor_name VARCHAR(255) NOT NULL UNIQUE,
    email VARCHAR(255) NOT NULL,
    supported_roles TEXT NOT NULL,
    budget_min NUMERIC(12,2) NOT NULL DEFAULT 0,
    budget_max NUMERIC(12,2) NOT NULL DEFAULT 0,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_on TIMESTAMP DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS candidate (
    candidate_id SERIAL PRIMARY KEY,
    candidate_name VARCHAR(255) NOT NULL,
    candidate_current_role VARCHAR(255) NOT NULL,
    expected_budget NUMERIC(12,2) NOT NULL DEFAULT 0,
    experience_years INTEGER NOT NULL DEFAULT 0,
    notice_period VARCHAR(50) NOT NULL DEFAULT 'Immediate',
    resume_file VARCHAR(255) NOT NULL DEFAULT '',
    skills_json JSONB NOT NULL DEFAULT '[]'::jsonb,
    location VARCHAR(100) NOT NULL DEFAULT ''
);

CREATE TABLE IF NOT EXISTS mailbox_item (
    id SERIAL PRIMARY KEY,
    subject TEXT NOT NULL,
    from_email VARCHAR(255) NOT NULL,
    received_on TIMESTAMP NOT NULL DEFAULT NOW(),
    snippet TEXT NOT NULL DEFAULT '',
    is_read BOOLEAN NOT NULL DEFAULT FALSE,
    source_type VARCHAR(100) NOT NULL DEFAULT 'Mailbox'
);

CREATE TABLE IF NOT EXISTS role_master (
    role_code VARCHAR(50) PRIMARY KEY,
    role_name VARCHAR(100) NOT NULL
);

CREATE TABLE IF NOT EXISTS app_user (
    user_id SERIAL PRIMARY KEY,
    full_name VARCHAR(255) NOT NULL,
    email VARCHAR(255) NOT NULL UNIQUE,
    password_hash TEXT,
    role_code VARCHAR(50) NOT NULL REFERENCES role_master(role_code),
    vendor_id INT NULL REFERENCES vendor(vendor_id),
    is_active BOOLEAN DEFAULT TRUE,
    is_first_login BOOLEAN DEFAULT TRUE,
    is_locked BOOLEAN DEFAULT FALSE,
    created_on TIMESTAMP DEFAULT NOW(),
    updated_on TIMESTAMP NULL
);

CREATE TABLE IF NOT EXISTS user_activation (
    activation_id SERIAL PRIMARY KEY,
    user_id INT NOT NULL REFERENCES app_user(user_id) ON DELETE CASCADE,
    activation_token UUID NOT NULL,
    otp_code VARCHAR(10) NOT NULL,
    otp_expiry TIMESTAMP NOT NULL,
    is_used BOOLEAN DEFAULT FALSE,
    created_on TIMESTAMP DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS password_history (
    password_history_id SERIAL PRIMARY KEY,
    user_id INT NOT NULL REFERENCES app_user(user_id) ON DELETE CASCADE,
    password_hash TEXT NOT NULL,
    created_on TIMESTAMP DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS auth_audit_log (
    audit_id SERIAL PRIMARY KEY,
    user_id INT NULL REFERENCES app_user(user_id),
    action_type VARCHAR(100) NOT NULL,
    action_detail TEXT,
    ip_address VARCHAR(100),
    created_on TIMESTAMP DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_planner_task_status ON planner_task(status);
CREATE INDEX IF NOT EXISTS idx_planner_task_received_on ON planner_task(received_on DESC);
CREATE INDEX IF NOT EXISTS idx_mailbox_received_on ON mailbox_item(received_on DESC);
CREATE INDEX IF NOT EXISTS idx_app_user_email ON app_user(email);

INSERT INTO role_master (role_code, role_name) VALUES
('SUPER_ADMIN', 'Super Admin'),
('RECRUITER', 'Recruiter'),
('VENDOR', 'Vendor')
ON CONFLICT (role_code) DO NOTHING;

INSERT INTO rule_master (rule_name, rule_type, condition_json, message, is_active) VALUES
('Minimum Budget', 'Budget', '{"rule":"Budget >= 4500"}', 'Flag task when budget is below workable range', TRUE),
('Supported Infra Roles', 'Role', '{"roles":["Azure Cloud Engineer","DevOps Engineer","Infrastructure"]}', 'Allow auto-routing to infra vendors', TRUE),
('Mandatory SLA', 'Validation', '{"slaRequired":true}', 'Add manual review gap if missing SLA', TRUE)
ON CONFLICT (rule_name) DO NOTHING;

INSERT INTO vendor (vendor_name, email, supported_roles, budget_min, budget_max, is_active) VALUES
('TechSource Partners', 'contact@techsource.com', 'Azure Cloud Engineer, DevOps Engineer, React Developer', 4500, 12000, TRUE),
('Analytica Staffing', 'delivery@analytica.com', 'Business Analyst, QA Engineer', 4000, 9000, TRUE),
('InfraTalent Hub', 'ops@infratalent.com', 'Azure Cloud Engineer, Infrastructure Engineer', 5500, 15000, TRUE)
ON CONFLICT (vendor_name) DO NOTHING;

INSERT INTO candidate (candidate_name, candidate_current_role, expected_budget, experience_years, notice_period, resume_file, skills_json, location) VALUES
('Arun Prakash', 'Azure Cloud Engineer', 7200, 6, '30 Days', 'Arun_Prakash.pdf', '["Azure","Terraform","CI/CD","Docker"]'::jsonb, 'Singapore'),
('Nisha Kumar', 'Business Analyst', 5200, 5, 'Immediate', 'Nisha_Kumar.pdf', '["Business Analysis","SQL","Power BI"]'::jsonb, 'Chennai'),
('Rahul Menon', 'React Developer', 6800, 4, '15 Days', 'Rahul_Menon.pdf', '["React","TypeScript","SQL"]'::jsonb, 'Bengaluru'),
('Sathish Rao', 'DevOps Engineer', 8300, 7, '30 Days', 'Sathish_Rao.pdf', '["Azure","CI/CD","Kubernetes","Docker"]'::jsonb, 'Hyderabad'),
('Divya S', 'QA Engineer', 4800, 4, 'Immediate', 'Divya_S.pdf', '["Testing","SQL","PostgreSQL"]'::jsonb, 'Coimbatore')
ON CONFLICT DO NOTHING;

INSERT INTO planner_task
(planner_no, client_name, role, priority, budget, currency, received_on, sla_date, status, open_positions, source_type, contact_name, contact_email, requirement_asked, skills_json, gaps_json, timeline_json, recommended_candidate_ids_json, assigned_vendor_ids_json)
VALUES
('PLN-202604210001', 'Acme Logistics', 'Azure Cloud Engineer', 'High', 8500, 'SGD', now() - interval '4 hours', now() + interval '2 days', 'New', 2, 'Mailbox', 'Melissa Tan', 'melissa.tan@acme.com', 'Need 2 Azure cloud engineers for migration support. Skills: Azure, Terraform, CI/CD. SLA tomorrow EOD.', '["Azure","Terraform","CI/CD"]', '["Client budget needs final negotiation."]',
 '[{"happenedOn":"2026-04-21T03:00:00Z","title":"Mail received","description":"Requirement email received in shared mailbox.","performedBy":"System"},{"happenedOn":"2026-04-21T03:15:00Z","title":"Planner created","description":"Task auto-created from mailbox.","performedBy":"System"}]',
 '[1,4]', '[1,3]'),
('PLN-202604210002', 'BrightRetail', 'Business Analyst', 'Medium', 5200, 'SGD', now() - interval '1 day', now() + interval '1 day', 'Under Review', 1, 'Uploaded Mail', 'Suresh Nair', 'suresh@brightretail.com', 'Need one BA for retail reporting transformation. Strong stakeholder handling and SQL required.', '["Business Analysis","SQL"]', '["SLA date not clearly mentioned in the mail."]',
 '[{"happenedOn":"2026-04-20T08:30:00Z","title":"Mail uploaded","description":"Requirement mail uploaded by admin.","performedBy":"DBiz Admin"},{"happenedOn":"2026-04-20T09:00:00Z","title":"Rule review","description":"Task sent for manual rule validation.","performedBy":"Rule Engine"}]',
 '[2]', '[]'),
('PLN-202604210003', 'Nova Health', 'React Developer', 'Medium', 6500, 'SGD', now() - interval '6 hours', now() + interval '3 days', 'Assigned to Vendor', 1, 'Mailbox', 'Priya Das', 'priya@novahealth.com', 'Looking for React developer with TypeScript and SQL experience. One profile needed.', '["React","TypeScript","SQL"]', '[]',
 '[{"happenedOn":"2026-04-21T01:10:00Z","title":"Mail received","description":"Requirement email received in mailbox.","performedBy":"System"},{"happenedOn":"2026-04-21T01:35:00Z","title":"Assigned to vendor","description":"Task routed to TechSource Partners.","performedBy":"DBiz Admin"}]',
 '[3]', '[1]')
ON CONFLICT DO NOTHING;

INSERT INTO mailbox_item (subject, from_email, received_on, snippet, is_read, source_type) VALUES
('Need 2 Azure cloud engineers for migration support', 'melissa.tan@acme.com', now() - interval '4 hours', 'Need 2 Azure cloud engineers for migration support. Skills: Azure, Terraform, CI/CD.', FALSE, 'Mailbox'),
('BA requirement for retail reporting project', 'suresh@brightretail.com', now() - interval '1 day', 'Need one BA for retail reporting transformation.', TRUE, 'Uploaded Mail'),
('React developer requirement - urgent', 'priya@novahealth.com', now() - interval '6 hours', 'Looking for React developer with TypeScript and SQL experience.', FALSE, 'Mailbox')
ON CONFLICT DO NOTHING;

INSERT INTO app_user (full_name, email, password_hash, role_code, is_active, is_first_login, is_locked)
VALUES ('System Admin', 'admin@dbiz.com', 'plain:Admin@123', 'SUPER_ADMIN', TRUE, FALSE, FALSE)
ON CONFLICT (email) DO NOTHING;


ALTER TABLE planner_task ADD COLUMN IF NOT EXISTS notes TEXT NOT NULL DEFAULT '';

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
CREATE INDEX IF NOT EXISTS idx_planner_candidate_submission_planner ON planner_candidate_submission(planner_id);
CREATE INDEX IF NOT EXISTS idx_planner_candidate_submission_vendor ON planner_candidate_submission(vendor_id);

INSERT INTO app_user (user_name, full_name, email, password_hash, user_role_code, vendor_id, is_active, is_first_login, is_locked)
VALUES ('vendor1', 'Vendor User One', 'vendor1@techsource.com', 'plain:Admin@123', 'VENDOR', 1, TRUE, FALSE, FALSE)
ON CONFLICT (email) DO NOTHING;
