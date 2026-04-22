import { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import { api } from '../api/client';
import type { Candidate, PlannerTask } from '../types';

export function TaskDetailPage() {
  const { id } = useParams();
  const [task, setTask] = useState<PlannerTask | null>(null);
  const [candidates, setCandidates] = useState<Candidate[]>([]);

  useEffect(() => {
    if (!id) return;
    api.getTask(id).then((data) => setTask(data as PlannerTask));
    api.getRecommendedCandidates(id).then((data) => setCandidates(data as Candidate[]));
  }, [id]);

  if (!task) return <div className="panel">Loading task details...</div>;

  return (
    <div className="page-grid">
      <section className="panel span-2">
        <div className="panel-header">
          <div>
            <p className="eyebrow">Planner Details</p>
            <h2>{task.role}</h2>
            <p className="muted">{task.clientName} · {task.plannerNo}</p>
          </div>
          <span className={`pill pill-${task.priority.toLowerCase()}`}>{task.status}</span>
        </div>

        <div className="detail-grid">
          <Info label="Client" value={task.clientName} />
          <Info label="Contact" value={`${task.contactName} (${task.contactEmail})`} />
          <Info label="Budget" value={`${task.currency} ${task.budget.toLocaleString()}`} />
          <Info label="Open Positions" value={String(task.openPositions)} />
          <Info label="SLA" value={new Date(task.slaDate).toLocaleString()} />
          <Info label="Source" value={task.sourceType} />
        </div>

        <div className="stack-section">
          <h3>Requirement asked</h3>
          <p>{task.requirementAsked}</p>
        </div>

        <div className="stack-section">
          <h3>Skills</h3>
          <div className="chip-row">{task.skills.map((skill) => <span key={skill} className="chip">{skill}</span>)}</div>
        </div>

        <div className="stack-section">
          <h3>Gaps</h3>
          <ul className="clean-list">{task.gaps.map((gap) => <li key={gap}>{gap}</li>)}</ul>
        </div>
      </section>

      <section className="panel">
        <h3>Recommended candidates</h3>
        <div className="stack-list">
          {candidates.map((candidate) => (
            <div key={candidate.id} className="mini-card">
              <strong>{candidate.name}</strong>
              <p>{candidate.currentRole}</p>
              <small>{candidate.experienceYears} years · {candidate.noticePeriod}</small>
            </div>
          ))}
        </div>
      </section>

      <section className="panel span-2">
        <h3>History timeline</h3>
        <div className="timeline">
          {task.timeline.map((item, index) => (
            <div key={index} className="timeline-item">
              <div className="timeline-dot" />
              <div>
                <strong>{item.title}</strong>
                <p>{item.description}</p>
                <small>{new Date(item.happenedOn).toLocaleString()} · {item.performedBy}</small>
              </div>
            </div>
          ))}
        </div>
      </section>
    </div>
  );
}

function Info({ label, value }: { label: string; value: string }) {
  return (
    <div className="info-card">
      <small>{label}</small>
      <strong>{value}</strong>
    </div>
  );
}
