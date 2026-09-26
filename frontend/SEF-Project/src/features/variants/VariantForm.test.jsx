import { expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { VariantForm } from './VariantForm';

it('submits all fields required to create a sellable variant and its stock record', async () => {
  const onSubmit = vi.fn();
  const user = userEvent.setup();
  render(<VariantForm colours={[{ id: 'colour-1', name: 'Black' }]} isSubmitting={false} onCancel={vi.fn()} onSubmit={onSubmit} sizes={[{ id: 'size-1', name: 'M' }]} />);

  await user.type(screen.getByRole('textbox', { name: 'SKU' }), 'TSH-M-BLK');
  await user.type(screen.getByRole('textbox', { name: 'Variant name' }), 'M / Black');
  await user.selectOptions(screen.getByRole('combobox', { name: 'Size' }), 'size-1');
  await user.selectOptions(screen.getByRole('combobox', { name: 'Colour' }), 'colour-1');
  await user.type(screen.getByRole('spinbutton', { name: 'Price' }), '3500');
  await user.clear(screen.getByRole('spinbutton', { name: /Initial stock/ }));
  await user.type(screen.getByRole('spinbutton', { name: /Initial stock/ }), '12');
  await user.clear(screen.getByRole('spinbutton', { name: /Reorder level/ }));
  await user.type(screen.getByRole('spinbutton', { name: /Reorder level/ }), '4');
  await user.click(screen.getByRole('button', { name: 'Save variant' }));

  expect(onSubmit).toHaveBeenCalledWith({ sku: 'TSH-M-BLK', name: 'M / Black', sizeId: 'size-1', colourId: 'colour-1', price: 3500, isActive: true, initialQuantityOnHand: 12, reorderLevel: 4 });
});
