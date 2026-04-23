import { useEffect, useState } from 'react';
import { api } from '../api/client';

type RoleItem = { roleCode: string; roleName: string };
type UserListItem = { userId: number; fullName: string; email: string; roleCode: string; vendorId?: number | null; isActive: boolean; isFirstLogin: boolean; isLocked: boolean; createdOn: string };

const initialForm = { fullName: '', email: '', roleCode: 'RECRUITER', vendorId: '', isActive: true };

export function UsersPage() {
  const [users, setUsers] = useState<UserListItem[]>([]);
  const [roles, setRoles] = useState<RoleItem[]>([]);
  const [form, setForm] = useState(initialForm);
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');

  async function load() {
    const [u, r] = await Promise.all([api.getUsers(), api.getRoles()]);
    setUsers(u as UserListItem[]);
    setRoles(r as RoleItem[]);
  }

  useEffect(() => { load().catch(console.error); }, []);

  async function onSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(''); setMessage('');
    try {
      await api.createUser({ ...form, vendorId: form.vendorId ? Number(form.vendorId) : null });
      setMessage('User created. Activation email has been simulated in API logs.');
      setForm(initialForm);
      await load();
    } catch (err: any) {
      setError(err?.message || 'Failed to create user');
    }
  }

  return (
    <div className="page-grid">
      <section className="panel">
        <div className="panel-header"><div><p className="eyebrow">Super Admin</p><h2>Create user</h2></div></div>
        <form className="drawer-form no-pad" onSubmit={onSubmit}>
          <label>Full Name<input value={form.fullName} onChange={(e) => setForm({ ...form, fullName: e.target.value })} /></label>
          <label>Email<input value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} /></label>
          <label>Role
            <select value={form.roleCode} onChange={(e) => setForm({ ...form, roleCode: e.target.value })}>
              {roles.map((role) => <option key={role.roleCode} value={role.roleCode}>{role.roleName}</option>)}
            </select>
          </label>
          <label>Vendor Id (only for vendor users)<input value={form.vendorId} onChange={(e) => setForm({ ...form, vendorId: e.target.value })} /></label>
          <label className="checkbox-row"><input type="checkbox" checked={form.isActive} onChange={(e) => setForm({ ...form, isActive: e.target.checked })} /> Active</label>
          {message && <div className="success-box">{message}</div>}
          {error && <div className="error-box">{error}</div>}
          <button className="primary-btn">Create User</button>
        </form>
      </section>
      <section className="panel span-2">
        <div className="panel-header"><div><p className="eyebrow">User Management</p><h2>Users</h2></div></div>
        <div className="table-wrap"><table><thead><tr><th>Name</th><th>Email</th><th>Role</th><th>Vendor</th><th>Status</th><th>First Login</th><th>Created</th></tr></thead><tbody>
          {users.map((user) => <tr key={user.userId}><td>{user.fullName}</td><td>{user.email}</td><td>{user.roleCode}</td><td>{user.vendorId ?? '-'}</td><td>{user.isActive ? 'Active' : 'Inactive'}</td><td>{user.isFirstLogin ? 'Pending' : 'Completed'}</td><td>{new Date(user.createdOn).toLocaleString()}</td></tr>)}
        </tbody></table></div>
      </section>
    </div>
  );
}
