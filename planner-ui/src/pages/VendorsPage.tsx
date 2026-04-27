import { useEffect, useState } from 'react';
import { api } from '../api/client';
import type { Vendor } from '../types';

export function VendorsPage() {
  const [vendors, setVendors] = useState<Vendor[]>([]);

  useEffect(() => {
    api.getVendors().then((data) => setVendors(data as Vendor[]));
  }, []);

  return (
    <section className="panel">
      <div className="panel-header">
        <div>
          <p className="eyebrow">Vendor Directory</p>
          <h2>Available vendors</h2>
        </div>
      </div>
      <div className="card-grid">
        {vendors.map((vendor) => (
          <div key={vendor.id} className="mini-card" style={{ display: 'flex', flexDirection: 'column', gap: '0.25rem' }}>
            <strong>{vendor.name}</strong>
            <p style={{ color: 'var(--text-secondary)', fontSize: '0.875rem' }}>{vendor.coverageRoles}</p>
            <div style={{ marginTop: '0.5rem', fontSize: '0.875rem' }}>
              <div><strong>UEN:</strong> {vendor.uenNo || '-'}</div>
              <div><strong>Email:</strong> {vendor.email}</div>
              <div><strong>Budget:</strong> SGD {vendor.budgetMin} - {vendor.budgetMax}</div>
              <div><strong>Location:</strong> {vendor.sourcingLocation} / {vendor.servingLocation}</div>
              <div style={{ marginTop: '0.5rem' }}>
                <strong>POC:</strong> {vendor.pocName} <br/>
                <span style={{ color: 'var(--text-secondary)' }}>{vendor.pocEmail} | {vendor.pocPhone}</span>
              </div>
            </div>
          </div>
        ))}
      </div>
    </section>
  );
}
