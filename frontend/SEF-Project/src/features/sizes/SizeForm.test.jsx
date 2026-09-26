import { expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SizeForm } from './SizeForm';

it('edits size description and display order using the backend fields', async () => {
  const onSubmit = vi.fn();
  const user = userEvent.setup();
  render(<SizeForm initialValue={{ name: 'M', description: 'Standard fit', displayOrder: 3, isActive: true }} isSubmitting={false} onCancel={vi.fn()} onSubmit={onSubmit} />);

  await user.clear(screen.getByRole('spinbutton', { name: /Display order/ }));
  await user.type(screen.getByRole('spinbutton', { name: /Display order/ }), '2');
  await user.click(screen.getByRole('button', { name: 'Save size' }));

  expect(onSubmit).toHaveBeenCalledWith({ name: 'M', description: 'Standard fit', displayOrder: 2, isActive: true });
});
