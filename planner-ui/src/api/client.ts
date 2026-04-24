
import { getSession } from '../lib/auth';

type AppConfig = { apiBaseUrl?: string };
const DEFAULT_API_BASE_URL = 'https://localhost:44302/api';
let apiBaseUrlPromise: Promise<string> | undefined;
function normalizeBaseUrl(url: string): string { return url.replace(/\/+$/, ''); }
async function loadConfig(): Promise<AppConfig | null> {
  try {
    const response = await fetch('/config.json', { cache: 'no-store' });
    if (!response.ok) return null;
    return await response.json() as AppConfig;
  } catch { return null; }
}
async function getApiBaseUrl(): Promise<string> {
  if (!apiBaseUrlPromise) apiBaseUrlPromise = (async () => normalizeBaseUrl((await loadConfig())?.apiBaseUrl ?? DEFAULT_API_BASE_URL))();
  return apiBaseUrlPromise;
}
async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const baseUrl = await getApiBaseUrl();
  const headers = new Headers(init?.headers || {});
  const session = getSession();
  if (session?.token) headers.set('Authorization', `Bearer ${session.token}`);
  const response = await fetch(`${baseUrl}${path}`, { ...init, headers });
  if (!response.ok) {
    const text = await response.text();
    throw new Error(text || `API request failed: ${response.status}`);
  }
  if (response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
}

export const api = {
  getDashboardSummary: () => request('/dashboard/summary'),
  getReviewQueue: () => request('/tasks/review-queue'),
  getTask: (id: string | number) => request(`/tasks/${id}`),
  getPlannerList: (queryString = '') => request(`/tasks/list${queryString ? `?${queryString}` : ''}`),
  getRecommendedCandidates: (id: string | number) => request(`/tasks/${id}/recommended-candidates`),
  getRecommendedVendors: (id: string | number) => request(`/tasks/${id}/recommended-vendors`),
  getVendorSubmissions: (id: string | number) => request(`/tasks/${id}/vendor-submissions`),
  saveVendorSubmissions: (id: string | number, payload: unknown) => request(`/tasks/${id}/vendor-submissions`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload) }),
  updateTask: (id: string | number, payload: unknown) => request(`/tasks/${id}`, { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload) }),
  assignVendors: (id: string | number, payload: unknown) => request(`/tasks/${id}/assign-vendors`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload) }),
  getRules: () => request('/rules'),
  createRule: (payload: unknown) => request('/rules', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload) }),
  getVendors: () => request('/vendors'),
  createVendor: (payload: unknown) => request('/vendors', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload) }),
  getCandidates: () => request('/candidates'),
  createCandidate: (payload: unknown) => request('/candidates', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload) }),
  getMailbox: () => request('/mailbox'),
  uploadMail: (payload: { file?: File | null; fromEmail: string; emailContent?: string }) => { const formData = new FormData(); if (payload.file) formData.append('file', payload.file); formData.append('fromEmail', payload.fromEmail); if (payload.emailContent) formData.append('emailContent', payload.emailContent); return request('/tasks/upload-mail', { method: 'POST', body: formData }); },
  login: (payload: { email: string; password: string }) => request('/auth/login', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload) }),
  verifyOtp: (payload: { email: string; activationToken: string; otpCode: string }) => request('/auth/verify-otp', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload) }),
  setInitialPassword: (payload: { email: string; activationToken: string; otpCode: string; newPassword: string; confirmPassword: string }) => request('/auth/set-initial-password', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload) }),
  getUsers: () => request('/users'),
  getRoles: () => request('/users/roles'),
  createUser: (payload: unknown) => request('/users', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload) })
};
