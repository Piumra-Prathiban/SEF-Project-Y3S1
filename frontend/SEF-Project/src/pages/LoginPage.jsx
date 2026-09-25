import { useState } from 'react';
import { Navigate, useLocation, useNavigate } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth';
import { getApiErrorMessage } from '../services/api';

export default function LoginPage() {
  const { isAuthenticated, login } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [submitting, setSubmitting] = useState(false);

  if (isAuthenticated) return <Navigate to="/products" replace />;

  async function handleSubmit(event) {
    event.preventDefault();
    setSubmitting(true);
    setError('');
    try {
      await login(email.trim(), password);
      navigate(location.state?.from?.pathname || '/products', { replace: true });
    } catch (requestError) {
      setError(getApiErrorMessage(requestError));
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <section className="auth-page">
      <div className="auth-panel">
        <p className="kicker">Customer access</p>
        <h1>Welcome back</h1>
        <p>Log in to manage your wishlist, cart, profile, and delivery addresses.</p>
        <form className="stacked-form" onSubmit={handleSubmit}>
          <label>Email address<input type="email" value={email} onChange={(event) => setEmail(event.target.value)} autoComplete="email" required /></label>
          <label>Password<input type="password" value={password} onChange={(event) => setPassword(event.target.value)} autoComplete="current-password" minLength="8" required /></label>
          {error && <p className="form-error" role="alert">{error}</p>}
          <button type="submit" className="button button--primary" disabled={submitting}>{submitting ? 'Logging in…' : 'Log in'}</button>
        </form>
      </div>
    </section>
  );
}
