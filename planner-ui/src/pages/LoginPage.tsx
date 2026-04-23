import { useState } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { api } from '../api/client';
import { setSession } from '../lib/auth';

export function LoginPage() {
  const [email, setEmail] = useState('admin@dbiz.com');
  const [password, setPassword] = useState('Admin@123');
  const [error, setError] = useState('');
  const [saving, setSaving] = useState(false);
  const navigate = useNavigate();

  async function onSubmit(e: React.FormEvent) {
    e.preventDefault();
    setSaving(true);
    setError('');
    try {
      const result = await api.login({ email, password }) as any;
      setSession({ token: result.token, roleCode: result.roleCode, userId: result.userId, email });
      navigate('/');
    } catch (err: any) {
      const msg = err?.message || 'Login failed';
      if (msg.includes('First-time activation')) setError('First-time activation required. Use the activation link from your email.');
      else setError(msg);
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="auth-shell">
      <form className="auth-card" onSubmit={onSubmit}>
        <p className="eyebrow">DBiz Planner</p>
        <h2>Sign in</h2>
        <p className="muted">Use your email and password to access the planner.</p>
        <label>Email<input value={email} onChange={(e) => setEmail(e.target.value)} /></label>
        <label>Password<input type="password" value={password} onChange={(e) => setPassword(e.target.value)} /></label>
        {error && <div className="error-box">{error}</div>}
        <button className="primary-btn" disabled={saving}>{saving ? 'Signing in...' : 'Login'}</button>
        <small className="muted">Demo admin after running seed: admin@dbiz.com / Admin@123</small>
        <Link className="muted" to="/activate-account">Already have activation details?</Link>
      </form>
    </div>
  );
}
