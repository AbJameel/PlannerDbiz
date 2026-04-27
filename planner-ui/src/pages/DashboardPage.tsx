import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { api } from '../api/client';
import { getSession } from '../lib/auth';
import type { DashboardSummary, PlannerTask, TimelineItem } from '../types';

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
  const session = getSession();
  const isVendor = session?.roleCode === 'VENDOR';

  function toStartOfDay(value: Date) {
    return new Date(value.getFullYear(), value.getMonth(), value.getDate());
  }

  function daysAgo(iso: string) {
    const date = new Date(iso);
    if (Number.isNaN(date.getTime())) return null;
    const diffMs = toStartOfDay(new Date()).getTime() - toStartOfDay(date).getTime();
    return Math.floor(diffMs / (24 * 60 * 60 * 1000));
  }

  function findEventDate(timeline: TimelineItem[], predicate: (item: TimelineItem) => boolean) {
    for (let i = timeline.length - 1; i >= 0; i -= 1) {
      const item = timeline[i];
      if (predicate(item)) return item.happenedOn;
    }
    return null;
  }

  function getDateField(task: PlannerTask, field: 'received' | 'assigned' | 'vendorReply') {
    if (field === 'assigned') {
      return (
        findEventDate(task.timeline, (x) => x.title.toLowerCase().includes('assign')) ??
        task.receivedOn
      );
    }
    if (field === 'vendorReply') {
      return (
        findEventDate(task.timeline, (x) => x.title.toLowerCase().includes('vendor submitted')) ??
        findEventDate(task.timeline, (x) => x.title.toLowerCase().includes('submitted')) ??
        task.receivedOn
      );
    }
    return task.receivedOn;
  }

  function getBucketCounts(items: PlannerTask[], field: 'received' | 'assigned' | 'vendorReply') {
    const counts = { today: 0, one: 0, two: 0, threePlus: 0 };
    for (const task of items) {
      const d = daysAgo(getDateField(task, field));
      if (d == null) continue;
      if (d === 0) counts.today += 1;
      else if (d === 1) counts.one += 1;
      else if (d === 2) counts.two += 1;
      else if (d >= 3) counts.threePlus += 1;
    }
    return counts;
  }

  async function loadDashboard() {
    const [summaryData, allTasks] = await Promise.all([api.getDashboardSummary(), api.getTasks()]);
    setSummary(summaryData as DashboardSummary);
    setTasks(allTasks as PlannerTask[]);
  }

  useEffect(() => { void loadDashboard(); }, [isVendor]);

  const filteredTasks = useMemo(() => {
    if (!search.trim()) return tasks;
    return tasks.filter((task) => [task.plannerNo, task.clientName, task.role, task.requirementTitle, task.status]
      .join(' ')
      .toLowerCase()
      .includes(search.toLowerCase()));
  }, [tasks, search]);

  const receivedGroup = useMemo(() => {
    return filteredTasks
      .filter((t) => ['new', 'in review'].includes((t.status || '').toLowerCase()))
      .sort((a, b) => new Date(b.receivedOn).getTime() - new Date(a.receivedOn).getTime());
  }, [filteredTasks]);

  const assignedGroup = useMemo(() => {
    return filteredTasks
      .filter((t) => (t.status || '').toLowerCase().includes('assigned'))
      .sort((a, b) => new Date(getDateField(b, 'assigned')).getTime() - new Date(getDateField(a, 'assigned')).getTime());
  }, [filteredTasks]);

  const vendorReplyGroup = useMemo(() => {
    return filteredTasks
      .filter((t) => (t.status || '').toLowerCase().includes('vendor submitted'))
      .sort((a, b) => new Date(getDateField(b, 'vendorReply')).getTime() - new Date(getDateField(a, 'vendorReply')).getTime());
  }, [filteredTasks]);

  const receivedBuckets = useMemo(() => getBucketCounts(receivedGroup, 'received'), [receivedGroup]);
  const assignedBuckets = useMemo(() => getBucketCounts(assignedGroup, 'assigned'), [assignedGroup]);
  const vendorReplyBuckets = useMemo(() => getBucketCounts(vendorReplyGroup, 'vendorReply'), [vendorReplyGroup]);

  function openBucket(statusGroup: 'new' | 'assigned' | 'vendorReplied', bucket: 'today' | '1' | '2' | '3plus', dateField: 'received' | 'assigned' | 'vendorReply') {
    navigate(`/planners?statusGroup=${encodeURIComponent(statusGroup)}&bucket=${encodeURIComponent(bucket)}&dateField=${encodeURIComponent(dateField)}`);
  }

  async function handleCreateTask(e: React.FormEvent) {
    e.preventDefault();
    setSaving(true); setError('');
    try {
      const created = await api.uploadMail({ file: mailFile, fromEmail: form.fromEmail || 'internal@dbiz.com', emailContent: form.emailContent });
      const createdTask = created as PlannerTask;
      setDrawerOpen(false); setForm(initialForm); setMailFile(null);
      await loadDashboard();
      navigate(`/tasks/${createdTask.id || (createdTask as any).plannerId || (createdTask as any).planner_id}`);
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
            <p className="muted">
              {isVendor
                ? 'View assigned requirements, submit candidates, and track your responses.'
                : 'View your assigned tasks. Admin and recruiters can create new tasks from JD uploads or pasted content.'}
            </p>
          </div>
          {!isVendor && <button className="primary-btn" onClick={() => setDrawerOpen(true)}>+ New Task</button>}
        </section>

        <section className="stats-grid span-2">
          {isVendor ? (
            <>
              <StatCard label="Assigned Queue" value={summary?.assignedQueue ?? summary?.newTasks ?? 0} onClick={() => navigate('/planners')} />
              <StatCard label="Pending Submission" value={summary?.pendingSubmission ?? summary?.underReview ?? 0} onClick={() => navigate('/planners?status=Assigned')} />
              <StatCard label="Replied to Recruiter" value={summary?.repliedToRecruiter ?? summary?.assignedToVendors ?? 0} onClick={() => navigate('/planners?status=Vendor Submitted')} />
              <StatCard label="SLA Today" value={summary?.slaToday ?? summary?.closingToday ?? 0} onClick={() => navigate('/planners?closingToday=true')} />
            </>
          ) : (
            <>
              <StatCard label="New Tasks" value={summary?.newTasks ?? 0} onClick={() => navigate('/planners?status=New')} />
              <StatCard label="Under Review" value={summary?.underReview ?? 0} onClick={() => navigate('/planners?status=In Review')} />
              <StatCard label="Assigned to Vendors" value={summary?.assignedToVendors ?? 0} onClick={() => navigate('/planners?status=Assigned to Vendor')} />
              <StatCard label="Closing Today" value={summary?.closingToday ?? 0} onClick={() => navigate('/planners?closingToday=true')} />
            </>
          )}
        </section>

        <section className="panel span-2">
          <div className="panel-header">
            <div>
              <p className="eyebrow">Timeline</p>
              <h3>Task activity by day</h3>
              <p className="muted">Click a tile to open the matching list (sorted by most recent first).</p>
            </div>
          </div>

          <div className="tile-section">
            <h4>Received (New / In Review)</h4>
            <div className="tile-grid">
              <StatCard label="Today" value={receivedBuckets.today} onClick={() => openBucket('new', 'today', 'received')} />
              <StatCard label="1 day ago" value={receivedBuckets.one} onClick={() => openBucket('new', '1', 'received')} />
              <StatCard label="2 days ago" value={receivedBuckets.two} onClick={() => openBucket('new', '2', 'received')} />
              <StatCard label="3+ days" value={receivedBuckets.threePlus} onClick={() => openBucket('new', '3plus', 'received')} />
            </div>
          </div>

          <div className="tile-section">
            <h4>Submitted to Vendor</h4>
            <div className="tile-grid">
              <StatCard label="Today" value={assignedBuckets.today} onClick={() => openBucket('assigned', 'today', 'assigned')} />
              <StatCard label="1 day ago" value={assignedBuckets.one} onClick={() => openBucket('assigned', '1', 'assigned')} />
              <StatCard label="2 days ago" value={assignedBuckets.two} onClick={() => openBucket('assigned', '2', 'assigned')} />
              <StatCard label="3+ days" value={assignedBuckets.threePlus} onClick={() => openBucket('assigned', '3plus', 'assigned')} />
            </div>
          </div>

          <div className="tile-section">
            <h4>Vendor Replied (Vendor Submitted)</h4>
            <div className="tile-grid">
              <StatCard label="Today" value={vendorReplyBuckets.today} onClick={() => openBucket('vendorReplied', 'today', 'vendorReply')} />
              <StatCard label="1 day ago" value={vendorReplyBuckets.one} onClick={() => openBucket('vendorReplied', '1', 'vendorReply')} />
              <StatCard label="2 days ago" value={vendorReplyBuckets.two} onClick={() => openBucket('vendorReplied', '2', 'vendorReply')} />
              <StatCard label="3+ days" value={vendorReplyBuckets.threePlus} onClick={() => openBucket('vendorReplied', '3plus', 'vendorReply')} />
            </div>
          </div>
        </section>

        <section className="panel review-queue-panel span-2">
          <div className="panel-header"><div><p className="eyebrow">Queue</p><h3>{isVendor ? 'Assigned / Submitted Tasks' : 'Task Queue'}</h3></div></div>
          <input className="search-input" placeholder="Search by planner no, client, role..." value={search} onChange={(e) => setSearch(e.target.value)} />
          <div className="queue-list">
            {filteredTasks.length === 0 && <p className="muted empty-state">No tasks found for your login.</p>}
            {filteredTasks.map((task) => (
              <button key={task.id} type="button" className="queue-item" onClick={() => navigate(`/tasks/${task.id || (task as any).plannerId || (task as any).planner_id}`)}>
                <div className="task-title-row"><strong>{task.requirementTitle || task.role}</strong><span className={`pill pill-${task.priority.toLowerCase()}`}>{task.priority}</span></div>
                <div className="muted">{task.clientName}</div>
                <small>{task.plannerNo} · {task.openPositions} position(s)</small>
                <small>SLA {new Date(task.slaDate).toLocaleDateString()}</small>
                <small>Status: {task.status}</small>
              </button>
            ))}
          </div>
        </section>
      </div>

      {!isVendor && <>
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
      </>}
    </>
  );
}

function StatCard({ label, value, onClick }: { label: string; value: number; onClick?: () => void; }) {
  return <button type="button" className="stat-card clickable" onClick={onClick}><p>{label}</p><h3>{value}</h3></button>;
}
