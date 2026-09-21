import { useState } from 'react';
import { useAuth } from './contexts/AuthContext';

function App() {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');

  const { user, isAuthenticated, login, logout } = useAuth();

  const [error, setError] = useState(null);

  async function handleLogin(event) {
    event.preventDefault();
    setError(null);

    try {
      await login(email, password);
    } catch (err) {
      setError({
        status: err.status,
        message: err.message,
        data: err.data,
      });
    }
  }

  if (isAuthenticated) {
    return (
      <main>
        <h1>Welcome</h1>

        <p>
          Logged in as: {user.email}
        </p>

        <p>
          Role: {user.role}
        </p>

        <button type="button" onClick={logout}>
          Logout
        </button>
      </main>
    );
  }

  return (
    <main>
      <h1>React → API Test</h1>

      <form onSubmit={handleLogin}>
        <div>
          <label>
            Email
            <input
              type="email"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              required
            />
          </label>
        </div>

        <div>
          <label>
            Password
            <input
              type="password"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              required
            />
          </label>
        </div>

        <button type="submit">
          Login
        </button>
      </form>

      {error && (
        <pre>
          {JSON.stringify(error, null, 2)}
        </pre>
      )}
    </main>
  );
}

export default App;