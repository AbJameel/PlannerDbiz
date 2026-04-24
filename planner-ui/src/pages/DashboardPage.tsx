
import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { api } from '../api/client';
import type { DashboardSummary, PlannerTask } from '../types';

const initialForm = { fromEmail: '', emailContent: '' };

export function DashboardPage() {
  const [summary, setSummary] = useState<DashboardSummary | null>(null);
  const [tasks, setTasks] = useState<PlannerTask[]>([]);
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [form, setForm] = useState(initialForm);
  const [mailFile, setMailFile] = useState<File | null>(null);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [search, setSearch] = useState('');
  const navigate = useNavigate();

  async function loadDashboard() {
    const [summaryData, queueData] = await Promise.all([api.getDashboardSummary(), api.getReviewQueue()]);
    setSummary(summaryData as DashboardSummary);
    setTasks(queueData as PlannerTask[]);
  }

  useEffect(() => { loadDashboard(); }, []);

  const filteredTasks = useMemo(() => {
    if (!search.trim()) return tasks;
    return tasks.filter((task) => [task.plannerNo, task.clientName, task.role, task.requirementTitle].join(' ').toLowerCase().includes(search.toLowerCase()));
  }, [tasks, search]);

  async function handleCreateTask(e: React.FormEvent) {
    e.preventDefault();
    setSaving(true); setError('');
    try {
      const created = await api.uploadMail({ file: mailFile, fromEmail: form.fromEmail || 'internal@dbiz.com', emailContent: form.emailContent });
      const createdTask = created as PlannerTask;
      setDrawerOpen(false); setForm(initialForm); setMailFile(null);
      await loadDashboard();
      navigate(`/tasks/${createdTask.id}`);
    } catch (err) { setError(err instanceof Error ? err.message : 'Failed to create task'); }
    finally { setSaving(false); }
  }

  return (
    <>
      <div className="page-grid review-layout vendor-dashboard-layout">
        <section className="hero-card span-2">
          <div>
            <p className="eyebrow">Requirement Planner</p>
            <h2>Task Dashboard</h2>
            <p className="muted">View your assigned tasks. Admin and recruiters can create new tasks from JD uploads or pasted content.</p>
          </div>
          <button className="primary-btn" onClick={() => setDrawerOpen(true)}>+ New Task</button>
        </section>
        <section className="stats-grid span-2">
          <StatCard label="New Tasks" value={summary?.newTasks ?? 0} onClick={() => navigate('/planners?status=New')} />
          <StatCard label="Under Review" value={summary?.underReview ?? 0} onClick={() => navigate('/planners?status=In Review')} />
          <StatCard label="Assigned to Vendors" value={summary?.assignedToVendors ?? 0} onClick={() => navigate('/planners?status=Assigned to Vendor')} />
          <StatCard label="Closing Today" value={summary?.closingToday ?? 0} onClick={() => navigate('/planners?closingToday=true')} />
        </section>
        <section className="panel review-queue-panel span-2">
          <div className="panel-header"><div><p className="eyebrow">Queue</p><h3>Task Queue</h3></div></div>
          <input className="search-input" placeholder="Search by planner no, client, role..." value={search} onChange={(e) => setSearch(e.target.value)} />
          <div className="queue-list">
            {filteredTasks.map((task) => (
              <button key={task.id} type="button" className="queue-item" onClick={() => navigate(`/tasks/${task.id}`)}>
                <div className="task-title-row"><strong>{task.role}</strong><span className={`pill pill-${task.priority.toLowerCase()}`}>{task.priority}</span></div>
                <div className="muted">{task.clientName}</div>
                <small>{task.plannerNo} · {task.openPositions} position(s)</small>
                <small>SLA {new Date(task.slaDate).toLocaleDateString()}</small>
                <small>Status: {task.status}</small>
              </button>
            ))}
          </div>
        </section>
      </div>
      <div className={`drawer-backdrop ${drawerOpen ? 'open' : ''}`} onClick={() => setDrawerOpen(false)} />
      <aside className={`task-drawer ${drawerOpen ? 'open' : ''}`}>
        <div className="drawer-header"><div><p className="eyebrow">Add New Task</p><h3>Create from JD</h3></div><button className="icon-btn" onClick={() => setDrawerOpen(false)}>✕</button></div>
        <form className="drawer-form" onSubmit={handleCreateTask}>
          <label>From email<input value={form.fromEmail} onChange={(e) => setForm({ ...form, fromEmail: e.target.value })} placeholder="internal@dbiz.com" /></label>
          <label>Upload JD document (.docx/.pdf/.txt/.eml/.msg)<input type="file" accept=".txt,.eml,.msg,.docx,.pdf" onChange={(e) => setMailFile(e.target.files?.[0] ?? null)} /></label>
          <div className="divider-text">or paste JD into Email Content</div>
          <label>Email Content<textarea rows={12} value={form.emailContent} onChange={(e) => setForm({ ...form, emailContent: e.target.value })} placeholder="Paste the JD or requirement email here..." disabled={!!mailFile} /></label>
          {error && <div className="error-box">{error}</div>}
          <div className="drawer-actions"><button type="button" className="secondary-btn" onClick={() => setDrawerOpen(false)}>Close</button><button type="submit" className="primary-btn" disabled={saving || (!mailFile && !form.emailContent.trim())}>{saving ? 'Creating...' : 'Create'}</button></div>
        </form>
      </aside>
    </>
  );
}

function StatCard({ label, value, onClick }: { label: string; value: number; onClick?: () => void; }) {
  return <button type="button" className="stat-card clickable" onClick={onClick}><p>{label}</p><h3>{value}</h3></button>;
}
