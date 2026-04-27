
import { useEffect, useMemo, useState } from 'react';
import { NavLink, Route, Routes, useNavigate } from 'react-router-dom';
import { DashboardPage } from './pages/DashboardPage';
import { TaskDetailPage } from './pages/TaskDetailPage';
import { VendorsPage } from './pages/VendorsPage';
import { CandidatesPage } from './pages/CandidatesPage';
import { MailboxPage } from './pages/MailboxPage';
import { LoginPage } from './pages/LoginPage';
import { ActivateAccountPage } from './pages/ActivateAccountPage';
import { UsersPage } from './pages/UsersPage';
import { PlannerListPage } from './pages/PlannerListPage';
import { ContactsPage } from './pages/ContactsPage';
import { ProtectedRoute } from './components/ProtectedRoute';
import { clearSession, getSession } from './lib/auth';

type ThemeMode = 'light' | 'dark';

function Shell() {
  const session = getSession();
  const navigate = useNavigate();
  const isVendor = session?.roleCode === 'VENDOR';
  const [menuOpen, setMenuOpen] = useState(false);
  const [theme, setTheme] = useState<ThemeMode>(() => {
    const stored = localStorage.getItem('planner_theme');
    return stored === 'dark' ? 'dark' : 'light';
  });

  useEffect(() => {
    document.documentElement.classList.toggle('theme-dark', theme === 'dark');
    localStorage.setItem('planner_theme', theme);
  }, [theme]);

  const themeLabel = useMemo(() => (theme === 'dark' ? 'Dark' : 'Light'), [theme]);

  function handleLogout() {
    clearSession();
    navigate('/login');
  }

  function renderThemeToggle() {
    return (
      <div className="theme-toggle-row">
        <span className="muted">{themeLabel}</span>
        <label className="theme-switch">
          <input
            type="checkbox"
            checked={theme === 'dark'}
            onChange={(e) => setTheme(e.target.checked ? 'dark' : 'light')}
            aria-label="Toggle dark mode"
          />
          <span className="theme-switch-track" />
        </label>
      </div>
    );
  }

  return (
    <div className={`app-shell ${menuOpen ? 'menu-open' : ''}`}>
      <header className="mobile-topbar">
        <button type="button" className="icon-btn" onClick={() => setMenuOpen(true)}>
          ☰
        </button>
        <div className="brand-block">
          <div className="brand-badge">D</div>
          <div>
            <h1>DBiz Planner</h1>
            {renderThemeToggle()}
          </div>
        </div>
      </header>

      <div
        className={`sidebar-backdrop ${menuOpen ? 'open' : ''}`}
        onClick={() => setMenuOpen(false)}
      />
      <aside className={`sidebar ${menuOpen ? 'open' : ''}`}>
        <div className="sidebar-toprow">
          <div className="brand-block">
            <div className="brand-badge">D</div>
            <div>
              <h1>DBiz Planner</h1>
              <p>React + .NET split app</p>
              {renderThemeToggle()}
            </div>
          </div>
          <button type="button" className="icon-btn sidebar-close-btn" onClick={() => setMenuOpen(false)}>
            ✕
          </button>
        </div>
        <nav className="nav-links">
          <NavLink to="/" onClick={() => setMenuOpen(false)}>
            Dashboard
          </NavLink>
          <NavLink to="/planners" onClick={() => setMenuOpen(false)}>
            Task List
          </NavLink>
          <NavLink to="/candidates" onClick={() => setMenuOpen(false)}>
            Candidates
          </NavLink>
          {!isVendor && (
            <NavLink to="/contacts" onClick={() => setMenuOpen(false)}>
              Contacts
            </NavLink>
          )}
          {!isVendor && (
            <>
              <NavLink to="/mailbox" onClick={() => setMenuOpen(false)}>
                Mailbox
              </NavLink>
              <NavLink to="/vendors" onClick={() => setMenuOpen(false)}>
                Vendors
              </NavLink>
            </>
          )}
          {session?.roleCode === 'SUPER_ADMIN' && (
            <NavLink to="/admin/users" onClick={() => setMenuOpen(false)}>
              Users
            </NavLink>
          )}
        </nav>
        <div className="session-block">
          <small>{session?.email}</small>
          <strong>{session?.roleCode?.replace('_', ' ')}</strong>
          <button className="secondary-btn" onClick={handleLogout}>
            Logout
          </button>
        </div>
      </aside>
      <main className="content-shell">
        <Routes>
          <Route path="/" element={<DashboardPage />} />
          <Route path="/planners" element={<PlannerListPage />} />
          <Route path="/tasks/:id" element={<TaskDetailPage />} />
          <Route path="/candidates" element={<CandidatesPage />} />
          {!isVendor && <Route path="/contacts" element={<ContactsPage />} />}
          {!isVendor && <><Route path="/mailbox" element={<MailboxPage />} /><Route path="/vendors" element={<VendorsPage />} /></>}
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
