import { NavLink, Route, Routes, useNavigate } from 'react-router-dom';
import { DashboardPage } from './pages/DashboardPage';
import { TaskDetailPage } from './pages/TaskDetailPage';
import { RulesPage } from './pages/RulesPage';
import { VendorsPage } from './pages/VendorsPage';
import { CandidatesPage } from './pages/CandidatesPage';
import { MailboxPage } from './pages/MailboxPage';
import { LoginPage } from './pages/LoginPage';
import { ActivateAccountPage } from './pages/ActivateAccountPage';
import { UsersPage } from './pages/UsersPage';
import { ProtectedRoute } from './components/ProtectedRoute';
import { clearSession, getSession } from './lib/auth';

function Shell() {
  const session = getSession();
  const navigate = useNavigate();
  return (
    <div className="app-shell">
      <aside className="sidebar">
        <div className="brand-block">
          <div className="brand-badge">D</div>
          <div><h1>DBiz Planner</h1><p>React + .NET split app</p></div>
        </div>
        <nav className="nav-links">
          <NavLink to="/">Review Queue</NavLink>
          <NavLink to="/mailbox">Mailbox</NavLink>
          <NavLink to="/rules">Rules</NavLink>
          <NavLink to="/vendors">Vendors</NavLink>
          <NavLink to="/candidates">Candidates</NavLink>
          {session?.roleCode === 'SUPER_ADMIN' && <NavLink to="/admin/users">Users</NavLink>}
        </nav>
        <div className="session-block">
          <small>{session?.email}</small>
          <strong>{session?.roleCode?.replace('_', ' ')}</strong>
          <button className="secondary-btn" onClick={() => { clearSession(); navigate('/login'); }}>Logout</button>
        </div>
      </aside>
      <main className="content-shell">
        <Routes>
          <Route path="/" element={<DashboardPage />} />
          <Route path="/tasks/:id" element={<TaskDetailPage />} />
          <Route path="/mailbox" element={<MailboxPage />} />
          <Route path="/rules" element={<RulesPage />} />
          <Route path="/vendors" element={<VendorsPage />} />
          <Route path="/candidates" element={<CandidatesPage />} />
          <Route path="/admin/users" element={<ProtectedRoute roles={['SUPER_ADMIN']}><UsersPage /></ProtectedRoute>} />
        </Routes>
      </main>
    </div>
  );
}

export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/activate-account" element={<ActivateAccountPage />} />
      <Route path="/*" element={<ProtectedRoute><Shell /></ProtectedRoute>} />
    </Routes>
  );
}
