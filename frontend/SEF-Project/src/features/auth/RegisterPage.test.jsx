import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import RegisterPage from './RegisterPage';

const register = vi.fn();
vi.mock('../../contexts/AuthContext', () => ({
  useAuth: () => ({ isAuthenticated: false, register }),
}));

beforeEach(() => { register.mockReset(); register.mockResolvedValue({}); });

function renderPage() {
  render(<MemoryRouter initialEntries={['/register']}><Routes><Route path="/register" element={<RegisterPage />} /><Route path="/orders" element={<p>Orders landing</p>} /></Routes></MemoryRouter>);
}

describe('RegisterPage', () => {
  it('creates a customer account with the API contract and opens orders', async () => {
    const user = userEvent.setup();
    renderPage();
    await user.type(screen.getByLabelText('First name'), 'Asha');
    await user.type(screen.getByLabelText('Last name'), 'Perera');
    await user.type(screen.getByLabelText('Email address'), 'asha@example.com');
    await user.type(screen.getByLabelText(/^Password/), 'securepass123');
    await user.type(screen.getByLabelText('Confirm password'), 'securepass123');
    await user.click(screen.getByRole('button', { name: /Create account/ }));
    expect(register).toHaveBeenCalledWith({ firstName: 'Asha', lastName: 'Perera', email: 'asha@example.com', password: 'securepass123' });
    expect(await screen.findByText('Orders landing')).toBeInTheDocument();
  });

  it('keeps the user on the form when passwords differ', async () => {
    const user = userEvent.setup();
    renderPage();
    await user.type(screen.getByLabelText('First name'), 'Asha');
    await user.type(screen.getByLabelText('Last name'), 'Perera');
    await user.type(screen.getByLabelText('Email address'), 'asha@example.com');
    await user.type(screen.getByLabelText(/^Password/), 'securepass123');
    await user.type(screen.getByLabelText('Confirm password'), 'anotherpass123');
    await user.click(screen.getByRole('button', { name: /Create account/ }));
    expect(register).not.toHaveBeenCalled();
    expect(screen.getByRole('alert')).toHaveTextContent('Passwords do not match.');
  });
});
