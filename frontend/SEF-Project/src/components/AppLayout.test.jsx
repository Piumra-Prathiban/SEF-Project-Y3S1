import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it, vi } from 'vitest';
import { useAuth } from '../contexts/AuthContext';
import AppLayout from './AppLayout';

vi.mock('../contexts/AuthContext', () => ({ useAuth: vi.fn() }));

function renderAs(user) {
  vi.mocked(useAuth).mockReturnValue({
    isAuthenticated: Boolean(user),
    user,
    logout: vi.fn(),
  });

  render(
    <MemoryRouter>
      <AppLayout />
    </MemoryRouter>
  );
}

describe('AppLayout navigation', () => {
  it.each(['Staff', 'Administrator'])('shows Marketing to %s', (role) => {
    renderAs({ email: 'staff@test.com', role });

    expect(screen.getByRole('link', { name: 'Marketing' })).toBeInTheDocument();
  });

  it('hides Marketing from customers', () => {
    renderAs({ email: 'customer@test.com', role: 'Customer' });

    expect(screen.queryByRole('link', { name: 'Marketing' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Log out' })).toBeInTheDocument();
  });

  it('shows a login link to anonymous users', () => {
    renderAs(null);

    expect(screen.queryByRole('link', { name: 'Marketing' })).not.toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Log in' })).toBeInTheDocument();
  });
});
