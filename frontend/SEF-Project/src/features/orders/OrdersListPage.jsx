import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../../contexts/AuthContext';
import { getOrders, OrderStatus } from '../../services/orderService';
import { formatCurrency, formatDate } from '../../utils/format';
import { isStaff } from '../../utils/roles';
import Loading from '../../components/Loading';
import ErrorAlert from '../../components/ErrorAlert';
import StatusBadge from '../../components/StatusBadge';
import Pagination from '../../components/Pagination';
import './orders.css';

function localDayToIso(dateString, endOfDay) {
  const [year, month, day] = dateString.split('-').map(Number);

  return new Date(
    year,
    month - 1,
    day,
    endOfDay ? 23 : 0,
    endOfDay ? 59 : 0,
    endOfDay ? 59 : 0,
    endOfDay ? 999 : 0,
  ).toISOString();
}

function OrdersListPage() {
  const { token, user } = useAuth();

  const [page, setPage] = useState(1);
  const [status, setStatus] = useState('');
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const [sortBy, setSortBy] = useState('');
  const [sortDirection, setSortDirection] = useState('');
  const [orderNumberInput, setOrderNumberInput] = useState('');
  const [customerIdInput, setCustomerIdInput] = useState('');
  const [orderNumber, setOrderNumber] = useState('');
  const [customerId, setCustomerId] = useState('');
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [retryTick, setRetryTick] = useState(0);

  const canFilterByCustomer = isStaff(user);

  useEffect(() => {
    let cancelled = false;

    async function load() {
      setLoading(true);
      setError(null);

      try {
        const query = {
          page,
          ...(status && { status }),
          ...(sortBy && { sortBy }),
          ...(sortDirection && { sortDirection }),
          ...(from && { from: localDayToIso(from) }),
          ...(to && { to: localDayToIso(to, true) }),
          ...(orderNumber && { orderNumber }),
          ...(customerId !== '' && { customerId }),
        };

        const response = await getOrders(token, query);

        if (!cancelled) {
          setData(response);
        }
      } catch (err) {
        if (!cancelled) {
          setError(err);
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    }

    load();

    return () => {
      cancelled = true;
    };
  }, [
    token,
    page,
    status,
    from,
    to,
    sortBy,
    sortDirection,
    orderNumber,
    customerId,
    retryTick,
  ]);

  function applyImmediateFilter(setter, value) {
    setter(value);
    setPage(1);
  }

  function handleSearchSubmit(event) {
    event.preventDefault();

    setOrderNumber(orderNumberInput.trim());
    setCustomerId(customerIdInput === '' ? '' : Number(customerIdInput));
    setPage(1);
  }

  function handleClearFilters() {
    setStatus('');
    setFrom('');
    setTo('');
    setSortBy('');
    setSortDirection('');
    setOrderNumberInput('');
    setCustomerIdInput('');
    setOrderNumber('');
    setCustomerId('');
    setPage(1);
  }

  const hasActiveFilters = Boolean(
    status || from || to || orderNumber || customerId !== '',
  );

  return (
    <div className="orders-page">
      <h1>Orders</h1>

      <section className="orders-filters" aria-label="Order filters">
        <label htmlFor="order-status-filter">
          Status
          <select
            id="order-status-filter"
            value={status}
            onChange={(event) =>
              applyImmediateFilter(setStatus, event.target.value)
            }
          >
            <option value="">All statuses</option>
            {Object.keys(OrderStatus).map((name) => (
              <option key={name} value={name}>
                {name}
              </option>
            ))}
          </select>
        </label>

        <label htmlFor="order-from-filter">
          From
          <input
            id="order-from-filter"
            type="date"
            value={from}
            onChange={(event) =>
              applyImmediateFilter(setFrom, event.target.value)
            }
          />
        </label>

        <label htmlFor="order-to-filter">
          To
          <input
            id="order-to-filter"
            type="date"
            value={to}
            onChange={(event) =>
              applyImmediateFilter(setTo, event.target.value)
            }
          />
        </label>

        <label htmlFor="order-sort-by">
          Sort by
          <select
            id="order-sort-by"
            value={sortBy}
            onChange={(event) =>
              applyImmediateFilter(setSortBy, event.target.value)
            }
          >
            <option value="">Placed (default)</option>
            <option value="total">Total</option>
            <option value="orderNumber">Order number</option>
            <option value="status">Status</option>
            <option value="createdAt">Created</option>
          </select>
        </label>

        <label htmlFor="order-sort-direction">
          Sort direction
          <select
            id="order-sort-direction"
            value={sortDirection}
            onChange={(event) =>
              applyImmediateFilter(setSortDirection, event.target.value)
            }
          >
            <option value="">Descending</option>
            <option value="asc">Ascending</option>
          </select>
        </label>

        <form className="orders-search" onSubmit={handleSearchSubmit}>
          <label htmlFor="order-number-search">
            Order number
            <input
              id="order-number-search"
              type="text"
              value={orderNumberInput}
              onChange={(event) => setOrderNumberInput(event.target.value)}
              placeholder="e.g. ORD-1001"
            />
          </label>

          {canFilterByCustomer && (
            <label htmlFor="customer-id-search">
              Customer ID
              <input
                id="customer-id-search"
                type="number"
                min="1"
                value={customerIdInput}
                onChange={(event) => setCustomerIdInput(event.target.value)}
              />
            </label>
          )}

          <button type="submit">Apply</button>

          {hasActiveFilters && (
            <button type="button" onClick={handleClearFilters}>
              Clear filters
            </button>
          )}
        </form>
      </section>

      {loading ? (
        <Loading />
      ) : error ? (
        <ErrorAlert
          error={error}
          onRetry={() => setRetryTick((tick) => tick + 1)}
        />
      ) : data.items.length === 0 ? (
        <div className="orders-empty">
          <p>No orders found.</p>
          {hasActiveFilters && (
            <button type="button" onClick={handleClearFilters}>
              Clear filters
            </button>
          )}
        </div>
      ) : (
        <>
          <table className="orders-table">
            <thead>
              <tr>
                <th>Order number</th>
                <th>Placed</th>
                <th>Status</th>
                <th>Total</th>
              </tr>
            </thead>
            <tbody>
              {data.items.map((order) => (
                <tr key={order.id}>
                  <td>
                    <Link to={`/orders/${order.id}`}>{order.orderNumber}</Link>
                  </td>
                  <td>{formatDate(order.placedAt)}</td>
                  <td>
                    <StatusBadge status={order.status} />
                  </td>
                  <td>{formatCurrency(order.total, order.currency)}</td>
                </tr>
              ))}
            </tbody>
          </table>

          <Pagination
            page={data.page}
            pageSize={data.pageSize}
            totalCount={data.totalCount}
            onPageChange={setPage}
          />
        </>
      )}
    </div>
  );
}

export default OrdersListPage;
