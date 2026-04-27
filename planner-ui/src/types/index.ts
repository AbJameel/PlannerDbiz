
export type DashboardSummary = {
  newTasks: number;
  underReview: number;
  assignedToVendors: number;
  closingToday: number;
  assignedQueue?: number;
  pendingSubmission?: number;
  repliedToRecruiter?: number;
  slaToday?: number;
};

export type TimelineItem = {
  happenedOn: string;
  title: string;
  description: string;
  performedBy: string;
};

export type PlannerTask = {
  id: number;
  plannerNo: string;
  clientName: string;
  requirementTitle: string;
  role: string;
  category: string;
  priority: string;
  budget: number;
  budgetMax?: number | null;
  currency: string;
  receivedOn: string;
  slaDate: string;
  status: string;
  openPositions: number;
  sourceType: string;
  contactName: string;
  contactEmail: string;
  contactPhone: string;
  requirementAsked: string;
  notes: string;
  skills: string[];
  secondarySkills: string[];
  gaps: string[];
  experienceRequired: string;
  location: string;
  workMode: string;
  employmentType: string;
  recruiterOverrideComment: string;
  timeline: TimelineItem[];
  recommendedCandidateIds: number[];
  assignedVendorIds: number[];
};

export type Rule = {
  id: number;
  name: string;
  category: string;
  condition: string;
  outcome: string;
  isActive: boolean;
};

export type Vendor = {
  id: number;
  name: string;
  email: string;
  coverageRoles: string;
  budgetMin: number;
  budgetMax: number;
  status: string;
};

export type Candidate = {
  id: number;
  name: string;
  currentRole: string;
  expectedBudget: number;
  experienceYears: number;
  noticePeriod: string;
  resumeFile: string;
  skills: string[];
  location: string;
};

export type MailboxItem = {
  id: number;
  subject: string;
  fromEmail: string;
  receivedOn: string;
  snippet: string;
  isRead: boolean;
  sourceType: string;
};

export type VendorCandidateSubmission = {
  submissionId: number;
  plannerId: number;
  vendorId: number;
  candidateName: string;
  contactDetail: string;
  visaType: string;
  resumeFile: string;
  candidateStatus: string;
  isSubmitted: boolean;
  createdOn: string;
  updatedOn?: string | null;
};
