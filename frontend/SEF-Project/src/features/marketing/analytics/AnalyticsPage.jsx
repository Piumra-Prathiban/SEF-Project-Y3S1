import { useState } from 'react';
import PageHeader from '../../../components/PageHeader';
import { useAuth } from '../../../contexts/AuthContext';
import DateRangeFilter from './DateRangeFilter';
import DemandSection from './sections/DemandSection';
import InventorySection from './sections/InventorySection';
import ProductPerformanceSection from './sections/ProductPerformanceSection';
import PromotionPerformanceSection from './sections/PromotionPerformanceSection';
import RevenueTrendSection from './sections/RevenueTrendSection';
import { useDateRange } from './useDateRange';

export default function AnalyticsPage() {
  const { token } = useAuth();
  const { selection, apiRange, setSelection } = useDateRange();
  const [refreshKey, setRefreshKey] = useState(0);
  const sectionProps = { token, range: apiRange, refreshKey };

  return (
    <>
      <PageHeader
        title="Analytics"
        subtitle="Sales, products, inventory, promotions and demand. All figures are calculated by the server."
        actions={
          <>
            <DateRangeFilter
              selection={selection}
              apiRange={apiRange}
              onChange={setSelection}
              onRefresh={() => setRefreshKey((key) => key + 1)}
            />
            <button type="button" className="button button-secondary" onClick={() => window.print()}>
              Print
            </button>
          </>
        }
      />

      <div className="dashboard-stack">
        <RevenueTrendSection {...sectionProps} />
        <div className="dashboard-grid">
          <ProductPerformanceSection {...sectionProps} mode="top" />
          <ProductPerformanceSection {...sectionProps} mode="low" />
        </div>
        <InventorySection token={token} refreshKey={refreshKey} />
        <PromotionPerformanceSection {...sectionProps} />
        <DemandSection {...sectionProps} />
      </div>
    </>
  );
}
