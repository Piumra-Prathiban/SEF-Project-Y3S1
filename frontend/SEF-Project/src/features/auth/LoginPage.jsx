import { useState } from 'react';
import { Navigate, useLocation, useNavigate } from 'react-router-dom';
import { ApiErrorAlert } from '../../components/ui/ApiErrorAlert';
import { PageShell } from '../../components/ui/PageShell';
import { useAuth } from '../../contexts/AuthContext';
import { isStaff } from '../../utils/roles';
import { normalizeApiError } from '../../utils/apiErrorUtils';
import './LoginPage.css';

export function LoginPage() {
  const { isAuthenticated, login } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const returnTo = location.state?.from
    ? `${location.state.from.pathname}${location.state.from.search ?? ''}`
    : null;
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  if (isAuthenticated) {
    return <Navigate to={returnTo ?? '/orders'} replace />;
  }

  async function handleSubmit(event) {
    event.preventDefault();

    setError(null);
    setIsSubmitting(true);

    try {
      const response = await login(email, password);
      const landing = isStaff(response?.user) ? '/products' : '/orders';

      navigate(returnTo ?? landing, { replace: true });
    } catch (err) {
      setError(normalizeApiError(err));
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <main className="auth-page">
      <PageShell
        eyebrow="SE3090 Group Project"
        title="Sign in"
        description="Sign in to shop the Clothic collection and manage products, inventory and orders."
      >
        <form className="auth-form" onSubmit={handleSubmit}>
          <label>
            Email
            <input
              autoComplete="email"
              onChange={(event) => setEmail(event.target.value)}
              required
              type="email"
              value={email}
            />
          </label>

          <label>
            Password
            <input
              autoComplete="current-password"
              onChange={(event) => setPassword(event.target.value)}
              required
              type="password"
              value={password}
            />
          </label>

          <button disabled={isSubmitting} type="submit">
            {isSubmitting ? 'Signing in...' : 'Login'}
          </button>
        </form>

        <ApiErrorAlert message={error} />
      </PageShell>
    </main>
  );
}

export default LoginPage;
