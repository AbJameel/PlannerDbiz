create database plannerdb;

-- Connect to plannerdb before running the rest.

create table if not exists planner_task (
    id serial primary key,
    planner_no varchar(50) not null unique,
    client_name varchar(255) not null,
    role varchar(255) not null,
    priority varchar(50) not null,
    budget numeric(12,2) not null default 0,
    currency varchar(10) not null default 'SGD',
    received_on timestamp not null default now(),
    sla_date timestamp not null,
    status varchar(100) not null default 'New',
    open_positions integer not null default 1,
    source_type varchar(100) not null default 'Mailbox',
    contact_name varchar(255) not null default '',
    contact_email varchar(255) not null default '',
    requirement_asked text not null default '',
    skills_json jsonb not null default '[]'::jsonb,
    gaps_json jsonb not null default '[]'::jsonb,
    timeline_json jsonb not null default '[]'::jsonb,
    recommended_candidate_ids_json jsonb not null default '[]'::jsonb,
    assigned_vendor_ids_json jsonb not null default '[]'::jsonb
);

create table if not exists rule_master (
    id serial primary key,
    name varchar(255) not null,
    category varchar(100) not null,
    condition text not null,
    outcome text not null,
    is_active boolean not null default true
);

create table if not exists vendor (
    id serial primary key,
    name varchar(255) not null,
    email varchar(255) not null,
    coverage_roles text not null,
    budget_min numeric(12,2) not null default 0,
    budget_max numeric(12,2) not null default 0,
    status varchar(50) not null default 'Active'
);

create table if not exists candidate (
    id serial primary key,
    name varchar(255) not null,
    current_role varchar(255) not null,
    expected_budget numeric(12,2) not null default 0,
    experience_years integer not null default 0,
    notice_period varchar(50) not null default 'Immediate',
    resume_file varchar(255) not null default '',
    skills_json jsonb not null default '[]'::jsonb,
    location varchar(100) not null default ''
);

create table if not exists mailbox_item (
    id serial primary key,
    subject text not null,
    from_email varchar(255) not null,
    received_on timestamp not null default now(),
    snippet text not null default '',
    is_read boolean not null default false,
    source_type varchar(100) not null default 'Mailbox'
);

create index if not exists idx_planner_task_status on planner_task(status);
create index if not exists idx_planner_task_received_on on planner_task(received_on desc);
create index if not exists idx_mailbox_received_on on mailbox_item(received_on desc);

insert into rule_master (name, category, condition, outcome, is_active) values
('Minimum Budget', 'Budget', 'Budget must be >= 4500', 'Flag task when budget is below workable range', true),
('Supported Infra Roles', 'Role', 'Azure Cloud Engineer, DevOps Engineer, Infrastructure roles', 'Allow auto-routing to infra vendors', true),
('Mandatory SLA', 'Validation', 'Email should include deadline or SLA', 'Add manual review gap if missing SLA', true)
on conflict do nothing;

insert into vendor (name, email, coverage_roles, budget_min, budget_max, status) values
('TechSource Partners', 'contact@techsource.com', 'Azure Cloud Engineer, DevOps Engineer, React Developer', 4500, 12000, 'Active'),
('Analytica Staffing', 'delivery@analytica.com', 'Business Analyst, QA Engineer', 4000, 9000, 'Active'),
('InfraTalent Hub', 'ops@infratalent.com', 'Azure Cloud Engineer, Infrastructure Engineer', 5500, 15000, 'Active')
on conflict do nothing;

