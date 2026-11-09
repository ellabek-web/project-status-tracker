import { useCallback, useEffect, useState } from 'react';
import type { AuthUser, Project, ReportItem, Slide, Subtask, TaskItem, TeamUser } from './api';
import { api, downloadReport, getAuth, isViewOnly, login, setAuth } from './api';
import './App.css';

type Tab = 'dashboard' | 'projects' | 'mywork' | 'team' | 'reports';

const STATUSES = ['NotStarted', 'InProgress', 'Done', 'Blocked'];
const ROLES: { name: string; desc: string }[] = [
  { name: 'President', desc: 'Executive portfolio view — view-only.' },
  { name: 'EVP', desc: 'Portfolio health — view-only.' },
  { name: 'VP', desc: 'Health and delivery across your area — view-only.' },
  { name: 'Director', desc: 'Sees everything — view-only, does not edit tasks.' },
  { name: 'Manager', desc: 'Owns projects, approves and reviews reports, edits tasks and subtasks in their projects.' },
  { name: 'ScrumMaster', desc: 'Updates sprints and tasks, moves tasks, flags and chases blockers.' },
  { name: 'Developer', desc: 'Updates the status of their own subtasks.' },
];

function ProgressBar({ pct }: { pct: number }) {
  return <div className="bar"><div className="bar-fill" style={{ width: `${pct}%` }} /></div>;
}

