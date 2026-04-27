-- Contacts module patch: task detail Contacts tab + global Contacts list page
ALTER TABLE public.planner_contact
    ADD COLUMN IF NOT EXISTS contact_level varchar(100) NOT NULL DEFAULT '';

CREATE INDEX IF NOT EXISTS idx_planner_contact_primary
    ON public.planner_contact(planner_id, is_primary);
