import { useEffect, useState } from 'react';
import { api } from '../api/client';
import type { Candidate } from '../types';

export function CandidatesPage() {
  const [candidates, setCandidates] = useState<Candidate[]>([]);

  useEffect(() => {
    api.getCandidates().then((data) => setCandidates(data as Candidate[]));
  }, []);

  return (
    <section className="panel">
      <div className="panel-header">
        <div>
          <p className="eyebrow">Candidate Bank</p>
          <h2>Candidate listing</h2>
        </div>
      </div>
      <div className="card-grid">
        {candidates.map((candidate) => (
          <div key={candidate.id} className="mini-card">
            <strong>{candidate.name}</strong>
            <p>{candidate.currentRole}</p>
            <small>{candidate.experienceYears} years · {candidate.noticePeriod}</small>
            <small>{candidate.location} · SGD {candidate.expectedBudget}</small>
            <div className="chip-row">{candidate.skills.map((skill) => <span key={skill} className="chip">{skill}</span>)}</div>
          </div>
        ))}
      </div>
    </section>
  );
}
