import { NavLink, Route, Routes } from 'react-router-dom';
import { DashboardPage } from './pages/DashboardPage';
import { TaskDetailPage } from './pages/TaskDetailPage';
import { RulesPage } from './pages/RulesPage';
import { VendorsPage } from './pages/VendorsPage';
import { CandidatesPage } from './pages/CandidatesPage';
import { MailboxPage } from './pages/MailboxPage';

export default function App() {
  return (
    <div className="app-shell">
      <aside className="sidebar">
        <div className="brand-block">
          <div className="brand-badge">D</div>
          <div>
            <h1>DBiz Planner</h1>
            <p>React + .NET split app</p>
          </div>
        </div>

        <nav className="nav-links">
          <NavLink to="/">Dashboard</NavLink>
          <NavLink to="/mailbox">Mailbox</NavLink>
          <NavLink to="/rules">Rules</NavLink>
          <NavLink to="/vendors">Vendors</NavLink>
          <NavLink to="/candidates">Candidates</NavLink>
        </nav>
      </aside>

      <main className="content-shell">
        <Routes>
          <Route path="/" element={<DashboardPage />} />
          <Route path="/tasks/:id" element={<TaskDetailPage />} />
          <Route path="/mailbox" element={<MailboxPage />} />
          <Route path="/rules" element={<RulesPage />} />
          <Route path="/vendors" element={<VendorsPage />} />
          <Route path="/candidates" element={<CandidatesPage />} />
        </Routes>
      </main>
    </div>
  );
}
