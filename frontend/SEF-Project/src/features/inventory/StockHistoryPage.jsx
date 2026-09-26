import { useEffect, useMemo, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { ApiErrorAlert } from '../../components/ui/ApiErrorAlert';
import { LoadingState } from '../../components/ui/LoadingState';
import { useCatalogApi } from '../../hooks/useCatalogApi';
import { useSessionGuard } from '../../hooks/useSessionGuard';
import { formatDateTime } from '../../utils/format';
import { getProductName, getSku, getVariantId, normalizeInventoryItems, normalizeStockHistoryItems } from './inventoryDashboardUtils';

const TRANSACTION_TYPES = ['Adjustment', 'Stock in', 'Stock out', 'Receipt', 'Sale', 'Reservation', 'Reservation release', 'Transfer'];

function typeLabel(value) {
  return typeof value === 'number' ? TRANSACTION_TYPES[value] || 'Movement' : String(value ?? 'Movement').replace(/([a-z])([A-Z])/g, '$1 $2');
}

export function StockHistoryPage() {
  const api = useCatalogApi();
  const guardSessionExpiry = useSessionGuard();
  const [params, setParams] = useSearchParams();
  const selectedId = params.get('variantId') || '';
  const [inventory, setInventory] = useState([]);
  const [transactions, setTransactions] = useState([]);
  const [search, setSearch] = useState('');
  const [loadingInventory, setLoadingInventory] = useState(true);
  const [loadingHistory, setLoadingHistory] = useState(false);
  const [error, setError] = useState(null);
  const [refreshKey, setRefreshKey] = useState(0);

  useEffect(() => {
    let active = true;
    // Refresh the selector when the user retries an inventory request.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setLoadingInventory(true);
    setError(null);
    api.getInventory().then((response) => {
      if (active) setInventory(normalizeInventoryItems(response));
    }).catch((err) => {
      if (active && !guardSessionExpiry(err)) setError(err?.message || 'Inventory could not be loaded.');
    }).finally(() => { if (active) setLoadingInventory(false); });
    return () => { active = false; };
  }, [api, guardSessionExpiry, refreshKey]);

  useEffect(() => {
    if (!selectedId) return;
    let active = true;
    // This effect intentionally resets the movement list when a variant changes.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setLoadingHistory(true);
    setTransactions([]);
    setError(null);
    api.getStockHistory(selectedId).then((response) => {
      if (active) setTransactions(normalizeStockHistoryItems(response));
    }).catch((err) => {
      if (active && !guardSessionExpiry(err)) setError(err?.message || 'Stock history could not be loaded.');
    }).finally(() => { if (active) setLoadingHistory(false); });
    return () => { active = false; };
  }, [api, guardSessionExpiry, selectedId, refreshKey]);

  const matchingInventory = useMemo(() => inventory.filter((item) => String(getVariantId(item)) === selectedId || `${getProductName(item)} ${getSku(item)}`.toLowerCase().includes(search.toLowerCase())), [inventory, search, selectedId]);
  const selectedItem = inventory.find((item) => String(getVariantId(item)) === selectedId);

  return (
    <div className="stock-history-page">
      <div className="page-header">
        <div><p className="eyebrow">INVENTORY / AUDIT TRAIL</p><h1>Stock History</h1><p className="page-subtitle">Trace every movement for a product variant.</p></div>
        <Link className="button button-secondary" to="/inventory">Back to inventory</Link>
      </div>

      <section className="stock-history-controls" aria-label="Choose a variant">
        <label>Find a product or SKU<input onChange={(event) => setSearch(event.target.value)} placeholder="Search inventory..." type="search" value={search} /></label>
        <label>Product variant<select onChange={(event) => setParams(event.target.value ? { variantId: event.target.value } : {})} value={selectedId}>
          <option value="">Select a variant</option>
          {matchingInventory.map((item) => <option key={getVariantId(item)} value={getVariantId(item)}>{getProductName(item)} · {getSku(item)}</option>)}
        </select></label>
        <button className="button button-secondary" disabled={!selectedId || loadingHistory} onClick={() => setRefreshKey((key) => key + 1)} type="button">Refresh history</button>
      </section>

      <ApiErrorAlert message={error} onRetry={() => setRefreshKey((key) => key + 1)} />
      {loadingInventory ? <LoadingState message="Loading inventory..." /> : error && !inventory.length ? null : !inventory.length ? <div className="empty-state">No inventory records are available.</div> : !selectedId ? <div className="empty-state">Choose a variant to see its stock movements.</div> : loadingHistory ? <LoadingState message="Loading stock movements..." /> : (
        <section className="workspace-panel">
          <div className="workspace-panel__heading"><div><p className="eyebrow">{getSku(selectedItem || {})}</p><h2>{getProductName(selectedItem || {})}</h2></div><span>{transactions.length} movements</span></div>
          {transactions.length === 0 ? <p className="workspace-empty">No stock movements have been recorded for this variant yet.</p> : <div className="table-card"><table className="data-table"><caption className="sr-only">Stock movements for {getProductName(selectedItem || {})}</caption><thead><tr><th>Date</th><th>Movement</th><th>Change</th><th>On hand after</th><th>Reason</th><th>By</th></tr></thead><tbody>{transactions.map((transaction) => <tr key={transaction.id}>
            <td>{transaction.createdAt ? formatDateTime(transaction.createdAt) : '—'}</td>
            <td><span className="status-pill">{typeLabel(transaction.type)}</span></td>
            <td className={transaction.quantityChange > 0 ? 'stock-change-positive' : 'stock-change-negative'}>{transaction.quantityChange > 0 ? '+' : ''}{transaction.quantityChange ?? transaction.quantity ?? '—'}</td>
            <td>{transaction.quantityOnHandAfter ?? '—'}</td>
            <td>{transaction.reason || transaction.reference || '—'}</td>
            <td>{transaction.performedByUserEmail || 'System'}</td>
          </tr>)}</tbody></table></div>}
        </section>
      )}
    </div>
  );
}
