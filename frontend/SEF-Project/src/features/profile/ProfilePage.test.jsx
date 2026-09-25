import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { ProfilePage } from './ProfilePage';
import {
  createAddress,
  getAddresses,
  getProfile,
  updateAddress,
  updateProfile,
} from './profileService';

vi.mock('./profileService', () => ({
  getProfile: vi.fn(),
  updateProfile: vi.fn(),
  getAddresses: vi.fn(),
  createAddress: vi.fn(),
  updateAddress: vi.fn(),
  deleteAddress: vi.fn(),
}));

vi.mock('../../contexts/AuthContext', () => ({
  useAuth: () => ({ token: 'test-token' }),
}));

const PROFILE = {
  customerId: 7,
  email: 'asha@example.com',
  firstName: 'Asha',
  lastName: 'Perera',
  memberSince: '2026-09-01T00:00:00Z',
};

const ADDRESS = {
  id: 3,
  label: 'Home',
  addressLine1: '12 Galle Road',
  addressLine2: null,
  city: 'Colombo',
  province: null,
  postalCode: '00300',
  country: 'Sri Lanka',
  isDefault: true,
};

function renderProfile() {
  return render(
    <MemoryRouter initialEntries={['/profile']}>
      <ProfilePage />
    </MemoryRouter>,
  );
}

beforeEach(() => {
  vi.clearAllMocks();
  getProfile.mockResolvedValue(PROFILE);
  getAddresses.mockResolvedValue([ADDRESS]);
  updateProfile.mockResolvedValue(PROFILE);
  createAddress.mockResolvedValue(ADDRESS);
  updateAddress.mockResolvedValue(ADDRESS);
});

describe('ProfilePage', () => {
  it('shows the account details and saved addresses', async () => {
    renderProfile();

    expect(await screen.findByLabelText('First name')).toHaveValue('Asha');
    expect(screen.getByLabelText('Last name')).toHaveValue('Perera');
    expect(screen.getByLabelText('Email address')).toHaveValue('asha@example.com');
    expect(screen.getByText('Home')).toBeInTheDocument();
    expect(screen.getByText('Default')).toBeInTheDocument();
    expect(screen.getByText(/12 Galle Road/)).toBeInTheDocument();
  });

  it('saves trimmed profile details', async () => {
    const user = userEvent.setup();

    renderProfile();

    const firstName = await screen.findByLabelText('First name');

    await user.clear(firstName);
    await user.type(firstName, '  Ashani  ');
    await user.click(screen.getByRole('button', { name: 'Save profile' }));

    await waitFor(() => {
      expect(updateProfile).toHaveBeenCalledWith('test-token', {
        email: 'asha@example.com',
        firstName: 'Ashani',
        lastName: 'Perera',
      });
    });

    expect(await screen.findByRole('status')).toHaveTextContent('Profile updated.');
  });

  it('creates a new address', async () => {
    const user = userEvent.setup();

    renderProfile();

    await screen.findByLabelText('First name');
    await user.click(screen.getByRole('button', { name: 'Add address' }));
    await user.type(screen.getByLabelText('Label'), 'Work');
    await user.type(screen.getByLabelText('Address line 1'), '99 Union Place');
    await user.type(screen.getByLabelText('City'), 'Colombo');
    await user.type(screen.getByLabelText('Postal code'), '00200');
    await user.click(screen.getByRole('button', { name: 'Save address' }));

    await waitFor(() => {
      expect(createAddress).toHaveBeenCalledWith('test-token', {
        label: 'Work',
        addressLine1: '99 Union Place',
        addressLine2: '',
        city: 'Colombo',
        province: '',
        postalCode: '00200',
        country: 'Sri Lanka',
        isDefault: false,
      });
    });
  });

  it('rejects an invalid postal code before calling the API', async () => {
    const user = userEvent.setup();

    renderProfile();

    await screen.findByLabelText('First name');
    await user.click(screen.getByRole('button', { name: 'Add address' }));
    await user.type(screen.getByLabelText('Label'), 'Work');
    await user.type(screen.getByLabelText('Address line 1'), '99 Union Place');
    await user.type(screen.getByLabelText('City'), 'Colombo');
    await user.type(screen.getByLabelText('Postal code'), '@@');
    await user.click(screen.getByRole('button', { name: 'Save address' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Enter a valid postal code.',
    );
    expect(createAddress).not.toHaveBeenCalled();
  });

  it('promotes a saved address to the default', async () => {
    getAddresses.mockResolvedValue([{ ...ADDRESS, isDefault: false }]);

    const user = userEvent.setup();
    renderProfile();

    await screen.findByLabelText('First name');
    await user.click(screen.getByRole('button', { name: 'Make default' }));

    await waitFor(() => {
      expect(updateAddress).toHaveBeenCalledWith('test-token', 3, {
        label: 'Home',
        addressLine1: '12 Galle Road',
        addressLine2: '',
        city: 'Colombo',
        province: '',
        postalCode: '00300',
        country: 'Sri Lanka',
        isDefault: true,
      });
    });
  });

  it('shows the API error and retries', async () => {
    getProfile.mockRejectedValueOnce(
      Object.assign(new Error('Profile unavailable'), { status: 500 }),
    );

    const user = userEvent.setup();
    renderProfile();

    expect(await screen.findByRole('alert')).toHaveTextContent('Profile unavailable');

    await user.click(screen.getByRole('button', { name: 'Try again' }));

    expect(await screen.findByLabelText('First name')).toHaveValue('Asha');
  });
});
