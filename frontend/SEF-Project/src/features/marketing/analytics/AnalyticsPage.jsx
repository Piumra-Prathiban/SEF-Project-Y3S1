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
      />

      <DateRangeFilter
        selection={selection}
        apiRange={apiRange}
        onChange={setSelection}
        onRefresh={() => setRefreshKey((key) => key + 1)}
      />

      <nav className="section-jump" aria-label="Jump to section">
        <a href="#revenue-trend">Revenue trend</a>
        <a href="#top-products">Top sellers</a>
        <a href="#low-products">Low performers</a>
        <a href="#inventory-insights">Inventory</a>
        <a href="#promotion-performance">Promotions</a>
        <a href="#demand-insights">Demand</a>
      </nav>

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
