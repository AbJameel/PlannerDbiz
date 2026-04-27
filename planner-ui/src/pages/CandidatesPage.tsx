import { useEffect, useMemo, useState } from 'react';
import { api } from '../api/client';
import { getSession } from '../lib/auth';
import type { Candidate, VendorCandidateSubmission } from '../types';

export function CandidatesPage() {
  const session = getSession();
  const isVendor = session?.roleCode === 'VENDOR';
  const [candidates, setCandidates] = useState<Candidate[]>([]);
  const [submitted, setSubmitted] = useState<VendorCandidateSubmission[]>([]);
  const [search, setSearch] = useState('');

  async function load() {
    if (isVendor) {
      const data = await api.getVendorSubmittedCandidates();
      setSubmitted(data as VendorCandidateSubmission[]);
    } else {
      const data = await api.getCandidates();
      setCandidates(data as Candidate[]);
    }
  }

  useEffect(() => { void load(); }, [isVendor]);

  const filteredSubmitted = useMemo(() => {
    const text = search.trim().toLowerCase();
    if (!text) return submitted;
    return submitted.filter((c) => [c.candidateName, c.contactDetail, c.visaType, c.resumeFile, c.candidateStatus]
      .join(' ')
      .toLowerCase()
      .includes(text));
  }, [submitted, search]);

  const filteredCandidates = useMemo(() => {
    const text = search.trim().toLowerCase();
    if (!text) return candidates;
    return candidates.filter((c) => [c.name, c.currentRole, c.location, ...(c.skills ?? [])]
      .join(' ')
      .toLowerCase()
      .includes(text));
  }, [candidates, search]);

  return (
    <section className="panel full-width-panel">
      <div className="panel-header">
        <div>
          <p className="eyebrow">Candidates</p>
          <h3>{isVendor ? 'My Submitted Candidates' : 'Candidate Pool'}</h3>
          <p className="muted">
            {isVendor
              ? 'View candidates you already submitted for assigned planner tasks. You can filter by name, skill/contact text, and download resumes.'
              : 'View reusable candidate profiles for recommendation and matching.'}
          </p>
        </div>
      </div>

      <input
        className="search-input"
        placeholder={isVendor ? 'Filter by candidate, skill/contact, visa, resume...' : 'Filter by skill, role, location...'}
        value={search}
        onChange={(e) => setSearch(e.target.value)}
      />

      <div className="queue-list">
        {isVendor ? (
          <>
            {filteredSubmitted.length === 0 && <p className="muted empty-state">No submitted candidates found.</p>}
            {filteredSubmitted.map((candidate) => (
              <div key={candidate.submissionId} className="mini-card">
                <strong>{candidate.candidateName || 'Unnamed Candidate'}</strong>
                <p>{candidate.contactDetail || 'No contact details'}</p>
                <small>Planner #{candidate.plannerId} · {candidate.visaType || 'Visa not mentioned'} · {candidate.candidateStatus}</small>
                <small>Submitted: {new Date(candidate.createdOn).toLocaleDateString()}</small>
                {candidate.resumeFile ? (
                  <a className="secondary-btn inline-link" href={candidate.resumeFile} target="_blank" rel="noreferrer">Download Resume</a>
                ) : <small>No resume uploaded</small>}
              </div>
            ))}
          </>
        ) : (
          <>
            {filteredCandidates.length === 0 && <p className="muted empty-state">No candidates found.</p>}
            {filteredCandidates.map((candidate) => (
              <div key={candidate.id} className="mini-card">
                <strong>{candidate.name}</strong>
                <p>{candidate.currentRole}</p>
                <small>{candidate.experienceYears} years · {candidate.noticePeriod}</small>
                <small>{candidate.location} · SGD {candidate.expectedBudget}</small>
                <div className="chip-row">{candidate.skills.map((skill) => <span key={skill} className="chip">{skill}</span>)}</div>
              </div>
            ))}
          </>
        )}
      </div>
    </section>
  );
}
