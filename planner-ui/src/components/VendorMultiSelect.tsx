import { useEffect, useMemo, useRef, useState } from 'react';

export type VendorOption = {
  id: number;
  name: string;
  coverageRoles: string;
  budgetMin: number;
  budgetMax: number;
  status: string;
};

type VendorMultiSelectProps = {
  vendors: VendorOption[];
  selectedVendorIds: number[];
  onChange: (ids: number[]) => void;
  placeholder?: string;
};

export function VendorMultiSelect({
  vendors,
  selectedVendorIds,
  onChange,
  placeholder = 'Search and select vendors...'
}: VendorMultiSelectProps) {
  const [query, setQuery] = useState('');
  const [open, setOpen] = useState(false);
  const wrapperRef = useRef<HTMLDivElement | null>(null);

  useEffect(() => {
    function handleOutside(event: MouseEvent) {
      if (!wrapperRef.current) return;
      if (!wrapperRef.current.contains(event.target as Node)) {
        setOpen(false);
      }
    }

    document.addEventListener('mousedown', handleOutside);
    return () => document.removeEventListener('mousedown', handleOutside);
  }, []);

  const activeVendors = useMemo(
    () => vendors.filter((v) => (v.status || '').toLowerCase() !== 'inactive'),
    [vendors]
  );

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    if (!q) return activeVendors;

    return activeVendors.filter((vendor) =>
      [
        vendor.name,
        vendor.coverageRoles,
        String(vendor.budgetMin),
        String(vendor.budgetMax)
      ]
        .join(' ')
        .toLowerCase()
        .includes(q)
    );
  }, [activeVendors, query]);

  const selectedVendors = useMemo(
    () => activeVendors.filter((v) => selectedVendorIds.includes(v.id)),
    [activeVendors, selectedVendorIds]
  );

  function toggleVendor(id: number) {
    if (selectedVendorIds.includes(id)) {
      onChange(selectedVendorIds.filter((x) => x !== id));
    } else {
      onChange([...selectedVendorIds, id]);
    }
  }

  function removeVendor(id: number) {
    onChange(selectedVendorIds.filter((x) => x !== id));
  }

  return (
    <div className="vendor-multiselect" ref={wrapperRef}>
      <div
        className={`vendor-select-box ${open ? 'open' : ''}`}
        onClick={() => setOpen(true)}
      >
        <div className="selected-tags">
          {selectedVendors.length === 0 ? (
            <span className="placeholder">{placeholder}</span>
          ) : (
            selectedVendors.map((vendor) => (
              <span key={vendor.id} className="selected-tag">
                {vendor.name}
                <button
                  type="button"
                  className="remove-tag-btn"
                  onClick={(e) => {
                    e.stopPropagation();
                    removeVendor(vendor.id);
                  }}
                >
                  ×
                </button>
              </span>
            ))
          )}
        </div>

        <input
          type="text"
          className="vendor-search-input"
          value={query}
          onChange={(e) => {
            setQuery(e.target.value);
            setOpen(true);
          }}
          onFocus={() => setOpen(true)}
          placeholder={selectedVendors.length > 0 ? '' : placeholder}
        />
      </div>

      {open && (
        <div className="vendor-dropdown">
          {filtered.length === 0 ? (
            <div className="vendor-option empty">No vendors found.</div>
          ) : (
            filtered.map((vendor) => {
              const selected = selectedVendorIds.includes(vendor.id);

              return (
                <div
                  key={vendor.id}
                  className={`vendor-option ${selected ? 'selected' : ''}`}
                  onClick={() => toggleVendor(vendor.id)}
                >
                  <input
                    type="checkbox"
                    checked={selected}
                    onChange={() => toggleVendor(vendor.id)}
                    onClick={(e) => e.stopPropagation()}
                  />
                  <div className="vendor-option-content">
                    <strong>{vendor.name}</strong>
                    <small>{vendor.coverageRoles}</small>
                    <small>
                      Budget {vendor.budgetMin} - {vendor.budgetMax}
                    </small>
                  </div>
                </div>
              );
            })
          )}
        </div>
      )}
    </div>
  );
}