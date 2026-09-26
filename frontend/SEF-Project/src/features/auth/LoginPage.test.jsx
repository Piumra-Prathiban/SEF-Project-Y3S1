import { expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import LoginPage from './LoginPage';

const login = vi.fn().mockResolvedValue({ user: { role: 'Customer' } });
vi.mock('../../contexts/AuthContext', () => ({
  useAuth: () => ({ isAuthenticated: false, user: null, login }),
}));

it('returns a shopper to checkout after signing in', async () => {
  const user = userEvent.setup();
  render(<MemoryRouter initialEntries={[{ pathname: '/login', state: { from: { pathname: '/checkout', search: '' } } }]}><Routes><Route path="/login" element={<LoginPage />} /><Route path="/checkout" element={<p>Checkout landing</p>} /></Routes></MemoryRouter>);
  await user.type(screen.getByLabelText('Email address'), 'asha@example.com');
  await user.type(screen.getByLabelText(/^Password/), 'securepass123');
  await user.click(screen.getByRole('button', { name: 'Sign in' }));
  expect(login).toHaveBeenCalledWith('asha@example.com', 'securepass123');
  expect(await screen.findByText('Checkout landing')).toBeInTheDocument();
});
