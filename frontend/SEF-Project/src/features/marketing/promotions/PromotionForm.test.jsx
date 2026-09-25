import { fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import PromotionForm from './PromotionForm';

const targets = {
  products: [
    { id: 'p1', name: 'Margherita Pizza', isActive: true },
    { id: 'p2', name: 'Cola', isActive: true },
  ],
  categories: [{ id: 'c1', name: 'Desserts', isActive: true }],
};

const campaigns = [
  {
    id: 'camp-1',
    name: 'Summer Launch',
    startDate: '2026-09-01T00:00:00Z',
    endDate: '2026-12-31T00:00:00Z',
  },
];

function renderForm(onSubmit = vi.fn()) {
  render(<PromotionForm targets={targets} campaigns={campaigns} onSubmit={onSubmit} />);
  return onSubmit;
}

async function fillValidForm(user) {
  await user.type(screen.getByLabelText(/^Name/), 'Autumn Deal');
  await user.type(screen.getByLabelText(/^Discount value/), '15');
  fireEvent.change(screen.getByLabelText(/^Start date/), { target: { value: '2026-10-01' } });
  fireEvent.change(screen.getByLabelText(/^End date/), { target: { value: '2026-10-31' } });
  await user.click(screen.getByLabelText('Margherita Pizza'));
}

describe('PromotionForm', () => {
  it('shows client-side validation errors and does not submit', async () => {
    const user = userEvent.setup();
    const onSubmit = renderForm();

    await user.click(screen.getByRole('button', { name: 'Save promotion' }));

    expect(screen.getByText('Name is required.')).toBeInTheDocument();
    expect(screen.getByText('Start date is required.')).toBeInTheDocument();
    expect(screen.getByText('Choose at least one product or category.')).toBeInTheDocument();
    expect(screen.getByLabelText(/^Name/)).toHaveAttribute('aria-invalid', 'true');
    expect(screen.getByRole('alert')).toHaveTextContent('Please correct the highlighted fields.');
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it('rejects a percentage above 100', async () => {
    const user = userEvent.setup();
    const onSubmit = renderForm();

    await fillValidForm(user);
    await user.clear(screen.getByLabelText(/^Discount value/));
    await user.type(screen.getByLabelText(/^Discount value/), '150');
    await user.click(screen.getByRole('button', { name: 'Save promotion' }));

    expect(
      screen.getByText('Percentage must be greater than 0 and at most 100.')
    ).toBeInTheDocument();
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it('rejects dates outside the chosen campaign', async () => {
    const user = userEvent.setup();
    const onSubmit = renderForm();

    await fillValidForm(user);
    fireEvent.change(screen.getByLabelText(/^End date/), { target: { value: '2027-02-01' } });
    await user.selectOptions(screen.getByLabelText(/^Campaign/), 'camp-1');
    await user.click(screen.getByRole('button', { name: 'Save promotion' }));

    expect(screen.getByText(/Dates must fall within the campaign/)).toBeInTheDocument();
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it('submits the API payload when valid', async () => {
    const user = userEvent.setup();
    const onSubmit = renderForm(vi.fn().mockResolvedValue(undefined));

    await fillValidForm(user);
    await user.selectOptions(screen.getByLabelText(/^Campaign/), 'camp-1');
    await user.click(screen.getByRole('button', { name: 'Save promotion' }));

    expect(onSubmit).toHaveBeenCalledWith({
      name: 'Autumn Deal',
      description: null,
      type: 0,
      discountValue: 15,
      startDate: '2026-10-01T00:00:00Z',
      endDate: '2026-10-31T00:00:00Z',
      isActive: true,
      campaignId: 'camp-1',
      productIds: ['p1'],
      categoryIds: [],
    });
  });

  it('shows server-side field errors', async () => {
    const user = userEvent.setup();
    renderForm(vi.fn().mockRejectedValue({
      status: 400,
      data: { errors: { Name: ['A promotion with this name already exists.'] } },
    }));

    await fillValidForm(user);
    await user.click(screen.getByRole('button', { name: 'Save promotion' }));

    expect(
      await screen.findByText('A promotion with this name already exists.')
    ).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Save promotion' })).toBeEnabled();
  });

  it('shows server-side business rule errors', async () => {
    const user = userEvent.setup();
    renderForm(vi.fn().mockRejectedValue({
      status: 409,
      message: "Promotion dates must fall within the campaign's dates.",
    }));

    await fillValidForm(user);
    await user.click(screen.getByRole('button', { name: 'Save promotion' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(
      "Promotion dates must fall within the campaign's dates."
    );
  });
});