insert into candidate (name, current_role, expected_budget, experience_years, notice_period, resume_file, skills_json, location) values
('Arun Prakash', 'Azure Cloud Engineer', 7200, 6, '30 Days', 'Arun_Prakash.pdf', '["Azure","Terraform","CI/CD","Docker"]', 'Singapore'),
('Nisha Kumar', 'Business Analyst', 5200, 5, 'Immediate', 'Nisha_Kumar.pdf', '["Business Analysis","SQL","Power BI"]', 'Chennai'),
('Rahul Menon', 'React Developer', 6800, 4, '15 Days', 'Rahul_Menon.pdf', '["React","TypeScript","SQL"]', 'Bengaluru'),
('Sathish Rao', 'DevOps Engineer', 8300, 7, '30 Days', 'Sathish_Rao.pdf', '["Azure","CI/CD","Kubernetes","Docker"]', 'Hyderabad'),
('Divya S', 'QA Engineer', 4800, 4, 'Immediate', 'Divya_S.pdf', '["Testing","SQL","PostgreSQL"]', 'Coimbatore')
on conflict do nothing;

insert into planner_task
(planner_no, client_name, role, priority, budget, currency, received_on, sla_date, status, open_positions, source_type, contact_name, contact_email, requirement_asked, skills_json, gaps_json, timeline_json, recommended_candidate_ids_json, assigned_vendor_ids_json)
values
('PLN-202604210001', 'Acme Logistics', 'Azure Cloud Engineer', 'High', 8500, 'SGD', now() - interval '4 hours', now() + interval '2 days', 'New', 2, 'Mailbox', 'Melissa Tan', 'melissa.tan@acme.com', 'Need 2 Azure cloud engineers for migration support. Skills: Azure, Terraform, CI/CD. SLA tomorrow EOD.', '["Azure","Terraform","CI/CD"]', '["Client budget needs final negotiation."]',
 '[{"happenedOn":"2026-04-21T03:00:00Z","title":"Mail received","description":"Requirement email received in shared mailbox.","performedBy":"System"},{"happenedOn":"2026-04-21T03:15:00Z","title":"Planner created","description":"Task auto-created from mailbox.","performedBy":"System"}]',
 '[1,4]', '[1,3]'),
('PLN-202604210002', 'BrightRetail', 'Business Analyst', 'Medium', 5200, 'SGD', now() - interval '1 day', now() + interval '1 day', 'Under Review', 1, 'Uploaded Mail', 'Suresh Nair', 'suresh@brightretail.com', 'Need one BA for retail reporting transformation. Strong stakeholder handling and SQL required.', '["Business Analysis","SQL"]', '["SLA date not clearly mentioned in the mail."]',
 '[{"happenedOn":"2026-04-20T08:30:00Z","title":"Mail uploaded","description":"Requirement mail uploaded by admin.","performedBy":"DBiz Admin"},{"happenedOn":"2026-04-20T09:00:00Z","title":"Rule review","description":"Task sent for manual rule validation.","performedBy":"Rule Engine"}]',
 '[2]', '[]'),
('PLN-202604210003', 'Nova Health', 'React Developer', 'Medium', 6500, 'SGD', now() - interval '6 hours', now() + interval '3 days', 'Assigned to Vendor', 1, 'Mailbox', 'Priya Das', 'priya@novahealth.com', 'Looking for React developer with TypeScript and SQL experience. One profile needed.', '["React","TypeScript","SQL"]', '[]',
 '[{"happenedOn":"2026-04-21T01:10:00Z","title":"Mail received","description":"Requirement email received in mailbox.","performedBy":"System"},{"happenedOn":"2026-04-21T01:35:00Z","title":"Assigned to vendor","description":"Task routed to TechSource Partners.","performedBy":"DBiz Admin"}]',
 '[3]', '[1]')
on conflict do nothing;

insert into mailbox_item (subject, from_email, received_on, snippet, is_read, source_type) values
('Need 2 Azure cloud engineers for migration support', 'melissa.tan@acme.com', now() - interval '4 hours', 'Need 2 Azure cloud engineers for migration support. Skills: Azure, Terraform, CI/CD.', false, 'Mailbox'),
('BA requirement for retail reporting project', 'suresh@brightretail.com', now() - interval '1 day', 'Need one BA for retail reporting transformation.', true, 'Uploaded Mail'),
('React developer requirement - urgent', 'priya@novahealth.com', now() - interval '6 hours', 'Looking for React developer with TypeScript and SQL experience.', false, 'Mailbox')
on conflict do nothing;
