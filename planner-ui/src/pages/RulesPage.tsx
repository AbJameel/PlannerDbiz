import { useEffect, useState } from 'react';
import { api } from '../api/client';
import type { Rule } from '../types';

export function RulesPage() {
  const [rules, setRules] = useState<Rule[]>([]);

  useEffect(() => {
    api.getRules().then((data) => setRules(data as Rule[]));
  }, []);

  return (
    <section className="panel">
      <div className="panel-header">
        <div>
          <p className="eyebrow">Rule Engine</p>
          <h2>Rule catalogue</h2>
        </div>
      </div>
      <div className="table-wrap">
        <table>
          <thead><tr><th>Name</th><th>Category</th><th>Condition</th><th>Outcome</th><th>Status</th></tr></thead>
          <tbody>
            {rules.map((rule) => (
              <tr key={rule.id}>
                <td>{rule.name}</td>
                <td>{rule.category}</td>
                <td>{rule.condition}</td>
                <td>{rule.outcome}</td>
                <td>{rule.isActive ? 'Active' : 'Inactive'}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}
