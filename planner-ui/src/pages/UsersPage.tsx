import { useEffect, useState } from 'react';
import { api } from '../api/client';
import type { Vendor } from '../types';

type RoleItem = { roleCode: string; roleName: string };
type UserListItem = { userId: number; fullName: string; email: string; roleCode: string; vendorId?: number | null; isActive: boolean; isFirstLogin: boolean; isLocked: boolean; createdOn: string };

const initialForm = { fullName: '', email: '', roleCode: 'RECRUITER', vendorId: '', isActive: true };

export function UsersPage() {
  const [users, setUsers] = useState<UserListItem[]>([]);
  const [roles, setRoles] = useState<RoleItem[]>([]);
  const [vendors, setVendors] = useState<Vendor[]>([]);
  const [form, setForm] = useState(initialForm);
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');

  async function load() {
    setLoading(true);
    try {
      const [u, r, v] = await Promise.all([api.getUsers(), api.getRoles(), api.getVendors()]);
      setUsers(u as UserListItem[]);
      setRoles(r as RoleItem[]);
      setVendors(v as Vendor[]);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => { load().catch(console.error); }, []);

  function closeDrawer() {
    setDrawerOpen(false);
    setForm(initialForm);
    setError('');
  }

  async function onSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError('');
    setMessage('');
    setSaving(true);

    try {
      await api.createUser({ ...form, vendorId: form.vendorId ? Number(form.vendorId) : null });
      setMessage('User created. Activation email has been simulated in API logs.');
      closeDrawer();
      await load();
    } catch (err: any) {
      setError(err?.message || 'Failed to create user');
    } finally {
      setSaving(false);
    }
  }

  function vendorName(vendorId?: number | null) {
    if (!vendorId) return '-';
    return vendors.find((v) => Number(v.id) === Number(vendorId))?.name ?? String(vendorId);
  }

  return (
    <>
      <section className="panel span-2">
        <div className="panel-header">
          <div>
            <p className="eyebrow">User Management</p>
            <h2>Users</h2>
            <small className="muted">{users.length} user(s)</small>
          </div>
          <button className="primary-btn" onClick={() => setDrawerOpen(true)}>+ Add User</button>
        </div>

        {message && <div className="success-box">{message}</div>}

        {loading ? <p>Loading users...</p> : (
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Name</th>
                  <th>Email</th>
                  <th>Role</th>
                  <th>Vendor</th>
                  <th>Status</th>
                  <th>First Login</th>
                  <th>Created</th>
                </tr>
              </thead>
              <tbody>
                {users.map((user) => (
                  <tr key={user.userId}>
                    <td><strong>{user.fullName}</strong></td>
                    <td>{user.email}</td>
                    <td>{user.roleCode}</td>
                    <td>{vendorName(user.vendorId)}</td>
                    <td>{user.isActive ? 'Active' : 'Inactive'}</td>
                    <td>{user.isFirstLogin ? 'Pending' : 'Completed'}</td>
                    <td>{new Date(user.createdOn).toLocaleString()}</td>
                  </tr>
                ))}
                {users.length === 0 && (
                  <tr><td colSpan={7} className="muted">No users found.</td></tr>
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
            <p className="eyebrow">Super Admin</p>
            <h2>Add New User</h2>
          </div>
          <button className="icon-btn" onClick={closeDrawer}>×</button>
        </div>

        <form className="drawer-form" onSubmit={onSubmit}>
          <label>Full Name
            <input required value={form.fullName} onChange={(e) => setForm({ ...form, fullName: e.target.value })} placeholder="Full name" />
          </label>

          <label>Email
            <input required type="email" value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} placeholder="user@dbiz.com" />
          </label>

          <label>Role
            <select value={form.roleCode} onChange={(e) => setForm({ ...form, roleCode: e.target.value, vendorId: e.target.value === 'VENDOR' ? form.vendorId : '' })}>
              {roles.map((role) => <option key={role.roleCode} value={role.roleCode}>{role.roleName}</option>)}
            </select>
          </label>

          {form.roleCode === 'VENDOR' && (
            <label>Vendor
              <select required value={form.vendorId} onChange={(e) => setForm({ ...form, vendorId: e.target.value })}>
                <option value="">Select vendor</option>
                {vendors.filter((v) => (v.status || '').toLowerCase() !== 'inactive').map((vendor) => (
                  <option key={vendor.id} value={vendor.id}>{vendor.name}</option>
                ))}
              </select>
            </label>
          )}

          <label className="checkbox-row">
            <input type="checkbox" checked={form.isActive} onChange={(e) => setForm({ ...form, isActive: e.target.checked })} /> Active
          </label>

          {error && <div className="error-box">{error}</div>}

          <div className="drawer-actions">
            <button type="button" className="secondary-btn" onClick={closeDrawer}>Cancel</button>
            <button className="primary-btn" disabled={saving}>{saving ? 'Saving...' : 'Create User'}</button>
          </div>
        </form>
      </aside>
    </>
  );
}
