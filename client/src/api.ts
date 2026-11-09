const API = '/api';

export interface AuthUser { userId: number; name: string; email: string; role: string; token: string; }
export interface Project { id: number; name: string; description: string; managerId: number; managerName: string; targetDate: string; cadence: string; totalSubtasks: number; doneSubtasks: number; progressPercent: number; health: string; }
export interface TaskItem { id: number; projectId: number; title: string; description: string; ownerId: number; ownerName: string; dueDate: string; totalSubtasks: number; doneSubtasks: number; progressPercent: number; }
export interface Subtask { id: number; taskId: number; title: string; ownerId: number; ownerName: string; dueDate: string; status: string; blockerNote: string; }
export interface ReportItem { id: number; projectId: number | null; title: string; cadence: string; periodStart: string; periodEnd: string; summary: string; completedText: string; inProgressText: string; blockedText: string; risksText: string; nextStepsText: string; status: string; createdAt: string; slidesJson: string; }
export interface Slide { title: string; bullets: string[]; }
export interface TeamUser { id: number; name: string; email: string; role: string; }

export function getAuth(): AuthUser | null {
  const raw = localStorage.getItem('pst-auth');
  return raw ? (JSON.parse(raw) as AuthUser) : null;
}
export function setAuth(u: AuthUser | null) {
  if (u) localStorage.setItem('pst-auth', JSON.stringify(u)); else localStorage.removeItem('pst-auth');
}

async function req<T>(path: string, init?: RequestInit): Promise<T> {
  const auth = getAuth();
  const res = await fetch(API + path, {
    ...init,
    headers: { 'Content-Type': 'application/json', ...(auth ? { Authorization: `Bearer ${auth.token}` } : {}), ...(init?.headers ?? {}) },
  });
  if (res.status === 401) { setAuth(null); throw new Error('Signed out — please sign in again.'); }
  if (res.status === 403) throw new Error('Not allowed for your role.');
  if (!res.ok) { const t = await res.text(); throw new Error(t || `Request failed (${res.status})`); }
  return res.json() as Promise<T>;
}

export async function login(email: string, password: string): Promise<AuthUser> {
  const data = await req<{ token: string; userId: number; name: string; email: string; role: string }>('/auth/login', {
    method: 'POST', body: JSON.stringify({ email, password }),
  });
  const user: AuthUser = { ...data, token: data.token };
  setAuth(user);
  return user;
}

export const api = {
  dashboard: () => req<{ totalProjects: number; totalTasks: number; totalSubtasks: number; doneSubtasks: number; progressPercent: number; projects: Project[]; needsAttention: { subtask: Subtask; taskTitle: string; projectName: string }[] }>('/dashboard'),
  projects: () => req<Project[]>('/projects'),
  project: (id: number) => req<{ project: Project; tasks: TaskItem[]; subtasks: Subtask[] }>(`/projects/${id}`),
  users: () => req<TeamUser[]>('/users'),
  myWork: () => req<{ subtask: Subtask; taskTitle: string; projectName: string }[]>('/mywork'),
  reports: () => req<ReportItem[]>('/reports'),
  generateReport: (projectId: number | null, cadence: string, periodEnd: string) =>
    req<ReportItem>('/reports/generate', { method: 'POST', body: JSON.stringify({ projectId, cadence, periodEnd }) }),
  reviewReport: (id: number) => req<ReportItem>(`/reports/${id}/review`, { method: 'PUT' }),
  updateSubtask: (id: number, body: { status?: string; blockerNote?: string }) =>
    req<Subtask>(`/subtasks/${id}`, { method: 'PUT', body: JSON.stringify(body) }),
  createTask: (projectId: number, body: { title: string; description: string; ownerId: number; dueDate: string }) =>
    req<{ id: number; title: string }>(`/projects/${projectId}/tasks`, { method: 'POST', body: JSON.stringify(body) }),
  createSubtask: (taskId: number, body: { title: string; ownerId: number; dueDate: string; status: string; blockerNote: string }) =>
    req<{ id: number; title: string }>(`/tasks/${taskId}/subtasks`, { method: 'POST', body: JSON.stringify(body) }),
};

export function downloadUrl(reportId: number, format: string) {
  const auth = getAuth();
  // Downloads need the JWT; the browser cannot set an Authorization header on a plain link,
  // so fetch the blob with the token and trigger the download in code.
  return { reportId, format, token: auth?.token ?? '' };
}

export async function downloadReport(reportId: number, format: string) {
  const auth = getAuth();
  const res = await fetch(`${API}/reports/${reportId}/download?format=${format}`, {
    headers: auth ? { Authorization: `Bearer ${auth.token}` } : {},
  });
  if (!res.ok) throw new Error(`Download failed (${res.status})`);
  const blob = await res.blob();
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = `report-${reportId}.${format === 'ppt' ? 'pptx' : format}`;
  a.click();
  URL.revokeObjectURL(url);
}

export const VIEW_ONLY_ROLES = ['President', 'EVP', 'VP', 'Director'];
export const isViewOnly = (role: string) => VIEW_ONLY_ROLES.includes(role);
export const canEditWork = (role: string) => ['Manager', 'ScrumMaster', 'Developer'].includes(role);
