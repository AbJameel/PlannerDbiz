type AppConfig = {
  apiBaseUrl?: string;
};

const DEFAULT_API_BASE_URL = 'https://localhost:44302/api';

let apiBaseUrlPromise: Promise<string> | undefined;

function normalizeBaseUrl(url: string): string {
  return url.replace(/\/+$/, '');
}

async function loadConfig(): Promise<AppConfig | null> {
  try {
    const response = await fetch('/config.json', { cache: 'no-store' });
    if (!response.ok) return null;
    const json = (await response.json()) as unknown;
    if (typeof json !== 'object' || json === null) return null;
    return json as AppConfig;
  } catch {
    return null;
  }
}

async function getApiBaseUrl(): Promise<string> {
  if (!apiBaseUrlPromise) {
    apiBaseUrlPromise = (async () => {
      const config = await loadConfig();
      const baseUrl = config?.apiBaseUrl ?? DEFAULT_API_BASE_URL;
      return normalizeBaseUrl(baseUrl);
    })();
  }
  return apiBaseUrlPromise;
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const baseUrl = await getApiBaseUrl();
  const response = await fetch(`${baseUrl}${path}`, init);
  if (!response.ok) {
    const text = await response.text();
    throw new Error(text || `API request failed: ${response.status}`);
  }
  if (response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
}

export const api = {
  getDashboardSummary: () => request('/dashboard/summary'),
  getTopTasks: () => request('/dashboard/tasks/top'),
  getTasks: () => request('/tasks'),
  getTask: (id: string | number) => request(`/tasks/${id}`),
  getRecommendedCandidates: (id: string | number) => request(`/tasks/${id}/recommended-candidates`),
  getRules: () => request('/rules'),
  createRule: (payload: unknown) => request('/rules', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload) }),
  getVendors: () => request('/vendors'),
  createVendor: (payload: unknown) => request('/vendors', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload) }),
  getCandidates: () => request('/candidates'),
  createCandidate: (payload: unknown) => request('/candidates', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload) }),
  getMailbox: () => request('/mailbox'),
  createTask: (payload: { subject: string; fromEmail: string; body: string }) =>
    request('/tasks', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload) }),
  uploadMail: (file: File, fromEmail: string) => {
    const formData = new FormData();
    formData.append('file', file);
    formData.append('fromEmail', fromEmail);
    return request('/tasks/upload-mail', { method: 'POST', body: formData });
  }
};
