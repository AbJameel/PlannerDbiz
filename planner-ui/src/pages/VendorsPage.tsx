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
          <div key={vendor.id} className="mini-card">
            <strong>{vendor.name}</strong>
            <p>{vendor.coverageRoles}</p>
            <small>{vendor.email}</small>
            <small>Budget: SGD {vendor.budgetMin} - {vendor.budgetMax}</small>
          </div>
        ))}
      </div>
    </section>
  );
}
