import { useEffect, useState } from 'react';
import { api } from '../api/client';
import type { Vendor } from '../types';

const initialVendorForm = {
  name: '',
  email: '',
  coverageRoles: '',
  budgetMin: '0',
  budgetMax: '0',
  status: 'Active'
};

export function VendorsPage() {
  const [vendors, setVendors] = useState<Vendor[]>([]);
  const [form, setForm] = useState(initialVendorForm);
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');

  async function load() {
    setLoading(true);
    try {
      const data = await api.getVendors();
      setVendors(data as Vendor[]);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => { load().catch(console.error); }, []);

  function closeDrawer() {
    setDrawerOpen(false);
    setForm(initialVendorForm);
    setError('');
  }

  async function onSubmit(e: React.FormEvent) {
    e.preventDefault();
    setSaving(true);
    setError('');
    setMessage('');

    try {
      await api.createVendor({
        name: form.name.trim(),
        email: form.email.trim(),
        coverageRoles: form.coverageRoles.trim(),
        budgetMin: Number(form.budgetMin || 0),
        budgetMax: Number(form.budgetMax || 0),
        status: form.status
      });

      setMessage('Vendor created successfully.');
      closeDrawer();
      await load();
    } catch (err: any) {
      setError(err?.message || 'Failed to create vendor');
    } finally {
      setSaving(false);
    }
  }

  return (
    <>
      <section className="panel span-2">
        <div className="panel-header">
          <div>
            <p className="eyebrow">Vendor Directory</p>
            <h2>Vendors</h2>
            <small className="muted">{vendors.length} vendor(s)</small>
          </div>
          <button className="primary-btn" onClick={() => setDrawerOpen(true)}>+ Add Vendor</button>
        </div>

        {message && <div className="success-box">{message}</div>}

        {loading ? <p>Loading vendors...</p> : (
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Name</th>
                  <th>Email</th>
                  <th>Coverage Roles</th>
                  <th>Budget Range</th>
                  <th>Status</th>
                </tr>
              </thead>
              <tbody>
                {vendors.map((vendor) => (
                  <tr key={vendor.id}>
                    <td><strong>{vendor.name}</strong></td>
                    <td>{vendor.email}</td>
                    <td>{vendor.coverageRoles}</td>
                    <td>SGD {vendor.budgetMin} - {vendor.budgetMax}</td>
                    <td>{vendor.status}</td>
                  </tr>
                ))}
                {vendors.length === 0 && (
                  <tr><td colSpan={5} className="muted">No vendors found.</td></tr>
                )}
              </tbody>
            </table>
          </div>
        )}
      </section>

      <div className={`drawer-backdrop ${drawerOpen ? 'open' : ''}`} onClick={closeDrawer} />
      <aside className={`task-drawer ${drawerOpen ? 'open' : ''}`}>
        <div className="drawer-header">
          <div>
            <p className="eyebrow">Vendor Management</p>
            <h2>Add New Vendor</h2>
          </div>
          <button className="icon-btn" onClick={closeDrawer}>×</button>
        </div>

        <form className="drawer-form" onSubmit={onSubmit}>
          <label>Vendor Name
            <input required value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} placeholder="Example: ABC Talent Solutions" />
          </label>

          <label>Email
            <input required type="email" value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} placeholder="vendor@example.com" />
          </label>

          <label>Coverage Roles
            <textarea required rows={4} value={form.coverageRoles} onChange={(e) => setForm({ ...form, coverageRoles: e.target.value })} placeholder="Java, .NET, React, Business Analyst" />
          </label>

          <label>Budget Min
            <input type="number" min="0" value={form.budgetMin} onChange={(e) => setForm({ ...form, budgetMin: e.target.value })} />
          </label>

          <label>Budget Max
            <input type="number" min="0" value={form.budgetMax} onChange={(e) => setForm({ ...form, budgetMax: e.target.value })} />
          </label>

          <label>Status
            <select value={form.status} onChange={(e) => setForm({ ...form, status: e.target.value })}>
              <option value="Active">Active</option>
              <option value="Inactive">Inactive</option>
            </select>
          </label>

          {error && <div className="error-box">{error}</div>}

          <div className="drawer-actions">
            <button type="button" className="secondary-btn" onClick={closeDrawer}>Cancel</button>
            <button className="primary-btn" disabled={saving}>{saving ? 'Saving...' : 'Create Vendor'}</button>
          </div>
        </form>
      </aside>
    </>
  );
}
