import { useEffect, useMemo, useState } from 'react';
import { api } from '../api/client';
import type { Candidate, DashboardSummary, PlannerTask, Vendor } from '../types';
import { VendorMultiSelect } from '../components/VendorMultiSelect';

const initialForm = { fromEmail: '', emailContent: '' };

export function DashboardPage() {
  const [summary, setSummary] = useState<DashboardSummary | null>(null);
  const [tasks, setTasks] = useState<PlannerTask[]>([]);
  const [selectedTaskId, setSelectedTaskId] = useState<number | null>(null);
  const [selectedTask, setSelectedTask] = useState<PlannerTask | null>(null);
  const [recommendedCandidates, setRecommendedCandidates] = useState<Candidate[]>([]);
  const [recommendedVendors, setRecommendedVendors] = useState<Vendor[]>([]);
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [form, setForm] = useState(initialForm);
  const [mailFile, setMailFile] = useState<File | null>(null);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [vendorIds, setVendorIds] = useState<number[]>([]);
  const [search, setSearch] = useState('');

  async function loadDashboard(selectId?: number | null) {
    const [summaryData, queueData] = await Promise.all([
      api.getDashboardSummary(),
      api.getReviewQueue()
    ]);

    const queue = queueData as PlannerTask[];
    setSummary(summaryData as DashboardSummary);
    setTasks(queue);

    const nextId = selectId ?? selectedTaskId ?? queue[0]?.id ?? null;
    if (nextId) {
      await loadTask(nextId);
    } else {
      setSelectedTask(null);
      setRecommendedCandidates([]);
      setRecommendedVendors([]);
    }
  }

  async function loadTask(taskId: number) {
    setSelectedTaskId(taskId);

    const [taskData, candidateData, vendorData] = await Promise.all([
      api.getTask(taskId),
      api.getRecommendedCandidates(taskId),
      api.getRecommendedVendors(taskId)
    ]);

    const task = taskData as PlannerTask;
    setSelectedTask(task);
    setRecommendedCandidates(candidateData as Candidate[]);
    setRecommendedVendors(vendorData as Vendor[]);
    setVendorIds(task.assignedVendorIds ?? []);
  }

  useEffect(() => {
    loadDashboard();
  }, []);

  const filteredTasks = useMemo(() => {
    if (!search.trim()) return tasks;

    return tasks.filter((task) =>
      [task.plannerNo, task.clientName, task.role, task.requirementTitle]
        .join(' ')
        .toLowerCase()
        .includes(search.toLowerCase())
    );
  }, [tasks, search]);

  async function handleCreateTask(e: React.FormEvent) {
    e.preventDefault();
    setSaving(true);
    setError('');

    try {
      const created = await api.uploadMail({
        file: mailFile,
        fromEmail: form.fromEmail || 'internal@dbiz.com',
        emailContent: form.emailContent
      });

      const createdTask = created as PlannerTask;
      setDrawerOpen(false);
      setForm(initialForm);
      setMailFile(null);
      await loadDashboard(createdTask.id);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to create task');
    } finally {
      setSaving(false);
    }
  }

  async function handleSave() {
    if (!selectedTask) return;

    setSaving(true);
    setError('');

    try {
      await api.updateTask(selectedTask.id, {
        clientName: selectedTask.clientName,
        requirementTitle: selectedTask.requirementTitle,
        role: selectedTask.role,
        category: selectedTask.category,
        budget: Number(selectedTask.budget || 0),
        budgetMax: selectedTask.budgetMax ? Number(selectedTask.budgetMax) : null,
        currency: selectedTask.currency,
        slaDate: selectedTask.slaDate,
        openPositions: Number(selectedTask.openPositions || 1),
        priority: selectedTask.priority,
        contactName: selectedTask.contactName,
        contactEmail: selectedTask.contactEmail,
        contactPhone: selectedTask.contactPhone,
        requirementAsked: selectedTask.requirementAsked,
        notes: selectedTask.notes,
        skills: selectedTask.skills,
        secondarySkills: selectedTask.secondarySkills,
        experienceRequired: selectedTask.experienceRequired,
        location: selectedTask.location,
        workMode: selectedTask.workMode,
        employmentType: selectedTask.employmentType,
        status: 'In Review',
        recruiterOverrideComment: selectedTask.recruiterOverrideComment
      });

      await loadDashboard(selectedTask.id);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to save task');
    } finally {
      setSaving(false);
    }
  }

  async function handleAssign() {
    if (!selectedTask) return;

    setSaving(true);
    setError('');

    try {
      if (vendorIds.length === 0) {
        throw new Error('Select at least one vendor.');
      }

      await handleSave();

      await api.assignVendors(selectedTask.id, {
        vendorIds,
        assignmentNote: 'Assigned after recruiter review.',
        updateStatusTo: 'Assigned to Vendor'
      });

      await loadDashboard(selectedTask.id);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to assign vendors');
    } finally {
      setSaving(false);
    }
  }

  function updateTask<K extends keyof PlannerTask>(key: K, value: PlannerTask[K]) {
    setSelectedTask((current) => (current ? { ...current, [key]: value } : current));
  }

  function updateSkillList(kind: 'skills' | 'secondarySkills', value: string) {
    const items = value
      .split(',')
      .map((x) => x.trim())
      .filter(Boolean);

    updateTask(kind, items as PlannerTask[typeof kind]);
  }

  return (
    <>
      <div className="page-grid review-layout">
        <section className="hero-card span-2">
          <div>
            <p className="eyebrow">Requirement Planner</p>
            <h2>My Review Queue</h2>
            <p className="muted">
              Paste JD into Email Content or upload a Word/PDF/TXT file. New tasks land in
              review where you can edit, save, and assign to vendors.
            </p>
          </div>
          <button className="primary-btn" onClick={() => setDrawerOpen(true)}>
            + New Task
          </button>
        </section>

        <section className="stats-grid span-2">
          <StatCard label="New Tasks" value={summary?.newTasks ?? 0} />
          <StatCard label="Under Review" value={summary?.underReview ?? 0} />
          <StatCard label="Assigned to Vendors" value={summary?.assignedToVendors ?? 0} />
          <StatCard label="Closing Today" value={summary?.closingToday ?? 0} />
        </section>

        <section className="panel review-queue-panel">
          <div className="panel-header">
            <div>
              <p className="eyebrow">Queue</p>
              <h3>Review Queue</h3>
            </div>
          </div>

          <input
            className="search-input"
            placeholder="Search by planner no, client, role..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />

          <div className="queue-list">
            {filteredTasks.map((task) => (
              <button
                key={task.id}
                type="button"
                className={`queue-item ${selectedTaskId === task.id ? 'active' : ''}`}
                onClick={() => loadTask(task.id)}
              >
                <div className="task-title-row">
                  <strong>{task.role}</strong>
                  <span className={`pill pill-${task.priority.toLowerCase()}`}>
                    {task.priority}
                  </span>
                </div>
                <div className="muted">{task.clientName}</div>
                <small>{task.plannerNo} · {task.openPositions} position(s)</small>
                <small>SLA {new Date(task.slaDate).toLocaleDateString()}</small>
                <small>Status: {task.status}</small>
              </button>
            ))}
          </div>
        </section>

        <section className="panel review-workspace">
          {!selectedTask ? (
            <div className="empty-state">
              <h3>No task selected</h3>
              <p className="muted">Create a new task or pick one from the review queue.</p>
            </div>
          ) : (
            <>
              <div className="panel-header">
                <div>
                  <p className="eyebrow">Review Workspace</p>
                  <h2>{selectedTask.requirementTitle || selectedTask.role}</h2>
                  <p className="muted">
                    {selectedTask.plannerNo} · {selectedTask.sourceType}
                  </p>
                </div>
                <div className="action-row">
                  <button className="secondary-btn" onClick={handleSave} disabled={saving}>
                    Save
                  </button>
                  <button className="primary-btn" onClick={handleAssign} disabled={saving}>
                    Assign to Vendor
                  </button>
                </div>
              </div>

              {error && <div className="error-box">{error}</div>}

              <div className="workspace-grid">
                <div className="editor-column">
                  <section className="editor-section">
                    <h3>Requirement</h3>
                    <div className="form-grid">
                      <label>
                        Client Name
                        <input
                          value={selectedTask.clientName}
                          onChange={(e) => updateTask('clientName', e.target.value)}
                        />
                      </label>

                      <label>
                        Requirement Title
                        <input
                          value={selectedTask.requirementTitle}
                          onChange={(e) => updateTask('requirementTitle', e.target.value)}
                        />
                      </label>

                      <label>
                        Role
                        <input
                          value={selectedTask.role}
                          onChange={(e) => updateTask('role', e.target.value)}
                        />
                      </label>

                      <label>
                        Category
                        <input
                          value={selectedTask.category}
                          onChange={(e) => updateTask('category', e.target.value)}
                        />
                      </label>

                      <label className="full-span">
                        Requirement Asked
                        <textarea
                          rows={4}
                          value={selectedTask.requirementAsked}
                          onChange={(e) => updateTask('requirementAsked', e.target.value)}
                        />
                      </label>

                      <label className="full-span">
                        Notes
                        <textarea
                          rows={3}
                          value={selectedTask.notes}
                          onChange={(e) => updateTask('notes', e.target.value)}
                        />
                      </label>
                    </div>
                  </section>

                  <section className="editor-section">
                    <h3>Commercial & SLA</h3>
                    <div className="form-grid">
                      <label>
                        Budget
                        <input
                          type="number"
                          value={selectedTask.budget}
                          onChange={(e) => updateTask('budget', Number(e.target.value))}
                        />
                      </label>

                      <label>
                        Budget Max
                        <input
                          type="number"
                          value={selectedTask.budgetMax ?? ''}
                          onChange={(e) =>
                            updateTask('budgetMax', e.target.value ? Number(e.target.value) : null)
                          }
                        />
                      </label>

                      <label>
                        Currency
                        <input
                          value={selectedTask.currency}
                          onChange={(e) => updateTask('currency', e.target.value)}
                        />
                      </label>

                      <label>
                        SLA Date
                        <input
                          type="datetime-local"
                          value={toLocalInputValue(selectedTask.slaDate)}
                          onChange={(e) => updateTask('slaDate', e.target.value)}
                        />
                      </label>

                      <label>
                        No. of Positions
                        <input
                          type="number"
                          value={selectedTask.openPositions}
                          onChange={(e) => updateTask('openPositions', Number(e.target.value))}
                        />
                      </label>

                      <label>
                        Priority
                        <select
                          value={selectedTask.priority}
                          onChange={(e) =>
                            updateTask('priority', e.target.value as PlannerTask['priority'])
                          }
                        >
                          <option>High</option>
                          <option>Medium</option>
                          <option>Low</option>
                        </select>
                      </label>
                    </div>
                  </section>

                  <section className="editor-section">
                    <h3>Contact & Skills</h3>
                    <div className="form-grid">
                      <label>
                        Contact Name
                        <input
                          value={selectedTask.contactName}
                          onChange={(e) => updateTask('contactName', e.target.value)}
                        />
                      </label>

                      <label>
                        Contact Email
                        <input
                          value={selectedTask.contactEmail}
                          onChange={(e) => updateTask('contactEmail', e.target.value)}
                        />
                      </label>

                      <label>
                        Contact Phone
                        <input
                          value={selectedTask.contactPhone}
                          onChange={(e) => updateTask('contactPhone', e.target.value)}
                        />
                      </label>

                      <label>
                        Experience Required
                        <input
                          value={selectedTask.experienceRequired}
                          onChange={(e) => updateTask('experienceRequired', e.target.value)}
                        />
                      </label>

                      <label>
                        Location
                        <input
                          value={selectedTask.location}
                          onChange={(e) => updateTask('location', e.target.value)}
                        />
                      </label>

                      <label>
                        Work Mode
                        <input
                          value={selectedTask.workMode}
                          onChange={(e) => updateTask('workMode', e.target.value)}
                        />
                      </label>

                      <label>
                        Employment Type
                        <input
                          value={selectedTask.employmentType}
                          onChange={(e) => updateTask('employmentType', e.target.value)}
                        />
                      </label>

                      <label className="full-span">
                        Primary Skills (comma separated)
                        <input
                          value={selectedTask.skills.join(', ')}
                          onChange={(e) => updateSkillList('skills', e.target.value)}
                        />
                      </label>

                      <label className="full-span">
                        Secondary Skills (comma separated)
                        <input
                          value={selectedTask.secondarySkills.join(', ')}
                          onChange={(e) => updateSkillList('secondarySkills', e.target.value)}
                        />
                      </label>

                      <label className="full-span">
                        Recruiter Override Comment
                        <textarea
                          rows={3}
                          value={selectedTask.recruiterOverrideComment}
                          onChange={(e) =>
                            updateTask('recruiterOverrideComment', e.target.value)
                          }
                        />
                      </label>
                    </div>
                  </section>
                </div>

                <div className="side-column">
                  <section className="editor-section">
                    <h3>Rule / Gap Review</h3>
                    <ul className="clean-list">
                      {selectedTask.gaps.length === 0 ? (
                        <li>No gaps found.</li>
                      ) : (
                        selectedTask.gaps.map((gap) => <li key={gap}>{gap}</li>)
                      )}
                    </ul>
                  </section>

                  <section className="editor-section">
                    <h3>Recommended Candidates</h3>
                    <div className="stack-list">
                      {recommendedCandidates.length === 0 && (
                        <p className="muted">No candidate recommendations yet.</p>
                      )}

                      {recommendedCandidates.map((candidate) => (
                        <div key={candidate.id} className="mini-card">
                          <strong>{candidate.name}</strong>
                          <p>{candidate.currentRole}</p>
                          <small>
                            {candidate.experienceYears} years · {candidate.noticePeriod}
                          </small>
                          <small>Expected: SGD {candidate.expectedBudget}</small>
                        </div>
                      ))}
                    </div>
                  </section>

                  <section className="editor-section">
                    <h3>Assign Vendors</h3>
                    <VendorMultiSelect
                      vendors={recommendedVendors}
                      selectedVendorIds={vendorIds}
                      onChange={setVendorIds}
                    />
                  </section>

                  <section className="editor-section">
                    <h3>Timeline</h3>
                    <div className="timeline compact">
                      {selectedTask.timeline.map((item, index) => (
                        <div key={index} className="timeline-item">
                          <div className="timeline-dot" />
                          <div>
                            <strong>{item.title}</strong>
                            <p>{item.description}</p>
                            <small>
                              {new Date(item.happenedOn).toLocaleString()} · {item.performedBy}
                            </small>
                          </div>
                        </div>
                      ))}
                    </div>
                  </section>
                </div>
              </div>
            </>
          )}
        </section>
      </div>

      <div
        className={`drawer-backdrop ${drawerOpen ? 'open' : ''}`}
        onClick={() => setDrawerOpen(false)}
      />

      <aside className={`task-drawer ${drawerOpen ? 'open' : ''}`}>
        <div className="drawer-header">
          <div>
            <p className="eyebrow">Add New Task</p>
            <h3>Create from JD</h3>
          </div>
          <button className="icon-btn" onClick={() => setDrawerOpen(false)}>
            ✕
          </button>
        </div>

        <form className="drawer-form" onSubmit={handleCreateTask}>
          <label>
            From email
            <input
              value={form.fromEmail}
              onChange={(e) => setForm({ ...form, fromEmail: e.target.value })}
              placeholder="internal@dbiz.com"
            />
          </label>

          <label>
            Upload JD document (.docx/.pdf/.txt/.eml/.msg)
            <input
              type="file"
              accept=".txt,.eml,.msg,.docx,.pdf"
              onChange={(e) => setMailFile(e.target.files?.[0] ?? null)}
            />
          </label>

          <div className="divider-text">or paste JD into Email Content</div>

          <label>
            Email Content
            <textarea
              rows={12}
              value={form.emailContent}
              onChange={(e) => setForm({ ...form, emailContent: e.target.value })}
              placeholder="Paste the JD or requirement email here..."
              disabled={!!mailFile}
            />
          </label>

          {error && <div className="error-box">{error}</div>}

          <div className="drawer-actions">
            <button
              type="button"
              className="secondary-btn"
              onClick={() => setDrawerOpen(false)}
            >
              Close
            </button>
            <button
              type="submit"
              className="primary-btn"
              disabled={saving || (!mailFile && !form.emailContent.trim())}
            >
              {saving ? 'Creating...' : 'Create'}
            </button>
          </div>
        </form>
      </aside>
    </>
  );
}

function StatCard({ label, value }: { label: string; value: number }) {
  return (
    <div className="stat-card">
      <p>{label}</p>
      <h3>{value}</h3>
    </div>
  );
}

function toLocalInputValue(value: string) {
  if (!value) return '';
  const date = new Date(value);
  const offset = date.getTimezoneOffset();
  const local = new Date(date.getTime() - offset * 60000);
  return local.toISOString().slice(0, 16);
}