
import { useEffect, useMemo, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { api } from '../api/client';
import type { PlannerTask, TimelineItem } from '../types';

export function PlannerListPage() {
  const [items, setItems] = useState<PlannerTask[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchParams, setSearchParams] = useSearchParams();
  const navigate = useNavigate();
  const [clientName, setClientName] = useState(searchParams.get('clientName') ?? '');
  const [search, setSearch] = useState(searchParams.get('search') ?? '');
  const [slaDate, setSlaDate] = useState(searchParams.get('slaDate') ?? '');

  useEffect(() => {
    setClientName(searchParams.get('clientName') ?? '');
    setSearch(searchParams.get('search') ?? '');
    setSlaDate(searchParams.get('slaDate') ?? '');
  }, [searchParams]);

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

  function getDateField(task: PlannerTask, field: string | null) {
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

  async function load() {
    setLoading(true);
    const query = new URLSearchParams();
    if (clientName) query.set('clientName', clientName);
    if (search) query.set('search', search);
    if (slaDate) query.set('slaDate', slaDate);
    const status = searchParams.get('status');
    const statusGroup = searchParams.get('statusGroup');
    const closingToday = searchParams.get('closingToday');
    if (status) query.set('status', status);
    if (statusGroup) query.set('statusGroup', statusGroup);
    if (closingToday) query.set('closingToday', closingToday);
    const bucket = searchParams.get('bucket');
    const dateField = searchParams.get('dateField');
    if (bucket) query.set('bucket', bucket);
    if (dateField) query.set('dateField', dateField);
    setSearchParams(query);
    const data = await api.getTasks();
    setItems(data as PlannerTask[]);
    setLoading(false);
  }

  useEffect(() => { load(); }, []);

  const visibleItems = useMemo(() => {
    const status = (searchParams.get('status') ?? '').trim();
    const statusGroup = (searchParams.get('statusGroup') ?? '').trim();
    const closingToday = searchParams.get('closingToday');
    const bucket = (searchParams.get('bucket') ?? '').trim();
    const dateField = searchParams.get('dateField');
    const now = toStartOfDay(new Date());

    function matchesBucket(task: PlannerTask) {
      if (!bucket) return true;
      const dateValue = getDateField(task, dateField);
      const d = dateValue ? daysAgo(dateValue) : null;
      if (d == null) return false;
      if (bucket === 'today') return d === 0;
      if (bucket === '1') return d === 1;
      if (bucket === '2') return d === 2;
      if (bucket === '3plus') return d >= 3;
      return true;
    }

    function matchesClosingToday(task: PlannerTask) {
      if (!closingToday) return true;
      const d = new Date(task.slaDate);
      if (Number.isNaN(d.getTime())) return false;
      return toStartOfDay(d).getTime() === now.getTime();
    }

    const filtered = items
      .filter((task) => {
        if (statusGroup) {
          const s = (task.status || '').toLowerCase();
          if (statusGroup === 'new' && !['new', 'in review'].includes(s)) return false;
          if (statusGroup === 'assigned' && !s.includes('assigned')) return false;
          if (statusGroup === 'vendorReplied' && !s.includes('vendor submitted')) return false;
        } else if (status && task.status !== status) return false;
        if (clientName && !(task.clientName ?? '').toLowerCase().includes(clientName.toLowerCase())) return false;
        if (search) {
          const hay = [task.plannerNo, task.clientName, task.role, task.requirementTitle, task.requirementAsked, task.status]
            .join(' ')
            .toLowerCase();
          if (!hay.includes(search.toLowerCase())) return false;
        }
        if (slaDate) {
          const d = new Date(task.slaDate);
          if (Number.isNaN(d.getTime())) return false;
          const cmp = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
          if (cmp !== slaDate) return false;
        }
        if (!matchesClosingToday(task)) return false;
        if (!matchesBucket(task)) return false;
        return true;
      })
      .sort((a, b) => {
        const aDate = new Date(getDateField(a, dateField));
        const bDate = new Date(getDateField(b, dateField));
        return bDate.getTime() - aDate.getTime();
      });

    return filtered;
  }, [items, searchParams, clientName, search, slaDate]);

  return (
    <div className="page-grid compact-page">
      <section className="panel span-2 compact-filter-panel">
        <div className="panel-header compact-header-row">
          <div><h3>Planner Tasks</h3><small className="muted">{visibleItems.length} task(s)</small></div>
        </div>
        <div className="compact-filters">
          <input value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Search JD / requirement text" />
          <input value={clientName} onChange={(e) => setClientName(e.target.value)} placeholder="Client" />
          <input type="date" value={slaDate} onChange={(e) => setSlaDate(e.target.value)} placeholder="SLA" />
          <button className="primary-btn" onClick={load}>Apply</button>
          <button className="secondary-btn" onClick={() => { setClientName(''); setSearch(''); setSlaDate(''); }}>Reset</button>
        </div>
      </section>
      <section className="panel span-2">
        {loading ? <p>Loading...</p> : (
          <div className="planner-table-wrapper">
            <table className="planner-table">
              <thead><tr><th>Planner No</th><th>Client</th><th>Title</th><th>Role</th><th>Status</th><th>Priority</th><th>Budget</th><th>SLA</th><th>Positions</th></tr></thead>
              <tbody>
                {visibleItems.map((item) => (
                  <tr
                    key={item.id}
                    onClick={() => {
                      if (!item.id) return;
                      navigate(`/tasks/${item.id}`);
                    }}
                    style={{ cursor: 'pointer' }}
                  >
                    <td>{item.plannerNo}</td><td>{item.clientName}</td><td>{item.requirementTitle}</td><td>{item.role}</td><td>{item.status}</td><td>{item.priority}</td><td>{item.currency} {item.budget}</td><td>{new Date(item.slaDate).toLocaleDateString()}</td><td>{item.openPositions}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </div>
  );
}
