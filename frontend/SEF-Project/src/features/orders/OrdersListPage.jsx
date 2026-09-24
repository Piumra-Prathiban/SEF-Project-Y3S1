import { useEffect, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { useAuth } from '../../contexts/AuthContext';
import { getOrders, OrderStatus } from '../../services/orderService';
import { formatCurrency, formatDate } from '../../utils/format';
import { isStaff } from '../../utils/roles';
import { useSessionGuard } from '../../hooks/useSessionGuard';
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

function readPage(searchParams) {
  const page = Number(searchParams.get('page'));

  return Number.isInteger(page) && page > 0 ? page : 1;
}

function OrdersListPage() {
  const { token, user } = useAuth();
  const guardSessionExpiry = useSessionGuard();
  const [searchParams, setSearchParams] = useSearchParams();

  const page = readPage(searchParams);
  const status = searchParams.get('status') ?? '';
  const from = searchParams.get('from') ?? '';
  const to = searchParams.get('to') ?? '';
  const sortBy = searchParams.get('sortBy') ?? '';
  const sortDirection = searchParams.get('sortDirection') ?? '';
  const orderNumber = searchParams.get('orderNumber') ?? '';
  const customerId = searchParams.get('customerId') ?? '';

  const [orderNumberInput, setOrderNumberInput] = useState(orderNumber);
  const [customerIdInput, setCustomerIdInput] = useState(customerId);
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [retryTick, setRetryTick] = useState(0);

  const canFilterByCustomer = isStaff(user);

  function updateParams(changes) {
    const next = new URLSearchParams(searchParams);

    Object.entries(changes).forEach(([key, value]) => {
      if (value === '' || value === null || value === undefined) {
        next.delete(key);
      } else {
        next.set(key, String(value));
      }
    });

    setSearchParams(next, { replace: true });
  }

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
          ...(customerId !== '' && { customerId: Number(customerId) }),
        };

        const response = await getOrders(token, query);

        if (!cancelled) {
          setData(response);
        }
      } catch (err) {
        if (!cancelled && !guardSessionExpiry(err)) {
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
    guardSessionExpiry,
  ]);

  function applyFilter(key, value) {
    updateParams({ [key]: value, page: undefined });
  }

  function handleSearchSubmit(event) {
    event.preventDefault();

    updateParams({
      orderNumber: orderNumberInput.trim(),
      customerId: customerIdInput === '' ? undefined : Number(customerIdInput),
      page: undefined,
    });
  }

  function handleClearFilters() {
    setOrderNumberInput('');
    setCustomerIdInput('');
    updateParams({
      status: undefined,
      from: undefined,
      to: undefined,
      sortBy: undefined,
      sortDirection: undefined,
      orderNumber: undefined,
      customerId: undefined,
      page: undefined,
    });
  }

  function handlePageChange(nextPage) {
    updateParams({ page: nextPage === 1 ? undefined : nextPage });
  }

  const hasActiveFilters = Boolean(
    status || from || to || orderNumber || customerId !== '',
  );

  const showLoading = loading || (!error && !data);

  return (
    <div className="orders-page">
      <h1>Orders</h1>

      <section className="orders-filters" aria-label="Order filters">
        <label htmlFor="order-status-filter">
          Status
          <select
            id="order-status-filter"
            value={status}
            onChange={(event) => applyFilter('status', event.target.value)}
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
            onChange={(event) => applyFilter('from', event.target.value)}
          />
        </label>

        <label htmlFor="order-to-filter">
          To
          <input
            id="order-to-filter"
            type="date"
            value={to}
            onChange={(event) => applyFilter('to', event.target.value)}
          />
        </label>

        <label htmlFor="order-sort-by">
          Sort by
          <select
            id="order-sort-by"
            value={sortBy}
            onChange={(event) => applyFilter('sortBy', event.target.value)}
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
              applyFilter('sortDirection', event.target.value)
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
              type="search"
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

      <div className="orders-results" aria-busy={showLoading}>
        {showLoading ? (
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
            <div className="table-scroll">
              <table className="orders-table" aria-label="Orders">
                <thead>
                  <tr>
                    <th scope="col">Order number</th>
                    <th scope="col">Placed</th>
                    <th scope="col">Status</th>
                    <th scope="col">Total</th>
                  </tr>
                </thead>
                <tbody>
                  {data.items.map((order) => (
                    <tr key={order.id}>
                      <td>
                        <Link to={`/orders/${order.id}`}>
                          {order.orderNumber}
                        </Link>
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
            </div>

            <Pagination
              page={data.page}
              pageSize={data.pageSize}
              totalCount={data.totalCount}
              onPageChange={handlePageChange}
            />
          </>
        )}
      </div>
    </div>
  );
}

export default OrdersListPage;
