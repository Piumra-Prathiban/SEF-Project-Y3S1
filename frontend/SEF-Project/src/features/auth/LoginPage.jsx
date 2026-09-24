import { useState } from 'react';
import { Navigate } from 'react-router-dom';
import { useAuth } from '../../contexts/AuthContext';
import ErrorAlert from '../../components/ErrorAlert';
import './LoginPage.css';

function LoginPage() {
  const { isAuthenticated, login } = useAuth();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState(null);
  const [submitting, setSubmitting] = useState(false);

  if (isAuthenticated) {
    return <Navigate to="/orders" replace />;
  }

  async function handleSubmit(event) {
    event.preventDefault();

    setError(null);
    setSubmitting(true);

    try {
      await login(email, password);
    } catch (err) {
      setError({
        status: err.status,
        message: err.message,
        data: err.data,
      });
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="login-page">
      <h1>Login</h1>

      <form onSubmit={handleSubmit}>
        <label>
          Email
          <input
            type="email"
            value={email}
            onChange={(event) => setEmail(event.target.value)}
            required
            autoComplete="email"
          />
        </label>

        <label>
          Password
          <input
            type="password"
            value={password}
            onChange={(event) => setPassword(event.target.value)}
            required
            autoComplete="current-password"
          />
        </label>

        <button type="submit" disabled={submitting}>
          {submitting ? 'Logging in…' : 'Login'}
        </button>
      </form>

      {error && <ErrorAlert error={error} />}
    </div>
  );
}

export default LoginPage;
