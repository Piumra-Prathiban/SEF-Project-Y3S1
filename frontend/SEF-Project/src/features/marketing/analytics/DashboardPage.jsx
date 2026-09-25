import { useState } from 'react';
import { Link } from 'react-router-dom';
import PageHeader from '../../../components/PageHeader';
import { useAuth } from '../../../contexts/AuthContext';
import DateRangeFilter from './DateRangeFilter';
import KpiSection from './KpiSection';
import ProductPerformanceSection from './sections/ProductPerformanceSection';
import RevenueTrendSection from './sections/RevenueTrendSection';
import { useDateRange } from './useDateRange';

export default function DashboardPage() {
  const { token } = useAuth();
  const { selection, apiRange, setSelection } = useDateRange();
  const [refreshKey, setRefreshKey] = useState(0);

  return (
    <>
      <PageHeader
        title="Dashboard"
        subtitle="Sales performance at a glance."
        actions={
          <>
            <Link className="button button-secondary" to="/marketing/analytics">Full analytics</Link>
            <Link className="button button-secondary" to="/marketing/reports">Reports</Link>
          </>
        }
      />

      <DateRangeFilter
        selection={selection}
        apiRange={apiRange}
        onChange={setSelection}
        onRefresh={() => setRefreshKey((key) => key + 1)}
      />

      <div className="dashboard-stack">
        <KpiSection token={token} range={apiRange} refreshKey={refreshKey} />
        <div className="dashboard-grid">
          <RevenueTrendSection token={token} range={apiRange} refreshKey={refreshKey} />
          <ProductPerformanceSection token={token} range={apiRange} refreshKey={refreshKey} mode="top" limit={5} />
        </div>
      </div>
    </>
  );
}
