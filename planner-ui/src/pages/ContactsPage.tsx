import { useEffect, useState } from 'react';
import { api } from '../api/client';
import type { PlannerContact } from '../types';

const emptyContact = (): PlannerContact => ({
  plannerId: 0,
  name: '',
  title: '',
  email: '',
  phone: '',
  agency: '',
  contactLevel: '',
  isPrimary: false
});

export function ContactsPage() {
  const [contacts, setContacts] = useState<PlannerContact[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');

  async function loadContacts() {
    setLoading(true);
    setError('');
    try {
      const data = await api.getContacts();
      setContacts(data as PlannerContact[]);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load contacts');
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    loadContacts();
  }, []);

  function updateContact(index: number, patch: Partial<PlannerContact>) {
    setContacts((current) => {
      const next = [...current];
      const updated = { ...next[index], ...patch };
      if (patch.isPrimary && updated.plannerId) {
        next.forEach((item) => {
          if (item.plannerId === updated.plannerId) item.isPrimary = false;
        });
      }
      next[index] = updated;
      return next;
    });
  }

  function addRows(count = 3) {
    setContacts((current) => [...current, ...Array.from({ length: count }, emptyContact)]);
  }

  function removeRow(index: number) {
    setContacts((current) => current.filter((_, i) => i !== index));
  }

  async function saveContacts() {
    setSaving(true);
    setMessage('');
    setError('');
    try {
      const rowsToSave = contacts.filter((x) => x.plannerId && (x.name || x.email || x.phone || x.title || x.agency));
      await api.saveContacts(rowsToSave);
      setMessage('Contacts saved successfully.');
      await loadContacts();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to save contacts');
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="page-stack">
      <section className="hero-card compact-hero">
        <div>
          <p className="eyebrow">Client contacts</p>
          <h2>Contact List</h2>
          <p>View and maintain multiple contacts for each planner task.</p>
        </div>
        <div className="hero-actions">
          <button className="secondary-btn" type="button" onClick={() => addRows(3)}>+ Add 3 Rows</button>
          <button className="primary-btn" type="button" onClick={saveContacts} disabled={saving}>{saving ? 'Saving...' : 'Save Contacts'}</button>
        </div>
      </section>

      {message && <div className="success-box">{message}</div>}
      {error && <div className="error-box">{error}</div>}

      <section className="table-card">
        {loading ? (
          <p>Loading contacts...</p>
        ) : (
          <div className="table-scroll">
            <table className="data-table">
              <thead>
                <tr>
                  <th>Task Id</th>
                  <th>Name</th>
                  <th>Contact Mobile</th>
                  <th>Email</th>
                  <th>Primary</th>
                  <th>Level</th>
                  <th>Title</th>
                  <th>Agency</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                {contacts.map((contact, index) => (
                  <tr key={`${contact.id ?? 'new'}-${index}`}>
                    <td><input type="number" value={contact.plannerId ?? 0} onChange={(e) => updateContact(index, { plannerId: Number(e.target.value) })} /></td>
                    <td><input value={contact.name} onChange={(e) => updateContact(index, { name: e.target.value })} /></td>
                    <td><input value={contact.phone} onChange={(e) => updateContact(index, { phone: e.target.value })} /></td>
                    <td><input value={contact.email} onChange={(e) => updateContact(index, { email: e.target.value })} /></td>
                    <td style={{ textAlign: 'center' }}><input type="checkbox" checked={contact.isPrimary} onChange={(e) => updateContact(index, { isPrimary: e.target.checked })} style={{ width: 'auto' }} /></td>
                    <td><input value={contact.contactLevel} onChange={(e) => updateContact(index, { contactLevel: e.target.value })} /></td>
                    <td><input value={contact.title} onChange={(e) => updateContact(index, { title: e.target.value })} /></td>
                    <td><input value={contact.agency} onChange={(e) => updateContact(index, { agency: e.target.value })} /></td>
                    <td><button className="secondary-btn" type="button" onClick={() => removeRow(index)}>Remove</button></td>
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
