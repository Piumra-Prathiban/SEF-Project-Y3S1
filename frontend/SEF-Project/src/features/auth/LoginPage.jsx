import { useState } from 'react';
import { Alert } from '../../components/ui/Alert';
import { PageShell } from '../../components/ui/PageShell';
import { useAuth } from '../../contexts/AuthContext';
import { navigateTo } from '../../hooks/useLocation';

export function LoginPage() {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState(null);
  const { login } = useAuth();

  async function handleLogin(event) {
    event.preventDefault();
    setError(null);

    try {
      await login(email, password);
      navigateTo('/products');
    } catch (err) {
      setError({
        status: err.status,
        message: err.message,
      });
    }
  }

  return (
    <main className="auth-page">
      <PageShell
        eyebrow="SE3090 Group Project"
        title="Sign in"
        description="Use your existing project account to access Member 1 product and inventory tools."
      >
        <form className="auth-form" onSubmit={handleLogin}>
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

          <button type="submit">Login</button>
        </form>

        {error && (
          <Alert tone="danger">
            {error.status ? `${error.status}: ` : ''}
            {error.message}
          </Alert>
        )}
      </PageShell>
    </main>
  );
}
