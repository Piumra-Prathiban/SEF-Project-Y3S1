import { useState } from 'react';
import { Navigate, useLocation, useNavigate } from 'react-router-dom';
import FormField from '../../components/FormField';
import { Alert } from '../../components/StatusViews';
import { useAuth } from '../../contexts/AuthContext';
import { MANAGER_ROLES } from '../marketing/marketingConstants';

export default function LoginPage() {
  const { isAuthenticated, login } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [submitting, setSubmitting] = useState(false);

  const from = location.state?.from;
  const returnTo = from ? `${from.pathname}${from.search ?? ''}` : null;

  if (isAuthenticated && !submitting) {
    return <Navigate to={returnTo ?? '/'} replace />;
  }

  async function handleSubmit(event) {
    event.preventDefault();
    setError('');
    setSubmitting(true);

    try {
      const response = await login(email, password);
      const home = MANAGER_ROLES.includes(response.user?.role) ? '/marketing' : '/';
      navigate(returnTo ?? home, { replace: true });
    } catch (err) {
      setError(err.status === 401 ? 'Invalid email or password.' : err.message);
      setSubmitting(false);
    }
  }

  return (
    <section className="narrow">
      <h1>Log in</h1>
      <form className="form" onSubmit={handleSubmit}>
        <Alert tone="danger">{error}</Alert>

        <FormField id="email" label="Email" required>
          {(props) => (
            <input
              {...props}
              type="email"
              autoComplete="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
            />
          )}
        </FormField>

        <FormField id="password" label="Password" required>
          {(props) => (
            <input
              {...props}
              type="password"
              autoComplete="current-password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
            />
          )}
        </FormField>

        <button type="submit" className="button" disabled={submitting}>
          {submitting ? 'Logging in…' : 'Log in'}
        </button>
      </form>
    </section>
  );
}
