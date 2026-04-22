import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../api/client';
import type { DashboardSummary, PlannerTask } from '../types';

const initialForm = {
  subject: '',
  fromEmail: '',
  body: ''
};

export function DashboardPage() {
  const [summary, setSummary] = useState<DashboardSummary | null>(null);
  const [tasks, setTasks] = useState<PlannerTask[]>([]);
  const [loading, setLoading] = useState(true);
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [form, setForm] = useState(initialForm);
  const [mailFile, setMailFile] = useState<File | null>(null);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');

  async function loadDashboard() {
    setLoading(true);
    try {
      const [summaryData, taskData] = await Promise.all([api.getDashboardSummary(), api.getTopTasks()]);
      setSummary(summaryData as DashboardSummary);
      setTasks(taskData as PlannerTask[]);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    loadDashboard();
  }, []);

  async function handleCreateTask(e: React.FormEvent) {
    e.preventDefault();
    setSaving(true);
    setError('');
    try {
      if (mailFile) {
        await api.uploadMail(mailFile, form.fromEmail || 'uploaded@mail.local');
      } else {
        await api.createTask(form);
      }
      setDrawerOpen(false);
      setForm(initialForm);
      setMailFile(null);
      await loadDashboard();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to create task');
    } finally {
      setSaving(false);
    }
  }

  if (loading) return <div className="panel">Loading dashboard...</div>;

  return (
    <>
      <div className="page-grid">
        <section className="hero-card">
          <div>
            <p className="eyebrow">Requirement Planner</p>
            <h2>New task board</h2>
            <p className="muted">Mailbox-driven demand intake with rule checks, vendor routing, and candidate recommendations.</p>
          </div>
          <button className="primary-btn" onClick={() => setDrawerOpen(true)}>+ New Task</button>
        </section>

        <section className="stats-grid">
          <StatCard label="New Tasks" value={summary?.newTasks ?? 0} />
          <StatCard label="Under Review" value={summary?.underReview ?? 0} />
          <StatCard label="Assigned to Vendors" value={summary?.assignedToVendors ?? 0} />
          <StatCard label="Closing Today" value={summary?.closingToday ?? 0} />
        </section>

        <section className="panel span-2">
          <div className="panel-header">
            <div>
              <h3>Top 5 new tasks</h3>
              <p className="muted">Now loaded from PostgreSQL through the API.</p>
            </div>
          </div>

          <div className="task-list">
            {tasks.map((task) => (
              <Link key={task.id} to={`/tasks/${task.id}`} className="task-item">
                <div>
                  <div className="task-title-row">
                    <strong>{task.role}</strong>
                    <span className={`pill pill-${task.priority.toLowerCase()}`}>{task.priority}</span>
                  </div>
                  <p>{task.clientName}</p>
                  <small>{task.plannerNo} · {task.openPositions} positions · SLA {new Date(task.slaDate).toLocaleDateString()}</small>
                </div>
                <div className="amount-box">{task.currency} {task.budget.toLocaleString()}</div>
              </Link>
            ))}
          </div>
        </section>
      </div>

      <div className={`drawer-backdrop ${drawerOpen ? 'open' : ''}`} onClick={() => setDrawerOpen(false)} />
      <aside className={`task-drawer ${drawerOpen ? 'open' : ''}`}>
        <div className="drawer-header">
          <div>
            <p className="eyebrow">Add New Task</p>
            <h3>Create from mail</h3>
          </div>
          <button className="icon-btn" onClick={() => setDrawerOpen(false)}>✕</button>
        </div>

        <form className="drawer-form" onSubmit={handleCreateTask}>
          <label>
            From email
            <input value={form.fromEmail} onChange={(e) => setForm({ ...form, fromEmail: e.target.value })} placeholder="client@company.com" />
          </label>

          <label>
            Subject
            <input value={form.subject} onChange={(e) => setForm({ ...form, subject: e.target.value })} placeholder="Need 2 Azure cloud engineers" disabled={!!mailFile} />
          </label>

          <label>
            Upload mail file (.txt/.eml)
            <input type="file" accept=".txt,.eml,.msg" onChange={(e) => setMailFile(e.target.files?.[0] ?? null)} />
          </label>

          <div className="divider-text">or paste email body</div>

          <label>
            Email content
            <textarea rows={10} value={form.body} onChange={(e) => setForm({ ...form, body: e.target.value })} placeholder="Paste the client requirement email here..." disabled={!!mailFile} />
          </label>

          {error && <div className="error-box">{error}</div>}

          <div className="drawer-actions">
            <button type="button" className="secondary-btn" onClick={() => setDrawerOpen(false)}>Close</button>
            <button type="submit" className="primary-btn" disabled={saving || (!mailFile && (!form.subject || !form.fromEmail || !form.body))}>
              {saving ? 'Saving...' : 'Create Task'}
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
