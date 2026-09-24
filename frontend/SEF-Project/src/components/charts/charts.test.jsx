import { fireEvent, render, screen, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it } from 'vitest';
import BarList from './BarList';
import LineChart from './LineChart';

const points = [
  { key: 'a', label: '1 Oct', value: 100 },
  { key: 'b', label: '2 Oct', value: 300 },
  { key: 'c', label: '3 Oct', value: 200 },
];

describe('LineChart', () => {
  it('draws the line and offers a data table', () => {
    const { container } = render(
      <LineChart title="Revenue over time" points={points} formatValue={(v) => `LKR ${v}`} />
    );

    expect(container.querySelector('.chart-line')).toBeInTheDocument();
    expect(container.querySelector('.chart-area')).toBeInTheDocument();

    const table = screen.getByRole('table', { name: 'Revenue over time' });
    expect(within(table).getAllByRole('row')).toHaveLength(4);
    expect(within(table).getByText('LKR 300')).toBeInTheDocument();
  });

  it('reads values with the keyboard', () => {
    render(<LineChart title="Revenue over time" points={points} formatValue={(v) => `LKR ${v}`} />);

    const chart = screen.getByRole('img', { name: /Revenue over time/ });

    fireEvent.focus(chart);
    expect(screen.getByRole('status')).toHaveTextContent('LKR 200');
    expect(screen.getByRole('status')).toHaveTextContent('3 Oct');

    fireEvent.keyDown(chart, { key: 'ArrowLeft' });
    expect(screen.getByRole('status')).toHaveTextContent('LKR 300');

    fireEvent.keyDown(chart, { key: 'Escape' });
    expect(screen.queryByRole('status')).not.toBeInTheDocument();
  });

  it('renders a single point without a line', () => {
    const { container } = render(<LineChart title="One day" points={[points[0]]} />);

    expect(container.querySelector('.chart-line')).not.toBeInTheDocument();
    expect(container.querySelector('.chart-marker')).toBeInTheDocument();
  });
});

describe('BarList', () => {
  it('scales bars to the largest value and shows values as text', () => {
    render(
      <MemoryRouter>
        <BarList
          title="Top products"
          items={[
            { key: '1', label: 'Pepperoni', value: 200, valueLabel: '200 units', to: '/p/1' },
            { key: '2', label: 'Cola', value: 50, valueLabel: '50 units' },
          ]}
        />
      </MemoryRouter>
    );

    const bars = screen.getAllByTestId('bar-fill');
    expect(bars[0]).toHaveStyle({ width: '100%' });
    expect(bars[1]).toHaveStyle({ width: '25%' });
    expect(screen.getByRole('link', { name: 'Pepperoni' })).toHaveAttribute('href', '/p/1');
    expect(screen.getByText('50 units')).toBeInTheDocument();
  });

  it('draws empty bars when every value is zero', () => {
    render(<BarList title="Low" items={[{ key: '1', label: 'Tiramisu', value: 0 }]} />);

    expect(screen.getByTestId('bar-fill')).toHaveStyle({ width: '0%' });
  });
});
