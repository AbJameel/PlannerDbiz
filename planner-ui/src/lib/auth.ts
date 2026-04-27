export type SessionUser = {
  userId: number;
  roleCode: string;
  token: string;
  email?: string;
  vendorId?: number | null;
};

const STORAGE_KEY = 'planner_session';

export function getSession(): SessionUser | null {
  const raw = localStorage.getItem(STORAGE_KEY);
  if (!raw) return null;
  try { return JSON.parse(raw) as SessionUser; } catch { return null; }
}

export function setSession(session: SessionUser) {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(session));
}

export function clearSession() {
  localStorage.removeItem(STORAGE_KEY);
}
