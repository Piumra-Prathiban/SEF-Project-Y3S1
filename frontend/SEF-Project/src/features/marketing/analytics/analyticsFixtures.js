// Shared API response fixtures for analytics tests (shapes match the API DTOs).

export const summaryCurrent = {
  from: '2026-10-01T00:00:00Z',
  to: '2026-10-11T00:00:00Z',
  orderCount: 2,
  unitsSold: 4,
  grossSales: 5300,
  discountTotal: 0,
  netRevenue: 5300,
  averageOrderValue: 2650,
  currency: 'LKR',
  includedStatuses: [1, 2, 3, 4],
};

export const summaryPrevious = {
  ...summaryCurrent,
  from: '2026-09-21T00:00:00Z',
  to: '2026-10-01T00:00:00Z',
  orderCount: 1,
  unitsSold: 4,
  grossSales: 4800,
  netRevenue: 4800,
  averageOrderValue: 4800,
};

export const summaryEmpty = {
  ...summaryCurrent,
  orderCount: 0,
  unitsSold: 0,
  grossSales: 0,
  netRevenue: 0,
  averageOrderValue: 0,
};

export const overTime = {
  from: '2026-10-01T00:00:00Z',
  to: '2026-10-04T00:00:00Z',
  granularity: 0,
  points: [
    { periodStart: '2026-10-01T00:00:00Z', orderCount: 0, unitsSold: 0, netRevenue: 0 },
    { periodStart: '2026-10-02T00:00:00Z', orderCount: 1, unitsSold: 3, netRevenue: 2700 },
    { periodStart: '2026-10-03T00:00:00Z', orderCount: 1, unitsSold: 1, netRevenue: 2600 },
  ],
};

export const overTimeEmpty = {
  ...overTime,
  points: overTime.points.map((p) => ({ ...p, orderCount: 0, unitsSold: 0, netRevenue: 0 })),
};

function product(id, name, unitsSold, revenue) {
  return { productId: id, productName: name, isActive: true, unitsSold, orderCount: unitsSold ? 1 : 0, revenue };
}

export const topProducts = {
  items: [
    product('p1', 'Pepperoni Pizza', 1, 2600),
    product('p2', 'Margherita Pizza', 2, 2400),
    product('p3', 'Cola', 1, 300),
  ],
  totalCount: 5, page: 1, pageSize: 10,
};

export const lowProducts = {
  items: [
    product('p4', 'Spaghetti Carbonara', 0, 0),
    product('p5', 'Tiramisu', 0, 0),
    product('p3', 'Cola', 1, 300),
  ],
  totalCount: 5, page: 1, pageSize: 10,
};

export const noSalesProducts = {
  items: [product('p4', 'Spaghetti Carbonara', 0, 0)],
  totalCount: 1, page: 1, pageSize: 10,
};

export const inventorySummary = {
  variantCount: 7,
  inStockCount: 5,
  lowStockCount: 1,
  outOfStockCount: 1,
  totalQuantityOnHand: 384,
  totalReservedQuantity: 200,
};

function stockItem(id, name, sku, available, reorderLevel, stockStatus) {
  return {
    productVariantId: id,
    productId: `prod-${id}`,
    productName: name,
    sku,
    variantName: 'Default',
    isActive: true,
    hasInventoryRecord: true,
    quantityOnHand: available,
    reservedQuantity: 0,
    availableQuantity: available,
    reorderLevel,
    stockStatus,
  };
}

export const outOfStockPage = {
  items: [stockItem('v1', 'Cola', 'BEV-COLA-330', 0, 50, 2)],
  totalCount: 1, page: 1, pageSize: 10,
};

export const lowStockPage = {
  items: [stockItem('v2', 'Tiramisu', 'DES-TIRA-S', 4, 5, 1)],
  totalCount: 1, page: 1, pageSize: 10,
};

export const emptyStockPage = { items: [], totalCount: 0, page: 1, pageSize: 10 };

export const promotionPerformance = {
  from: '2026-10-01T00:00:00Z',
  to: '2026-10-11T00:00:00Z',
  livePromotionCount: 3,
  totalRedemptions: 2,
  items: [
    {
      promotionId: 'promo-1',
      name: 'Pizza 20% Off',
      type: 0,
      discountValue: 20,
      campaignName: 'Summer Launch',
      isLive: true,
      couponCount: 1,
      redemptions: 2,
      uniqueCustomers: 1,
      redeemedOrderCount: 1,
      redeemedOrderRevenue: 1920,
      discountAmount: 480,
    },
  ],
  totalCount: 1, page: 1, pageSize: 20,
};

export const demand = {
  items: [
    {
      productVariantId: 'v1', productId: 'prod-v1', productName: 'Cola', sku: 'BEV-COLA-330',
      unitsSold: 3, previousUnitsSold: 1, unitsPerDay: 0.3, trendPercent: 200, trend: 2,
      availableQuantity: 200, daysOfCover: 666.7,
    },
    {
      productVariantId: 'v3', productId: 'prod-v3', productName: 'Margherita Pizza', sku: 'PIZ-MARG-S',
      unitsSold: 2, previousUnitsSold: 4, unitsPerDay: 0.2, trendPercent: -50, trend: 4,
      availableQuantity: 50, daysOfCover: 250,
    },
    {
      productVariantId: 'v5', productId: 'prod-v5', productName: 'Tiramisu', sku: 'DES-TIRA-S',
      unitsSold: 0, previousUnitsSold: 0, unitsPerDay: 0, trendPercent: null, trend: 0,
      availableQuantity: 4, daysOfCover: null,
    },
  ],
  totalCount: 7, page: 1, pageSize: 10,
};

// Intl currency output uses a no-break space; DOM text matchers normalise it to ' '.
export const text = (value) => String(value).replace(/\s/g, ' ');
