export type DashboardSummary = {
  newTasks: number;
  underReview: number;
  assignedToVendors: number;
  closingToday: number;
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
  role: string;
  priority: string;
  budget: number;
  currency: string;
  receivedOn: string;
  slaDate: string;
  status: string;
  openPositions: number;
  sourceType: string;
  contactName: string;
  contactEmail: string;
  requirementAsked: string;
  skills: string[];
  gaps: string[];
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
