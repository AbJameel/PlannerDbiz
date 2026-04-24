
import { useEffect, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { api } from '../api/client';

type PlannerListApiItem = {
  id: number;
  plannerNo: string;
  clientName: string;
  requirementTitle: string;
  role: string;
  status: string;
  priority: string;
  budget: number;
  currency: string;
  slaDate: string;
  openPositions: number;
};

type PlannerListApiResponse = { items: PlannerListApiItem[]; totalCount: number };

export function PlannerListPage() {
  const [items, setItems] = useState<PlannerListApiItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchParams, setSearchParams] = useSearchParams();
  const navigate = useNavigate();
  const [clientName, setClientName] = useState(searchParams.get('clientName') ?? '');
  const [search, setSearch] = useState(searchParams.get('search') ?? '');
  const [slaDate, setSlaDate] = useState(searchParams.get('slaDate') ?? '');

  async function load() {
    setLoading(true);
    const query = new URLSearchParams();
    if (clientName) query.set('clientName', clientName);
    if (search) query.set('search', search);
    if (slaDate) query.set('slaDate', slaDate);
    const status = searchParams.get('status');
    const closingToday = searchParams.get('closingToday');
    if (status) query.set('status', status);
    if (closingToday) query.set('closingToday', closingToday);
    setSearchParams(query);
    const data = await api.getPlannerList(query.toString()) as PlannerListApiResponse;
    setItems(data.items ?? []);
    setLoading(false);
  }

  useEffect(() => { load(); }, []);

  return (
    <div className="page-grid compact-page">
      <section className="panel span-2 compact-filter-panel">
        <div className="panel-header compact-header-row">
          <div><h3>Planner Tasks</h3><small className="muted">{items.length} task(s)</small></div>
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
                {items.map((item) => (
                  <tr key={item.id} onClick={() => navigate(`/tasks/${item.id}`)} style={{ cursor: 'pointer' }}>
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
