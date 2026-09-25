import { render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { useAuth } from '../contexts/AuthContext';
import ProtectedRoute from './ProtectedRoute';

vi.mock('../contexts/AuthContext', () => ({ useAuth: vi.fn() }));

function renderAt(path) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route path="/login" element={<p>Login page</p>} />
        <Route element={<ProtectedRoute roles={['Staff', 'Administrator']} />}>
          <Route path="/marketing" element={<p>Marketing content</p>} />
        </Route>
      </Routes>
    </MemoryRouter>
  );
}

describe('ProtectedRoute', () => {
  beforeEach(() => {
    vi.mocked(useAuth).mockReset();
  });

  it('redirects anonymous users to the login page', () => {
    vi.mocked(useAuth).mockReturnValue({ isAuthenticated: false, user: null });

    renderAt('/marketing');

    expect(screen.getByText('Login page')).toBeInTheDocument();
  });

  it('denies users without a permitted role', () => {
    vi.mocked(useAuth).mockReturnValue({
      isAuthenticated: true,
      user: { role: 'Customer' },
    });

    renderAt('/marketing');

    expect(screen.getByRole('heading', { name: 'Access denied' })).toBeInTheDocument();
    expect(screen.queryByText('Marketing content')).not.toBeInTheDocument();
  });

  it.each(['Staff', 'Administrator'])('allows %s', (role) => {
    vi.mocked(useAuth).mockReturnValue({ isAuthenticated: true, user: { role } });

    renderAt('/marketing');

    expect(screen.getByText('Marketing content')).toBeInTheDocument();
  });
});
