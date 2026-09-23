import { PlaceholderPage } from '../components/ui/PlaceholderPage';
import { categoriesFeature } from '../features/categories';
import { collectionsFeature } from '../features/collections';
import { coloursFeature } from '../features/colours';
import { inventoryFeature } from '../features/inventory';
import { inventoryAgentFeature } from '../features/inventory-agent';
import { productsFeature } from '../features/products';
import { sizesFeature } from '../features/sizes';
import { variantsFeature } from '../features/variants';
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
    element: createPlaceholder(productsFeature),
    showInNavigation: true,
  },
  {
    path: '/categories',
    label: 'Categories',
    element: createPlaceholder(categoriesFeature),
    roles: STAFF_ROLES,
    showInNavigation: true,
  },
  {
    path: '/collections',
    label: 'Collections',
    element: createPlaceholder(collectionsFeature),
    roles: STAFF_ROLES,
    showInNavigation: true,
  },
  {
    path: '/sizes',
    label: 'Sizes',
    element: createPlaceholder(sizesFeature),
    roles: STAFF_ROLES,
    showInNavigation: true,
  },
  {
    path: '/colours',
    label: 'Colours',
    element: createPlaceholder(coloursFeature),
    roles: STAFF_ROLES,
    showInNavigation: true,
  },
  {
    path: '/variants',
    label: 'Variants',
    element: createPlaceholder(variantsFeature),
    roles: STAFF_ROLES,
    showInNavigation: true,
  },
  {
    path: '/inventory',
    label: 'Inventory',
    element: createPlaceholder(inventoryFeature),
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
    element: createPlaceholder({
      area: 'Low Stock',
      description: 'Variants where quantity on hand is at or below reorder level.',
    }),
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
