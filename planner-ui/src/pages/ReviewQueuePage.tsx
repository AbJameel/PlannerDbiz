import { useEffect, useMemo, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { api } from '../api/client';
import type { Candidate, PlannerTask, Vendor } from '../types';

export function ReviewQueuePage() {
  const [tasks, setTasks] = useState<PlannerTask[]>([]);
  const [selectedTask, setSelectedTask] = useState<PlannerTask | null>(null);
  const [candidates, setCandidates] = useState<Candidate[]>([]);
  const [vendors, setVendors] = useState<Vendor[]>([]);
  const [selectedVendorIds, setSelectedVendorIds] = useState<number[]>([]);
  const [edit, setEdit] = useState<{
    role: string;
    clientName: string;
    budget: string;
    currency: string;
    slaDate: string;
    openPositions: string;
    requirementAsked: string;
    skillsText: string;
    notes: string;
  } | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [assigning, setAssigning] = useState(false);
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');
  const [searchParams] = useSearchParams();

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    api.getReviewQueue()
      .then((data: unknown) => {
        if (cancelled) return;
        setTasks(data as PlannerTask[]);
      })
      .finally(() => {
        if (cancelled) return;
        setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, []);

  const reviewTasks = useMemo(() => {
    return tasks.filter((task) => {
      const status = task.status?.toLowerCase?.() ?? '';
      return status.includes('review') || status === 'new';
    });
  }, [tasks]);

  const selectedTaskId = useMemo(() => {
    const id = searchParams.get('taskId');
    return id ? String(id) : (reviewTasks[0]?.id != null ? String(reviewTasks[0].id) : null);
  }, [searchParams, reviewTasks]);

  useEffect(() => {
    let cancelled = false;
    if (!selectedTaskId) {
      setSelectedTask(null);
      setEdit(null);
      setCandidates([]);
      setSelectedVendorIds([]);
      return () => {
        cancelled = true;
      };
    }
    api.getTask(selectedTaskId).then((data) => {
      if (cancelled) return;
      const t = data as PlannerTask;
      setSelectedTask(t);
      setCandidates([]);
      setSelectedVendorIds((t.assignedVendorIds ?? []).map((x) => Number(x)));
      setEdit({
        role: t.role ?? '',
        clientName: t.clientName ?? '',
        budget: String(t.budget ?? ''),
        currency: t.currency ?? '',
        slaDate: t.slaDate ?? '',
        openPositions: String(t.openPositions ?? ''),
        requirementAsked: t.requirementAsked ?? '',
        skillsText: (t.skills ?? []).join(', '),
        notes: String((t as any)?.notes ?? '')
      });
      api.getRecommendedCandidates(selectedTaskId).then((c) => {
        if (cancelled) return;
        setCandidates(c as Candidate[]);
      });
    });
    return () => {
      cancelled = true;
    };
  }, [selectedTaskId]);

  useEffect(() => {
    let cancelled = false;
    api.getVendors().then((data) => {
      if (cancelled) return;
      setVendors(data as Vendor[]);
    });
    return () => {
      cancelled = true;
    };
  }, []);

  async function onSave() {
    if (!selectedTaskId || !selectedTask || !edit) return;
    setSaving(true);
    setMessage('');
    setError('');
    try {
      const payload = {
        role: edit.role,
        clientName: edit.clientName,
        budget: edit.budget ? Number(edit.budget) : null,
        currency: edit.currency,
        slaDate: edit.slaDate,
        openPositions: edit.openPositions ? Number(edit.openPositions) : null,
        requirementAsked: edit.requirementAsked,
        skills: edit.skillsText
          .split(',')
          .map((s) => s.trim())
          .filter(Boolean),
        notes: edit.notes
      };
      const updated = await api.updateTask(selectedTaskId, payload);
      setSelectedTask(updated as PlannerTask);
      setMessage('Saved.');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to save');
    } finally {
      setSaving(false);
    }
  }

  async function onAssign() {
    if (!selectedTaskId || !selectedTask || !edit) return;
    setMessage('');
    setError('');
    const positions = Number(edit.openPositions);
    if (!edit.role.trim()) {
      setError('Role is required before assigning to vendors.');
      return;
    }
    if (!edit.slaDate) {
      setError('SLA date is required before assigning to vendors.');
      return;
    }
    if (!Number.isFinite(positions) || positions <= 0) {
      setError('Number of positions must be greater than 0 before assigning to vendors.');
      return;
    }
    if (selectedVendorIds.length === 0) {
      setError('Select at least one vendor to assign.');
      return;
    }
    const status = selectedTask.status?.toLowerCase?.() ?? '';
    if (status.includes('closed')) {
      setError('Closed tasks cannot be assigned to vendors.');
      return;
    }
    setAssigning(true);
    try {
      await onSave();
      const updated = await api.assignVendors(selectedTaskId, { vendorIds: selectedVendorIds });
      setSelectedTask(updated as PlannerTask);
      setMessage('Assigned to vendor queue.');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to assign vendors');
    } finally {
      setAssigning(false);
    }
  }

  if (loading) return <div className="panel">Loading review queue...</div>;

  return (
    <div className="page-grid">
      <section className="panel">
        <div className="panel-header">
          <div>
            <p className="eyebrow">My Review Queue</p>
            <h2>Tasks awaiting recruiter review</h2>
            <p className="muted">{reviewTasks.length} tasks</p>
          </div>
        </div>
        <div className="stack-list">
          {reviewTasks.map((task) => (
            <Link key={task.id} to={`/review?taskId=${task.id}`} className="task-item">
              <div>
                <div className="task-title-row">
                  <strong>{task.role}</strong>
                  <span className={`pill pill-${task.priority.toLowerCase()}`}>{task.status}</span>
                </div>
                <p>{task.clientName}</p>
                <small>{task.plannerNo} · {task.openPositions} positions · SLA {new Date(task.slaDate).toLocaleDateString()}</small>
              </div>
              <div className="amount-box">{task.currency} {task.budget.toLocaleString()}</div>
            </Link>
          ))}
        </div>
      </section>

      <section className="panel">
        {!selectedTask || !edit ? (
          <div className="muted">Select a task to review.</div>
        ) : (
          <>
            <div className="panel-header">
              <div>
                <p className="eyebrow">Review / Edit Task</p>
                <h2>{selectedTask.plannerNo}</h2>
                <p className="muted">{selectedTask.status} · Created {new Date(selectedTask.receivedOn).toLocaleString()} · {selectedTask.sourceType}</p>
              </div>
              <Link className="secondary-btn" to={`/tasks/${selectedTask.id}`}>Open full screen</Link>
            </div>

            <form className="drawer-form no-pad" onSubmit={(e) => { e.preventDefault(); void onSave(); }}>
              <label>Client Name<input value={edit.clientName} onChange={(e) => setEdit({ ...edit, clientName: e.target.value })} /></label>
              <label>Role<input value={edit.role} onChange={(e) => setEdit({ ...edit, role: e.target.value })} /></label>
              <label>Budget<input type="number" value={edit.budget} onChange={(e) => setEdit({ ...edit, budget: e.target.value })} /></label>
              <label>Currency<input value={edit.currency} onChange={(e) => setEdit({ ...edit, currency: e.target.value })} /></label>
              <label>SLA Date<input type="datetime-local" value={toDateTimeLocal(edit.slaDate)} onChange={(e) => setEdit({ ...edit, slaDate: fromDateTimeLocal(e.target.value) })} /></label>
              <label>Number of Positions<input type="number" value={edit.openPositions} onChange={(e) => setEdit({ ...edit, openPositions: e.target.value })} /></label>
              <label>Requirement Asked<textarea rows={6} value={edit.requirementAsked} onChange={(e) => setEdit({ ...edit, requirementAsked: e.target.value })} /></label>
              <label>Skills (comma separated)<input value={edit.skillsText} onChange={(e) => setEdit({ ...edit, skillsText: e.target.value })} /></label>
              <label>Notes<textarea rows={4} value={edit.notes} onChange={(e) => setEdit({ ...edit, notes: e.target.value })} /></label>
              {message && <div className="success-box">{message}</div>}
              {error && <div className="error-box">{error}</div>}
              <div className="drawer-actions">
                <button type="submit" className="primary-btn" disabled={saving}>{saving ? 'Saving...' : 'Save'}</button>
              </div>
            </form>

            <div className="stack-section">
              <h3>Vendor Assignment</h3>
              <div className="stack-list">
                {vendors.map((vendor) => (
                  <label key={vendor.id} className="checkbox-row">
                    <input
                      type="checkbox"
                      checked={selectedVendorIds.includes(vendor.id)}
                      onChange={(e) => {
                        if (e.target.checked) setSelectedVendorIds([...selectedVendorIds, vendor.id]);
                        else setSelectedVendorIds(selectedVendorIds.filter((id) => id !== vendor.id));
                      }}
                    />
                    <span>{vendor.name}</span>
                  </label>
                ))}
              </div>
              <div className="drawer-actions">
                <button className="primary-btn" onClick={() => { void onAssign(); }} disabled={assigning || selectedVendorIds.length === 0}>
                  {assigning ? 'Assigning...' : 'Assign to Vendor'}
                </button>
              </div>
            </div>

            <div className="stack-section">
              <h3>Recommended Candidates</h3>
              {candidates.length === 0 ? (
                <p className="muted">No recommendations available.</p>
              ) : (
                <div className="stack-list">
                  {candidates.map((candidate) => (
                    <div key={candidate.id} className="mini-card">
                      <strong>{candidate.name}</strong>
                      <p>{candidate.currentRole}</p>
                      <small>{candidate.experienceYears} years · {candidate.noticePeriod}</small>
                    </div>
                  ))}
                </div>
              )}
            </div>

            <div className="stack-section">
              <h3>Timeline</h3>
              {selectedTask.timeline?.length ? (
                <div className="timeline">
                  {selectedTask.timeline.map((item, index) => (
                    <div key={index} className="timeline-item">
                      <div className="timeline-dot" />
                      <div>
                        <strong>{item.title}</strong>
                        <p>{item.description}</p>
                        <small>{new Date(item.happenedOn).toLocaleString()} · {item.performedBy}</small>
                      </div>
                    </div>
                  ))}
                </div>
              ) : (
                <p className="muted">No timeline activity.</p>
              )}
            </div>
          </>
        )}
      </section>
    </div>
  );
}

function toDateTimeLocal(value: string) {
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return '';
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

function fromDateTimeLocal(value: string) {
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return '';
  return d.toISOString();
}
