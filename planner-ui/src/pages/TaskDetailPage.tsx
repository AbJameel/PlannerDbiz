import { useEffect, useMemo, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { api } from '../api/client';
import { getSession } from '../lib/auth';
import type {
  Candidate,
  PlannerTask,
  Vendor,
  VendorCandidateSubmission
} from '../types';
import { VendorMultiSelect } from '../components/VendorMultiSelect';

function toLocalInputValue(value: string) {
  if (!value) return '';
  const date = new Date(value);
  const offset = date.getTimezoneOffset();
  const local = new Date(date.getTime() - offset * 60000);
  return local.toISOString().slice(0, 16);
}

export function TaskDetailPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const session = getSession();
  const isVendor = session?.roleCode === 'VENDOR';

  const [task, setTask] = useState<PlannerTask | null>(null);
  const [recommendedCandidates, setRecommendedCandidates] = useState<Candidate[]>([]);
  const [recommendedVendors, setRecommendedVendors] = useState<Vendor[]>([]);
  const [allVendors, setAllVendors] = useState<Vendor[]>([]);
  const [vendorIds, setVendorIds] = useState<number[]>([]);
  const [vendorComment, setVendorComment] = useState('');
  const [submissions, setSubmissions] = useState<VendorCandidateSubmission[]>([]);
  const [activeTab, setActiveTab] = useState<'edit' | 'history'>('edit');
  const [requirementTab, setRequirementTab] = useState<'actual' | 'winnable' | 'gaps'>(
    'actual'
  );
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');

  const vendorOptions = useMemo(() => {
    const recommendedMap = new Map<number, Vendor>();
    recommendedVendors.forEach((v) => recommendedMap.set(v.id, v));

    const merged: Vendor[] = [];

    for (const vendor of recommendedVendors) {
      merged.push(vendor);
    }

    for (const vendor of allVendors) {
      if (!recommendedMap.has(vendor.id)) {
        merged.push(vendor);
      }
    }

    return merged;
  }, [recommendedVendors, allVendors]);

  async function loadTask() {
    if (!id) return;

    setLoading(true);
    setError('');

    try {
      const taskData = await api.getTask(id);
      const taskResult = taskData as PlannerTask;
      setTask(taskResult);
      setVendorIds(taskResult.assignedVendorIds ?? []);

      if (isVendor) {
        const vendorData = (await api.getVendorSubmissions(id)) as {
          vendorComment: string;
          items: VendorCandidateSubmission[];
        };
        setVendorComment(vendorData.vendorComment ?? '');
        setSubmissions(vendorData.items ?? []);
      } else {
        const [candidateData, vendorData, allVendorData, vendorSubmissionData] =
          await Promise.all([
            api.getRecommendedCandidates(id),
            api.getRecommendedVendors(id),
            api.getVendors(),
            api.getVendorSubmissions(id)
          ]);

        setRecommendedCandidates(candidateData as Candidate[]);
        setRecommendedVendors(vendorData as Vendor[]);
        setAllVendors(allVendorData as Vendor[]);

        const allSubs = vendorSubmissionData as {
          vendorComment: string;
          items: VendorCandidateSubmission[];
        };
        setSubmissions(allSubs.items ?? []);
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load task');
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    loadTask();
  }, [id]);

  function updateTask<K extends keyof PlannerTask>(key: K, value: PlannerTask[K]) {
    setTask((current) => (current ? { ...current, [key]: value } : current));
  }

  function updateSkillList(kind: 'skills' | 'secondarySkills', value: string) {
    const items = value
      .split(',')
      .map((x) => x.trim())
      .filter(Boolean);

    updateTask(kind, items as PlannerTask[typeof kind]);
  }

  function addSubmission() {
    setSubmissions((current) => [
      ...current,
      {
        submissionId: 0,
        plannerId: Number(id),
        vendorId: 0,
        candidateName: '',
        contactDetail: '',
        visaType: '',
        resumeFile: '',
        candidateStatus: 'Draft',
        isSubmitted: false,
        createdOn: new Date().toISOString()
      }
    ]);
  }

  function updateSubmission(index: number, patch: Partial<VendorCandidateSubmission>) {
    setSubmissions((current) =>
      current.map((item, i) => (i === index ? { ...item, ...patch } : item))
    );
  }

  function removeSubmission(index: number) {
    setSubmissions((current) => current.filter((_, i) => i !== index));
  }

  async function handleSave() {
    if (!task) return;

    setSaving(true);
    setError('');

    try {
      await api.updateTask(task.id, {
        clientName: task.clientName,
        requirementTitle: task.requirementTitle,
        role: task.role,
        category: task.category,
        budget: Number(task.budget || 0),
        budgetMax: task.budgetMax ? Number(task.budgetMax) : null,
        currency: task.currency,
        slaDate: task.slaDate,
        openPositions: Number(task.openPositions || 1),
        priority: task.priority,
        contactName: task.contactName,
        contactEmail: task.contactEmail,
        contactPhone: task.contactPhone,
        requirementAsked: task.requirementAsked,
        notes: task.notes,
        skills: task.skills,
        secondarySkills: task.secondarySkills,
        experienceRequired: task.experienceRequired,
        location: task.location,
        workMode: task.workMode,
        employmentType: task.employmentType,
        status: 'In Review',
        recruiterOverrideComment: task.recruiterOverrideComment
      });

      await loadTask();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to save task');
    } finally {
      setSaving(false);
    }
  }

  async function handleAssign() {
    if (!task) return;

    setSaving(true);
    setError('');

    try {
      if (vendorIds.length === 0) {
        throw new Error('Select at least one vendor.');
      }

      await handleSave();

      await api.assignVendors(task.id, {
        vendorIds,
        assignmentNote: 'Assigned after recruiter review.',
        updateStatusTo: 'Assigned to Vendor'
      });

      await loadTask();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to assign vendors');
    } finally {
      setSaving(false);
    }
  }

  async function handleVendorSaveDraft() {
    if (!id) return;

    setSaving(true);
    setError('');

    try {
      await api.saveVendorSubmissions(id, {
        vendorComment,
        items: submissions,
        submit: false
      });
      await loadTask();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to save draft');
    } finally {
      setSaving(false);
    }
  }

  async function handleVendorSubmit() {
    if (!id) return;

    setSaving(true);
    setError('');

    try {
      await api.saveVendorSubmissions(id, {
        vendorComment,
        items: submissions,
        submit: true
      });
      await loadTask();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to submit candidates');
    } finally {
      setSaving(false);
    }
  }

  if (loading) return <div className="panel">Loading task details...</div>;
  if (!task) return <div className="panel">Task not found.</div>;

  const vendorSubmitted = isVendor
    ? submissions.some((item) => item.isSubmitted)
    : task.status === 'Vendor Submitted';

  function renderSkillChips(items: string[], emptyText: string) {
    if (!items || items.length === 0) return <p className="muted">{emptyText}</p>;
    return (
      <div className="chip-row">
        {items.map((s) => (
          <span key={s} className="chip">
            {s}
          </span>
        ))}
      </div>
    );
  }

  return (
    <div className="page-grid">
      <section className="panel span-2 task-detail-page">
        <div className="panel-header">
          <div>
            <p className="eyebrow">Planner Task</p>
            <h2>{task.requirementTitle || task.role}</h2>
            <p className="muted">
              {task.plannerNo} · {task.sourceType}
            </p>
          </div>

          <div className="action-row">
            <button className="secondary-btn" onClick={() => navigate('/planners')}>
              Back
            </button>

            {!isVendor && (
              <>
                <button className="secondary-btn" onClick={handleSave} disabled={saving}>
                  Save
                </button>
                <button className="primary-btn" onClick={handleAssign} disabled={saving}>
                  Assign to Vendor
                </button>
              </>
            )}

            {isVendor && (
              <>
                <button
                  className="secondary-btn"
                  onClick={handleVendorSaveDraft}
                  disabled={saving || vendorSubmitted}
                >
                  Save Draft
                </button>
                <button
                  className="primary-btn"
                  onClick={handleVendorSubmit}
                  disabled={saving || vendorSubmitted}
                >
                  Submit
                </button>
              </>
            )}
          </div>
        </div>

        {error && <div className="error-box">{error}</div>}

        {!isVendor && (
          <div className="tab-strip">
            <button
              type="button"
              className={`tab-btn ${activeTab === 'edit' ? 'active' : ''}`}
              onClick={() => setActiveTab('edit')}
            >
              Edit
            </button>
            <button
              type="button"
              className={`tab-btn ${activeTab === 'history' ? 'active' : ''}`}
              onClick={() => setActiveTab('history')}
            >
              History
            </button>
          </div>
        )}

        {isVendor || activeTab === 'edit' ? (
          <div className="workspace-grid">
            <div className="editor-column">
              <section className="editor-section">
                <h3>Task Summary</h3>
                <div className="form-grid">
                  <label>
                    Client Name
                    <input
                      value={task.clientName}
                      disabled={isVendor}
                      onChange={(e) => updateTask('clientName', e.target.value)}
                    />
                  </label>

                  <label>
                    Requirement Title
                    <input
                      value={task.requirementTitle}
                      disabled={isVendor}
                      onChange={(e) => updateTask('requirementTitle', e.target.value)}
                    />
                  </label>

                  <label>
                    Role
                    <input
                      value={task.role}
                      disabled={isVendor}
                      onChange={(e) => updateTask('role', e.target.value)}
                    />
                  </label>

                  <label>
                    Status
                    <input value={task.status} disabled />
                  </label>

                  <label className="full-span">
                    Requirement Asked
                    <textarea
                      rows={4}
                      value={task.requirementAsked}
                      disabled={isVendor}
                      onChange={(e) => updateTask('requirementAsked', e.target.value)}
                    />
                  </label>

                  {!isVendor && (
                    <label className="full-span">
                      Notes
                      <textarea
                        rows={3}
                        value={task.notes}
                        onChange={(e) => updateTask('notes', e.target.value)}
                      />
                    </label>
                  )}

                  {isVendor && (
                    <label className="full-span">
                      Vendor Comments
                      <textarea
                        rows={4}
                        value={vendorComment}
                        disabled={vendorSubmitted}
                        onChange={(e) => setVendorComment(e.target.value)}
                      />
                    </label>
                  )}
                </div>
              </section>

              {!isVendor && (
                <>
                  <section className="editor-section">
                    <h3>Commercial & SLA</h3>
                    <div className="form-grid">
                      <label>
                        Budget
                        <input
                          type="number"
                          value={task.budget}
                          onChange={(e) => updateTask('budget', Number(e.target.value))}
                        />
                      </label>

                      <label>
                        Budget Max
                        <input
                          type="number"
                          value={task.budgetMax ?? ''}
                          onChange={(e) =>
                            updateTask('budgetMax', e.target.value ? Number(e.target.value) : null)
                          }
                        />
                      </label>

                      <label>
                        Currency
                        <input
                          value={task.currency}
                          onChange={(e) => updateTask('currency', e.target.value)}
                        />
                      </label>

                      <label>
                        SLA Date
                        <input
                          type="datetime-local"
                          value={toLocalInputValue(task.slaDate)}
                          onChange={(e) => updateTask('slaDate', e.target.value)}
                        />
                      </label>

                      <label>
                        No. of Positions
                        <input
                          type="number"
                          value={task.openPositions}
                          onChange={(e) => updateTask('openPositions', Number(e.target.value))}
                        />
                      </label>

                      <label>
                        Priority
                        <select
                          value={task.priority}
                          onChange={(e) =>
                            updateTask('priority', e.target.value as PlannerTask['priority'])
                          }
                        >
                          <option>High</option>
                          <option>Medium</option>
                          <option>Low</option>
                        </select>
                      </label>
                    </div>
                  </section>

                  <section className="editor-section">
                    <h3>Contact & Skills</h3>
                    <div className="form-grid">
                      <label>
                        Contact Name
                        <input
                          value={task.contactName}
                          onChange={(e) => updateTask('contactName', e.target.value)}
                        />
                      </label>

                      <label>
                        Contact Email
                        <input
                          value={task.contactEmail}
                          onChange={(e) => updateTask('contactEmail', e.target.value)}
                        />
                      </label>

                      <label>
                        Contact Phone
                        <input
                          value={task.contactPhone}
                          onChange={(e) => updateTask('contactPhone', e.target.value)}
                        />
                      </label>

                      <label>
                        Experience Required
                        <input
                          value={task.experienceRequired}
                          onChange={(e) => updateTask('experienceRequired', e.target.value)}
                        />
                      </label>

                      <label>
                        Location
                        <input
                          value={task.location}
                          onChange={(e) => updateTask('location', e.target.value)}
                        />
                      </label>

                      <label>
                        Work Mode
                        <input
                          value={task.workMode}
                          onChange={(e) => updateTask('workMode', e.target.value)}
                        />
                      </label>

                      <label>
                        Employment Type
                        <input
                          value={task.employmentType}
                          onChange={(e) => updateTask('employmentType', e.target.value)}
                        />
                      </label>

                      <label className="full-span">
                        Primary Skills (comma separated)
                        <input
                          value={task.skills.join(', ')}
                          onChange={(e) => updateSkillList('skills', e.target.value)}
                        />
                      </label>

                      <label className="full-span">
                        Secondary Skills (comma separated)
                        <input
                          value={task.secondarySkills.join(', ')}
                          onChange={(e) => updateSkillList('secondarySkills', e.target.value)}
                        />
                      </label>

                      <label className="full-span">
                        Recruiter Override Comment
                        <textarea
                          rows={3}
                          value={task.recruiterOverrideComment}
                          onChange={(e) =>
                            updateTask('recruiterOverrideComment', e.target.value)
                          }
                        />
                      </label>
                    </div>
                  </section>
                </>
              )}

              {isVendor && (
                <section className="editor-section">
                  <h3>Candidate Submission</h3>
                  <div className="stack-list">
                    {submissions.map((item, index) => (
                      <div key={index} className="mini-card">
                        <div className="form-grid">
                          <label>
                            Candidate Name
                            <input
                              disabled={vendorSubmitted || item.isSubmitted}
                              value={item.candidateName}
                              onChange={(e) =>
                                updateSubmission(index, { candidateName: e.target.value })
                              }
                            />
                          </label>

                          <label>
                            Contact Detail
                            <input
                              disabled={vendorSubmitted || item.isSubmitted}
                              value={item.contactDetail}
                              onChange={(e) =>
                                updateSubmission(index, { contactDetail: e.target.value })
                              }
                            />
                          </label>

                          <label>
                            Visa Type
                            <input
                              disabled={vendorSubmitted || item.isSubmitted}
                              value={item.visaType}
                              onChange={(e) =>
                                updateSubmission(index, { visaType: e.target.value })
                              }
                            />
                          </label>

                          <label>
                            Upload Resume
                            <input
                              type="file"
                              disabled={vendorSubmitted || item.isSubmitted}
                              onChange={(e) =>
                                updateSubmission(index, {
                                  resumeFile: e.target.files?.[0]?.name ?? item.resumeFile
                                })
                              }
                            />
                            <small className="muted">{item.resumeFile}</small>
                          </label>
                        </div>

                        {!vendorSubmitted && !item.isSubmitted && (
                          <div className="mini-actions">
                            <button
                              type="button"
                              className="secondary-btn"
                              onClick={() => removeSubmission(index)}
                            >
                              Remove
                            </button>
                          </div>
                        )}
                      </div>
                    ))}

                    {!vendorSubmitted && (
                      <button
                        type="button"
                        className="secondary-btn"
                        onClick={addSubmission}
                      >
                        + Add Candidate
                      </button>
                    )}
                  </div>
                </section>
              )}
            </div>

            <div className="side-column">
              <section className="editor-section">
                <h3>Requirement Analysis</h3>
                <p className="muted">Requirement Classification: {task.category || '-'}</p>

                <div className="tab-strip">
                  <button
                    type="button"
                    className={`tab-btn ${requirementTab === 'actual' ? 'active' : ''}`}
                    onClick={() => setRequirementTab('actual')}
                  >
                    Actual
                  </button>
                  <button
                    type="button"
                    className={`tab-btn ${requirementTab === 'winnable' ? 'active' : ''}`}
                    onClick={() => setRequirementTab('winnable')}
                  >
                    Winnable
                  </button>
                  <button
                    type="button"
                    className={`tab-btn ${requirementTab === 'gaps' ? 'active' : ''}`}
                    onClick={() => setRequirementTab('gaps')}
                  >
                    Gaps
                  </button>
                </div>

                {requirementTab === 'actual' && (
                  <div className="stack-list">
                    <div>
                      <p className="muted">Actual requirement from JD</p>
                      <div className="info-card">
                        <small>{task.requirementAsked || 'No requirement text available.'}</small>
                      </div>
                    </div>
                    <div>
                      <p className="muted">Requirement Skills (Must-have)</p>
                      {renderSkillChips(task.skills, 'No must-have skills captured yet.')}
                    </div>
                  </div>
                )}

                {requirementTab === 'winnable' && (
                  <div className="stack-list">
                    <p className="muted">Winnable Skills (Good-to-have)</p>
                    {renderSkillChips(task.secondarySkills, 'No winnable skills captured yet.')}
                  </div>
                )}

                {requirementTab === 'gaps' && (
                  <div className="stack-list">
                    <p className="muted">Gap Skills (Missing areas)</p>
                    {renderSkillChips(task.gaps, 'No gaps identified.')}
                  </div>
                )}
              </section>

              {!isVendor && (
                <>
                  <section className="editor-section">
                    <h3>Recommended Candidates</h3>
                    <div className="stack-list">
                      {recommendedCandidates.length === 0 && (
                        <p className="muted">No candidate recommendations yet.</p>
                      )}

                      {recommendedCandidates.map((candidate) => (
                        <div key={candidate.id} className="mini-card">
                          <strong>{candidate.name}</strong>
                          <p>{candidate.currentRole}</p>
                          <small>
                            {candidate.experienceYears} years · {candidate.noticePeriod}
                          </small>
                          <small>Expected: SGD {candidate.expectedBudget}</small>
                        </div>
                      ))}
                    </div>
                  </section>

                  <section className="editor-section">
                    <h3>Assign Vendors</h3>
                    <VendorMultiSelect
                      vendors={vendorOptions}
                      selectedVendorIds={vendorIds}
                      onChange={setVendorIds}
                    />
                  </section>
                </>
              )}

              <section className="editor-section">
                <h3>{isVendor ? 'My Candidate List' : 'Vendor Candidate List'}</h3>
                <div className="stack-list">
                  {submissions.length === 0 ? (
                    <p className="muted">No candidates added yet.</p>
                  ) : (
                    submissions.map((item, idx) => (
                      <div key={idx} className="mini-card">
                        <strong>{item.candidateName || 'Draft Candidate'}</strong>
                        <p>{item.contactDetail}</p>
                        <small>
                          {item.visaType} · {item.resumeFile || 'No resume uploaded'}
                        </small>
                        <small>Status: {item.candidateStatus}</small>
                      </div>
                    ))
                  )}
                </div>
              </section>
            </div>
          </div>
        ) : (
          <section className="editor-section history-tab-panel">
            <h3>Task History</h3>
            <div className="timeline">
              {task.timeline.length === 0 ? (
                <p className="muted">No history available.</p>
              ) : (
                task.timeline.map((item, index) => (
                  <div key={index} className="timeline-item">
                    <div className="timeline-dot" />
                    <div>
                      <strong>{item.title}</strong>
                      <p>{item.description}</p>
                      <small>
                        {new Date(item.happenedOn).toLocaleString()} · {item.performedBy}
                      </small>
                    </div>
                  </div>
                ))
              )}
            </div>
          </section>
        )}
      </section>
    </div>
  );
}
