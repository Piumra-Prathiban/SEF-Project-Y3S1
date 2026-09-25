import { fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { CAMPAIGN_STATUSES } from '../marketingConstants';
import { EMPTY_CAMPAIGN_FORM } from '../marketingUtils';
import CampaignForm from './CampaignForm';

describe('CampaignForm', () => {
  it('validates required fields and date order', async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn();
    render(<CampaignForm onSubmit={onSubmit} />);

    await user.click(screen.getByRole('button', { name: 'Save campaign' }));

    expect(screen.getByText('Name is required.')).toBeInTheDocument();
    expect(screen.getByText('Start date is required.')).toBeInTheDocument();

    await user.type(screen.getByLabelText(/^Name/), 'Spring Specials');
    fireEvent.change(screen.getByLabelText(/^Start date/), { target: { value: '2027-04-30' } });
    fireEvent.change(screen.getByLabelText(/^End date/), { target: { value: '2027-04-01' } });
    await user.click(screen.getByRole('button', { name: 'Save campaign' }));

    expect(screen.getByText('End date must be on or after the start date.')).toBeInTheDocument();
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it('submits the API payload with the chosen status', async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn().mockResolvedValue(undefined);
    render(<CampaignForm onSubmit={onSubmit} />);

    await user.type(screen.getByLabelText(/^Name/), 'Spring Specials');
    fireEvent.change(screen.getByLabelText(/^Start date/), { target: { value: '2027-04-01' } });
    fireEvent.change(screen.getByLabelText(/^End date/), { target: { value: '2027-04-30' } });
    await user.selectOptions(screen.getByLabelText(/^Status/), 'Scheduled');
    await user.click(screen.getByRole('button', { name: 'Save campaign' }));

    expect(onSubmit).toHaveBeenCalledWith({
      name: 'Spring Specials',
      description: null,
      startDate: '2027-04-01T00:00:00Z',
      endDate: '2027-04-30T00:00:00Z',
      status: CAMPAIGN_STATUSES.SCHEDULED,
    });
  });

  it('locks the status of a completed campaign', () => {
    render(
      <CampaignForm
        initialValues={{
          ...EMPTY_CAMPAIGN_FORM,
          name: 'Finished',
          startDate: '2026-01-01',
          endDate: '2026-02-01',
          status: String(CAMPAIGN_STATUSES.COMPLETED),
        }}
        onSubmit={vi.fn()}
      />
    );

    expect(screen.getByLabelText(/^Status/)).toBeDisabled();
    expect(
      screen.getByText('Completed and cancelled campaigns cannot change status.')
    ).toBeInTheDocument();
  });
});
