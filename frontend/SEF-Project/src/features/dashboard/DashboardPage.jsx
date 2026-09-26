import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { ApiErrorAlert } from '../../components/ui/ApiErrorAlert';
import { useAuth } from '../../contexts/AuthContext';
import { useSessionGuard } from '../../hooks/useSessionGuard';
import { getInventory, getProducts } from '../../services/catalogApi';
import { getOrders, OrderStatusName } from '../../services/orderService';
import { formatCurrency, formatDate } from '../../utils/format';

const ACTIONS = [
  { label: 'Add a product', detail: 'Grow the collection', to: '/products' },
  { label: 'Manage stock', detail: 'Keep sizes available', to: '/inventory' },
  { label: 'Create promotion', detail: 'Give a collection a moment', to: '/marketing/promotions/new' },
  { label: 'Review orders', detail: 'Keep deliveries moving', to: '/orders' },
];

function totalFrom(response, key = 'totalItems') {
  if (Array.isArray(response)) return response.length;
  return response?.[key] ?? response?.items?.length ?? 0;
}

export default function DashboardPage() {
  const { token, user } = useAuth();
  const guardSessionExpiry = useSessionGuard();
  const [data, setData] = useState(null);
  const [error, setError] = useState(null);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [productResult, inventoryResult, orderResult] = await Promise.allSettled([
        getProducts({ page: 1, pageSize: 1 }, { token }),
        getInventory(undefined, { token }),
        getOrders(token, { page: 1, pageSize: 5, sortBy: 'createdAt', sortDirection: 'desc' }),
      ]);
      const failedResult = [productResult, inventoryResult, orderResult].find((result) => result.status === 'rejected');
      if (failedResult && guardSessionExpiry(failedResult.reason)) return;
      const products = productResult.status === 'fulfilled' ? productResult.value : null;
      const inventory = inventoryResult.status === 'fulfilled' ? inventoryResult.value : null;
      const orders = orderResult.status === 'fulfilled' ? orderResult.value : null;
      const inventoryItems = Array.isArray(inventory) ? inventory : inventory?.items ?? [];
      setData({
        productCount: products ? totalFrom(products) : null,
        inventoryCount: inventory ? totalFrom(inventory) : null,
        lowStockCount: inventory ? inventoryItems.filter((item) => item.isLowStock).length : null,
        orderCount: orders ? totalFrom(orders, 'totalCount') : null,
        orders: orders?.items ?? null,
      });
      if (failedResult) setError('Some store figures could not be loaded. Try refreshing the overview.');
    } catch (err) {
      if (!guardSessionExpiry(err)) setError(err?.message || 'The workspace overview could not be loaded.');
    } finally {
      setLoading(false);
    }
  }, [token, guardSessionExpiry]);

  useEffect(() => {
    // Load a fresh overview when the signed-in session changes.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    load();
  }, [load]);

  const greeting = user?.email?.split('@')[0] || 'there';

  return (
    <div className="workspace-dashboard">
      <div className="workspace-hero">
        <div>
          <p className="eyebrow">YOUR WORKSPACE</p>
          <h2>Good to see you, {greeting}.</h2>
          <p>Everything happening across your store, in one place.</p>
          <div className="workspace-hero__actions">
            <Link className="button" to="/orders">View orders <span aria-hidden="true">↗</span></Link>
            <Link className="button button-secondary" to="/marketing/dashboard">Explore insights</Link>
          </div>
        </div>
        <div className="workspace-hero__art" aria-hidden="true"><span>C</span><i /><b /></div>
      </div>

      <div className="workspace-section-heading"><div><p className="eyebrow">AT A GLANCE</p><h2>Store overview</h2></div><button className="button button-secondary button-small" onClick={load} type="button">Refresh</button></div>
      <ApiErrorAlert message={error} onRetry={load} />
      <div className="workspace-metrics" aria-busy={loading}>
        {[
          { label: 'Products', value: data?.productCount, to: '/products', symbol: '01' },
          { label: 'Orders', value: data?.orderCount, to: '/orders', symbol: '02' },
          { label: 'Stock records', value: data?.inventoryCount, to: '/inventory', symbol: '03' },
          { label: 'Low stock', value: data?.lowStockCount, to: '/inventory/low-stock', symbol: '04' },
        ].map((metric) => (
          <Link className="workspace-metric" key={metric.label} to={metric.to}>
            <span>{metric.label}</span><small>{metric.symbol}</small>
            <strong>{loading ? '—' : metric.value ?? '—'}</strong>
            <span className="workspace-metric__arrow" aria-hidden="true">↗</span>
          </Link>
        ))}
      </div>

      <div className="workspace-columns">
        <section className="workspace-panel" aria-labelledby="recent-orders-title">
          <div className="workspace-panel__heading"><div><p className="eyebrow">FULFILMENT</p><h2 id="recent-orders-title">Recent orders</h2></div><Link to="/orders">View all ↗</Link></div>
          {loading ? <p>Loading recent orders...</p> : data?.orders?.length ? (
            <div className="workspace-order-list">
              {data.orders.map((order) => <Link key={order.id} to={`/orders/${order.id}`}>
                <span><strong>{order.orderNumber}</strong><small>{formatDate(order.placedAt)}</small></span>
                <span><strong>{formatCurrency(order.total, order.currency || 'LKR')}</strong><small>{OrderStatusName[order.status] || 'Order'}</small></span>
                <span aria-hidden="true">↗</span>
              </Link>)}
            </div>
          ) : <p className="workspace-empty">{data?.orders ? 'New orders will appear here as soon as customers check out.' : 'Recent orders are unavailable right now.'}</p>}
        </section>
        <section className="workspace-panel workspace-panel--actions" aria-labelledby="quick-actions-title">
          <div className="workspace-panel__heading"><div><p className="eyebrow">SHORTCUTS</p><h2 id="quick-actions-title">Quick actions</h2></div></div>
          <div className="workspace-action-list">
            {ACTIONS.map((action) => <Link key={action.label} to={action.to}><span><strong>{action.label}</strong><small>{action.detail}</small></span><span aria-hidden="true">↗</span></Link>)}
          </div>
        </section>
      </div>
    </div>
  );
}
