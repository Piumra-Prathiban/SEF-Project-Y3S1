import { PlaceholderPage } from '../components/ui/PlaceholderPage';
import { CategoriesPage } from '../features/categories';
import { CollectionsPage } from '../features/collections';
import { ColoursPage } from '../features/colours';
import { InventoryDashboardPage, LowStockPage } from '../features/inventory';
import { inventoryAgentFeature } from '../features/inventory-agent';
import { ProductsPage } from '../features/products';
import { SizesPage } from '../features/sizes';
import { VariantsPage } from '../features/variants';
import { STAFF_ROLES } from '../utils/roles';

function createPlaceholder(feature) {
  return (
    <PlaceholderPage
      area={feature.area}
      description={feature.description}
    />
  );
}

export const memberOneRoutes = [
  {
    path: '/products',
    label: 'Products',
    element: <ProductsPage />,
    showInNavigation: true,
  },
  {
    path: '/categories',
    label: 'Categories',
    element: <CategoriesPage />,
    roles: STAFF_ROLES,
    showInNavigation: true,
  },
  {
    path: '/collections',
    label: 'Collections',
    element: <CollectionsPage />,
    roles: STAFF_ROLES,
    showInNavigation: true,
  },
  {
    path: '/sizes',
    label: 'Sizes',
    element: <SizesPage />,
    roles: STAFF_ROLES,
    showInNavigation: true,
  },
  {
    path: '/colours',
    label: 'Colours',
    element: <ColoursPage />,
    roles: STAFF_ROLES,
    showInNavigation: true,
  },
  {
    path: '/variants',
    label: 'Variants',
    element: <VariantsPage />,
    roles: STAFF_ROLES,
    showInNavigation: true,
  },
  {
    path: '/inventory',
    label: 'Inventory',
    element: <InventoryDashboardPage />,
    roles: STAFF_ROLES,
    showInNavigation: true,
  },
  {
    path: '/inventory/history',
    label: 'Stock History',
    element: createPlaceholder({
      area: 'Stock History',
      description: 'Transaction history for inventory stock movements.',
    }),
    showInNavigation: true,
  },
  {
    path: '/inventory/low-stock',
    label: 'Low Stock',
    element: <LowStockPage />,
    roles: STAFF_ROLES,
    showInNavigation: true,
  },
  {
    path: '/inventory-agent',
    label: 'Inventory Agent',
    element: createPlaceholder(inventoryAgentFeature),
    roles: STAFF_ROLES,
    showInNavigation: true,
  },
];
