import { useSearchParams, useNavigate } from 'react-router-dom';
import { useState } from 'react';
import { api } from '../api/client';

export function ActivateAccountPage() {
  const [search] = useSearchParams();
  const navigate = useNavigate();
  const [email, setEmail] = useState(search.get('email') ?? '');
  const [token, setToken] = useState(search.get('token') ?? '');
  const [otpCode, setOtpCode] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');
  const [saving, setSaving] = useState(false);

  async function verifyOtp() {
    setSaving(true); setError(''); setMessage('');
    try {
      await api.verifyOtp({ email, activationToken: token, otpCode });
      setMessage('OTP verified. You can now set your password.');
    } catch (err: any) { setError(err?.message || 'OTP verification failed'); }
    finally { setSaving(false); }
  }

  async function setPassword(e: React.FormEvent) {
    e.preventDefault();
    setSaving(true); setError(''); setMessage('');
    try {
      await api.setInitialPassword({ email, activationToken: token, otpCode, newPassword, confirmPassword });
      setMessage('Password set successfully. Redirecting to login...');
      setTimeout(() => navigate('/login'), 1200);
    } catch (err: any) { setError(err?.message || 'Activation failed'); }
    finally { setSaving(false); }
  }

  return (
    <div className="auth-shell">
      <form className="auth-card" onSubmit={setPassword}>
        <p className="eyebrow">First-time activation</p>
        <h2>Activate account</h2>
        <label>Email<input value={email} onChange={(e) => setEmail(e.target.value)} /></label>
        <label>Activation Token<input value={token} onChange={(e) => setToken(e.target.value)} /></label>
        <label>OTP Code<input value={otpCode} onChange={(e) => setOtpCode(e.target.value)} /></label>
        <button type="button" className="secondary-btn" onClick={verifyOtp} disabled={saving}>Verify OTP</button>
        <label>New Password<input type="password" value={newPassword} onChange={(e) => setNewPassword(e.target.value)} /></label>
        <label>Confirm Password<input type="password" value={confirmPassword} onChange={(e) => setConfirmPassword(e.target.value)} /></label>
        {message && <div className="success-box">{message}</div>}
        {error && <div className="error-box">{error}</div>}
        <button className="primary-btn" disabled={saving}>{saving ? 'Saving...' : 'Set Password'}</button>
      </form>
    </div>
  );
}
