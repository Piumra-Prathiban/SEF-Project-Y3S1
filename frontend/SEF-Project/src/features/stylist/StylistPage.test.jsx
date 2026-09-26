import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import StylistPage from './StylistPage';
import { apiRequest } from '../../services/api';

vi.mock('../../contexts/AuthContext', () => ({ useAuth: () => ({ token: 'customer-token' }) }));
vi.mock('../../services/api', () => ({ apiRequest: vi.fn() }));

beforeEach(() => {
  vi.clearAllMocks();
  apiRequest.mockResolvedValue({
    criteria: { occasion: 'Work dinner' },
    recommendations: [{ productId: 'product-1', variantId: 'variant-1', productName: 'Cotton Shirt', variantName: 'M · Cream', price: 3500, quantity: 1, size: 'M', colour: 'Cream', reason: 'A relaxed option for your evening.' }],
    relaxedCriteria: [], unappliedPreferences: [],
  });
});

describe('StylistPage', () => {
  it('sends fashion preferences and links to real recommended products', async () => {
    const user = userEvent.setup();
    render(<MemoryRouter><StylistPage /></MemoryRouter>);
    await user.type(screen.getByLabelText('Occasion'), 'Work dinner');
    await user.type(screen.getByLabelText('Budget (LKR)'), '5000');
    await user.type(screen.getByLabelText('Preferred size'), 'M');
    await user.type(screen.getByLabelText(/^Colours you like/), 'Cream, Navy');
    await user.click(screen.getByRole('button', { name: /Find my pieces/ }));
    expect(apiRequest).toHaveBeenCalledWith('/recommendations', expect.objectContaining({ method: 'POST', token: 'customer-token', body: JSON.stringify({ occasion: 'Work dinner', budget: 5000, preferredSize: 'M', preferredColours: ['Cream', 'Navy'], stylePreferences: null }) }));
    expect(await screen.findByRole('heading', { name: 'Cotton Shirt' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /View piece/ })).toHaveAttribute('href', '/shop/product-1');
  });
});
