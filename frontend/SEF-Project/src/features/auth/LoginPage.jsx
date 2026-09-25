import { useState } from 'react';
import { Navigate, useNavigate } from 'react-router-dom';
import { ApiErrorAlert } from '../../components/ui/ApiErrorAlert';
import { PageShell } from '../../components/ui/PageShell';
import { useAuth } from '../../contexts/AuthContext';
import { normalizeApiError } from '../../utils/apiErrorUtils';
import './LoginPage.css';

export function LoginPage() {
  const { isAuthenticated, login } = useAuth();
  const navigate = useNavigate();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  if (isAuthenticated) {
    return <Navigate to="/orders" replace />;
  }

  async function handleSubmit(event) {
    event.preventDefault();

    setError(null);
    setIsSubmitting(true);

    try {
      await login(email, password);
      navigate('/products');
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