export default function App() {
  const [user, setUser] = useState<AuthUser | null>(() => getAuth());
  const [tab, setTab] = useState<Tab>('dashboard');
  const [error, setError] = useState('');
  const [email, setEmail] = useState('manager@demo.local');
  const [password, setPassword] = useState('ChangeMe123!');

  const [dashboard, setDashboard] = useState<Awaited<ReturnType<typeof api.dashboard>> | null>(null);
  const [projects, setProjects] = useState<Project[]>([]);
  const [selectedProject, setSelectedProject] = useState<{ project: Project; tasks: TaskItem[]; subtasks: Subtask[] } | null>(null);
  const [myWork, setMyWork] = useState<Awaited<ReturnType<typeof api.myWork>>>([]);
  const [team, setTeam] = useState<TeamUser[]>([]);
  const [reports, setReports] = useState<ReportItem[]>([]);
  const [selectedReport, setSelectedReport] = useState<ReportItem | null>(null);
  const [formatByReport, setFormatByReport] = useState<Record<number, string>>({});
  const [genProject, setGenProject] = useState('');
  const [genCadence, setGenCadence] = useState('Weekly');
  const [genPeriodEnd, setGenPeriodEnd] = useState('2026-11-07');
  const [busy, setBusy] = useState(false);

  const loadAll = useCallback(async () => {
    setError('');
    try {
      const [d, p, w, t, r] = await Promise.all([api.dashboard(), api.projects(), api.myWork(), api.users(), api.reports()]);
      setDashboard(d); setProjects(p); setMyWork(w); setTeam(t); setReports(r);
    } catch (e) { setError(e instanceof Error ? e.message : String(e)); }
  }, []);

  useEffect(() => { if (user) void loadAll(); }, [user, loadAll]);

  async function doLogin(e: React.FormEvent) {
    e.preventDefault(); setError('');
    try { setUser(await login(email, password)); } catch (err) { setError(err instanceof Error ? err.message : String(err)); }
  }

  async function refreshProject() {
    if (!selectedProject) return;
    setSelectedProject(await api.project(selectedProject.project.id));
    await loadAll();
  }

  async function changeStatus(s: Subtask, status: string) {
    setError('');
    try { await api.updateSubtask(s.id, { status }); await refreshProject(); } catch (e) { setError(e instanceof Error ? e.message : String(e)); }
  }

  async function generateReport(e: React.FormEvent) {
    e.preventDefault(); setBusy(true); setError('');
    try {
      const r = await api.generateReport(genProject ? Number(genProject) : null, genCadence, genPeriodEnd);
      setReports((prev) => [r, ...prev]); setSelectedReport(r);
    } catch (err) { setError(err instanceof Error ? err.message : String(err)); } finally { setBusy(false); }
  }

  if (!user) {
    return (
      <div className="login-wrap">
        <div className="login-card">
          <h1>Project Status Tracker</h1>
          <p>Real progress from subtasks — not estimates. Sign in with your demo account.</p>
          <form onSubmit={doLogin}>
            <label>Email<input value={email} onChange={(e) => setEmail(e.target.value)} type="email" required /></label>
            <label>Password<input value={password} onChange={(e) => setPassword(e.target.value)} type="password" required /></label>
            <button type="submit">Sign in</button>
          </form>
          {error && <p className="error">{error}</p>}
          <p className="hint">Demo logins (password <code>ChangeMe123!</code> for all — change before real use):<br />
            manager@demo.local · scrum@demo.local · dev@demo.local · president@demo.local · evp@demo.local · vp@demo.local · director@demo.local</p>
        </div>
      </div>
    );
  }

  const viewOnly = isViewOnly(user.role);
  const slides: Slide[] = selectedReport ? (JSON.parse(selectedReport.slidesJson) as Slide[]) : [];

  return (
    <div className="app">
      <header>
        <div><h1>Project Status Tracker</h1><span className="sub">Signed in as {user.name} — {user.role}{viewOnly ? ' (view-only)' : ''}</span></div>
        <button className="ghost" onClick={() => { setAuth(null); setUser(null); }}>Sign out</button>
      </header>
      <nav>
        {(['dashboard', 'projects', 'mywork', 'team', 'reports'] as Tab[]).map((t) => (
          <button key={t} className={tab === t ? 'active' : ''} onClick={() => setTab(t)}>
            {t === 'dashboard' ? 'Dashboard' : t === 'projects' ? 'Projects' : t === 'mywork' ? 'My Work' : t === 'team' ? 'Team & Roles' : 'Report Centre'}
          </button>
        ))}
      </nav>
      {error && <p className="error">{error}</p>}

      {tab === 'dashboard' && dashboard && (
        <section>
          <h2>Dashboard</h2>
          <div className="cards">
            <div className="card"><strong>{dashboard.progressPercent}%</strong><span>overall progress ({dashboard.doneSubtasks}/{dashboard.totalSubtasks} subtasks done)</span><ProgressBar pct={dashboard.progressPercent} /></div>
            <div className="card"><strong>{dashboard.totalProjects}</strong><span>projects · {dashboard.totalTasks} tasks</span></div>
            <div className="card"><strong>{dashboard.needsAttention.length}</strong><span>items need attention (blocked or overdue)</span></div>
          </div>
          <h3>Projects</h3>
          <table><thead><tr><th>Project</th><th>Manager</th><th>Progress</th><th>Health</th></tr></thead>
            <tbody>{dashboard.projects.map((p) => <tr key={p.id} onClick={() => { setSelectedProject(null); void api.project(p.id).then(setSelectedProject); setTab('projects'); }}><td>{p.name}</td><td>{p.managerName}</td><td>{p.progressPercent}% ({p.doneSubtasks}/{p.totalSubtasks})<ProgressBar pct={p.progressPercent} /></td><td>{p.health}</td></tr>)}</tbody></table>
          <h3>Needs attention</h3>
          {dashboard.needsAttention.length === 0 ? <p>Nothing blocked or overdue.</p> : (
            <ul>{dashboard.needsAttention.map((n) => <li key={n.subtask.id}><strong>{n.subtask.title}</strong> — {n.taskTitle} ({n.projectName}) · {n.subtask.ownerName} · due {n.subtask.dueDate} · {n.subtask.status}{n.subtask.blockerNote ? ` — ${n.subtask.blockerNote}` : ''}</li>)}</ul>
          )}
        </section>
      )}

      {tab === 'projects' && (
        <section>
          <h2>Projects</h2>
          <div className="list">
            {projects.map((p) => <button key={p.id} className={selectedProject?.project.id === p.id ? 'active' : ''} onClick={() => void api.project(p.id).then(setSelectedProject)}>{p.name} — {p.progressPercent}%</button>)}
          </div>
          {selectedProject && (
            <div>
              <h3>{selectedProject.project.name}</h3>
              <p>{selectedProject.project.description} · Manager: {selectedProject.project.managerName} · Target: {selectedProject.project.targetDate} · {selectedProject.project.cadence} · Health: {selectedProject.project.health}</p>
              {selectedProject.tasks.map((t) => (
                <div key={t.id} className="task">
                  <h4>{t.title} — {t.progressPercent}% ({t.doneSubtasks}/{t.totalSubtasks})</h4>
                  <ProgressBar pct={t.progressPercent} />
                  <ul>{selectedProject.subtasks.filter((s) => s.taskId === t.id).map((s) => (
                    <li key={s.id}>
                      <strong>{s.title}</strong> · {s.ownerName} · due {s.dueDate} · {s.status}
                      {s.blockerNote && <span> — {s.blockerNote}</span>}
                      {!viewOnly && (user.role !== 'Developer' || s.ownerId === user.userId) && (
                        <select value={s.status} onChange={(e) => void changeStatus(s, e.target.value)}>
                          {STATUSES.map((st) => <option key={st} value={st}>{st}</option>)}
                        </select>
                      )}
                      {viewOnly && <span className="tag">view-only</span>}
                    </li>
                  ))}</ul>
                </div>
              ))}
            </div>
          )}
        </section>
      )}

      {tab === 'mywork' && (
        <section>
          <h2>My Work — {user.name}</h2>
          {myWork.length === 0 ? <p>No subtasks assigned to you.</p> : (
            <ul>{myWork.map((w) => (
              <li key={w.subtask.id}><strong>{w.subtask.title}</strong> — {w.taskTitle} ({w.projectName}) · due {w.subtask.dueDate} · {w.subtask.status}
                <select value={w.subtask.status} onChange={async (e) => { await api.updateSubtask(w.subtask.id, { status: e.target.value }); await loadAll(); }}>
                  {STATUSES.map((st) => <option key={st} value={st}>{st}</option>)}
                </select>
              </li>
            ))}</ul>
          )}
        </section>
      )}

      {tab === 'team' && (
        <section>
          <h2>Team &amp; Roles</h2>
          <table><thead><tr><th>Name</th><th>Email</th><th>Role</th><th>Access</th></tr></thead>
            <tbody>{team.map((m) => <tr key={m.id}><td>{m.name}</td><td>{m.email}</td><td>{m.role}</td><td>{isViewOnly(m.role) ? 'View-only' : 'Can edit'}</td></tr>)}</tbody></table>
          <h3>What each role can do</h3>
          <ul>{ROLES.map((r) => <li key={r.name}><strong>{r.name}:</strong> {r.desc}</li>)}</ul>
        </section>
      )}

      {tab === 'reports' && (
        <section>
          <h2>Report Centre</h2>
          <form className="inline" onSubmit={generateReport}>
            <label>Project <select value={genProject} onChange={(e) => setGenProject(e.target.value)}><option value="">All projects</option>{projects.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}</select></label>
            <label>Cadence <select value={genCadence} onChange={(e) => setGenCadence(e.target.value)}><option>Weekly</option><option>Fortnightly</option></select></label>
            <label>Period end <input type="date" value={genPeriodEnd} onChange={(e) => setGenPeriodEnd(e.target.value)} required /></label>
            <button type="submit" disabled={busy || viewOnly}>{busy ? 'Generating…' : 'Generate draft report'}</button>
            {viewOnly && <span className="tag">view-only roles cannot generate reports</span>}
          </form>
          <table><thead><tr><th>Report</th><th>Period</th><th>Status</th><th>Format</th><th>Download</th><th></th></tr></thead>
            <tbody>{reports.map((r) => (
              <tr key={r.id} onClick={() => setSelectedReport(r)}>
                <td>{r.title}</td><td>{r.periodStart} to {r.periodEnd}</td><td>{r.status}</td>
                <td onClick={(e) => e.stopPropagation()}>
                  <select value={formatByReport[r.id] ?? 'xlsx'} onChange={(e) => setFormatByReport({ ...formatByReport, [r.id]: e.target.value })}>
                    <option value="pdf">PDF</option><option value="ppt">PPT</option><option value="xlsx">Excel</option>
                  </select>
                </td>
                <td onClick={(e) => e.stopPropagation()}>
                  <button onClick={async () => { try { await downloadReport(r.id, formatByReport[r.id] ?? 'xlsx'); } catch (e) { setError(e instanceof Error ? e.message : String(e)); } }}>Download</button>
                </td>
                <td onClick={(e) => e.stopPropagation()}>
                  {user.role === 'Manager' && r.status === 'Draft' && <button onClick={async () => { const u = await api.reviewReport(r.id); setReports((prev) => prev.map((x) => (x.id === u.id ? u : x))); setSelectedReport(u); }}>Mark reviewed</button>}
                </td>
              </tr>
            ))}</tbody></table>
          {selectedReport && (
            <div className="report-preview">
              <h3>{selectedReport.title}</h3>
              <p>{selectedReport.summary}</p>
              <p><strong>Completed:</strong> {selectedReport.completedText || '—'}</p>
              <p><strong>In progress:</strong> {selectedReport.inProgressText || '—'}</p>
              <p><strong>Blocked:</strong> {selectedReport.blockedText || '—'}</p>
              <p><strong>Risks:</strong> {selectedReport.risksText}</p>
              <p><strong>Next steps:</strong> {selectedReport.nextStepsText}</p>
              <h4>Slide deck preview (gold / purple / black)</h4>
              <div className="slides">
                {slides.map((s, i) => (
                  <div key={i} className={i === 0 ? 'slide cover' : 'slide'}>
                    <h5>{s.title}</h5>
                    <ul>{s.bullets.map((b, j) => <li key={j}>{b}</li>)}</ul>
                  </div>
                ))}
              </div>
            </div>
          )}
        </section>
      )}
    </div>
  );
}
