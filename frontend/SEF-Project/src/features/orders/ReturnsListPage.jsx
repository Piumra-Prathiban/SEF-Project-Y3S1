import { useEffect, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { useAuth } from '../../contexts/AuthContext';
import {
  getReturns,
  ReturnReasonName,
  ReturnStatus,
  ReturnStatusName,
} from '../../services/orderService';
import { formatCurrency, formatDateTime } from '../../utils/format';
import { useSessionGuard } from '../../hooks/useSessionGuard';
import { ApiErrorAlert } from '../../components/ui/ApiErrorAlert';
import { LoadingState } from '../../components/ui/LoadingState';
import StatusBadge from '../../components/StatusBadge';
import './orders.css';

function ReturnsListPage() {
  const { token } = useAuth();
  const guardSessionExpiry = useSessionGuard();
  const [searchParams, setSearchParams] = useSearchParams();
  const [result, setResult] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [retry, setRetry] = useState(0);
  const page = Math.max(1, Number(searchParams.get('page')) || 1);
  const status = searchParams.get('status') ?? '';
  const orderNumber = searchParams.get('orderNumber') ?? '';

  useEffect(() => {
    let cancelled = false;
    // A URL change begins a new request and must replace the prior page state.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setLoading(true);
    setError(null);
    getReturns(token, { page, pageSize: 20, status, orderNumber })
      .then((response) => {
        if (!cancelled) setResult(response);
      })
      .catch((requestError) => {
        if (!cancelled && !guardSessionExpiry(requestError)) setError(requestError);
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => { cancelled = true; };
  }, [token, page, status, orderNumber, retry, guardSessionExpiry]);

  function updateFilter(name, value) {
    const next = new URLSearchParams(searchParams);
    if (value === '') next.delete(name); else next.set(name, value);
    if (name !== 'page') next.delete('page');
    setSearchParams(next);
  }

  return (
    <div className="orders-page">
      <div className="page-title-row">
        <div>
          <p className="section-eyebrow">After-sales care</p>
          <h1>Returns</h1>
          <p>Track requests from review through receipt and refund.</p>
        </div>
      </div>

      <div className="orders-filters">
        <label>
          Status
          <select value={status} onChange={(event) => updateFilter('status', event.target.value)}>
            <option value="">All statuses</option>
            {Object.entries(ReturnStatus).map(([name, value]) => <option key={name} value={value}>{ReturnStatusName[value]}</option>)}
          </select>
        </label>
        <label>
          Order number
          <input value={orderNumber} onChange={(event) => updateFilter('orderNumber', event.target.value)} placeholder="ORD-…" />
        </label>
      </div>

      {loading ? <LoadingState /> : error ? (
        <ApiErrorAlert message={error.message || 'Returns could not be loaded.'} onRetry={() => setRetry((value) => value + 1)} />
      ) : result?.items.length === 0 ? (
        <div className="orders-empty"><h2>No returns found</h2><p>Return requests will appear here once submitted.</p></div>
      ) : (
        <div className="table-scroll">
          <table className="orders-table" aria-label="Returns">
            <thead><tr><th>Return</th><th>Order</th><th>Requested</th><th>Reason</th><th>Status</th><th>Refund</th><th>Items</th></tr></thead>
            <tbody>
              {result?.items.map((itemReturn) => (
                <tr key={itemReturn.id}>
                  <td><strong>{itemReturn.returnNumber}</strong></td>
                  <td><Link to={`/orders/${itemReturn.orderId}`}>{itemReturn.orderNumber}</Link></td>
                  <td><time dateTime={itemReturn.requestedAt}>{formatDateTime(itemReturn.requestedAt)}</time></td>
                  <td>{ReturnReasonName[itemReturn.reason]}</td>
                  <td><StatusBadge status={itemReturn.status} kind="return" /></td>
                  <td>{formatCurrency(itemReturn.refundAmount, itemReturn.currency)}</td>
                  <td>{itemReturn.items.reduce((sum, item) => sum + item.quantity, 0)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {result && result.totalCount > result.pageSize && (
        <nav className="returns-pagination" aria-label="Returns pages">
          <button type="button" disabled={page <= 1} onClick={() => updateFilter('page', String(page - 1))}>Previous</button>
          <span>Page {page} of {Math.ceil(result.totalCount / result.pageSize)}</span>
          <button type="button" disabled={page >= Math.ceil(result.totalCount / result.pageSize)} onClick={() => updateFilter('page', String(page + 1))}>Next</button>
        </nav>
      )}
    </div>
  );
}

export default ReturnsListPage;
