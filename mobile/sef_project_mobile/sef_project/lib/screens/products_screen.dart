import 'package:flutter/material.dart';

import '../models/shopping_models.dart';
import '../state/customer_store.dart';
import '../widgets/common.dart';
import '../widgets/product_image.dart';
import 'product_detail_screen.dart';

class ProductsScreen extends StatefulWidget {
  const ProductsScreen({super.key});
  @override
  State<ProductsScreen> createState() => _ProductsScreenState();
}

class _ProductsScreenState extends State<ProductsScreen> {
  final _search = TextEditingController();
  bool _loaded = false;

  @override
  void dispose() { _search.dispose(); super.dispose(); }

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    if (!_loaded) {
      _loaded = true;
      WidgetsBinding.instance.addPostFrameCallback((_) {
        StoreScope.of(context).loadProducts().catchError((_) {});
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final store = StoreScope.of(context);
    return Scaffold(
      appBar: AppBar(title: const Text('Discover'), actions: [
        IconButton(tooltip: 'Filters and sorting', onPressed: () => _showFilters(context, store), icon: const Icon(Icons.tune)),
      ]),
      body: Column(children: [
        Padding(
          padding: const EdgeInsets.all(16),
          child: SearchBar(
            controller: _search,
            hintText: 'Search products or SKUs',
            leading: const Icon(Icons.search),
            trailing: [IconButton(tooltip: 'Search', onPressed: () => _runSearch(store), icon: const Icon(Icons.arrow_forward))],
            onSubmitted: (_) => _runSearch(store),
          ),
        ),
        Expanded(
          child: AsyncPanel(
            loading: store.loading && store.products.items.isEmpty,
            error: store.error,
            empty: store.products.items.isEmpty,
            emptyTitle: 'No products found',
            emptyMessage: 'Try a broader search or adjust your filters.',
            onRetry: store.loadProducts,
            child: LayoutBuilder(builder: (context, constraints) {
              final columns = constraints.maxWidth >= 900 ? 4 : constraints.maxWidth >= 600 ? 3 : 2;
              return RefreshIndicator(
                onRefresh: store.loadProducts,
                child: GridView.builder(
                  padding: const EdgeInsets.fromLTRB(16, 0, 16, 24),
                  gridDelegate: SliverGridDelegateWithFixedCrossAxisCount(crossAxisCount: columns, childAspectRatio: .64, crossAxisSpacing: 12, mainAxisSpacing: 12),
                  itemCount: store.products.items.length,
                  itemBuilder: (context, index) => ProductTile(product: store.products.items[index]),
                ),
              );
            }),
          ),
        ),
        if (store.products.totalPages > 1)
          SafeArea(
            top: false,
            child: Padding(
              padding: const EdgeInsets.all(8),
              child: Row(mainAxisAlignment: MainAxisAlignment.center, children: [
                IconButton(onPressed: store.products.page > 1 ? () => store.loadProducts(page: store.products.page - 1) : null, icon: const Icon(Icons.chevron_left)),
                Text('Page ${store.products.page} of ${store.products.totalPages}'),
                IconButton(onPressed: store.products.page < store.products.totalPages ? () => store.loadProducts(page: store.products.page + 1) : null, icon: const Icon(Icons.chevron_right)),
              ]),
            ),
          ),
      ]),
    );
  }

  Future<void> _runSearch(CustomerStore store) => runAction(
    context,
    () => store.applyFilters(
      searchText: _search.text,
      category: store.categoryId,
      minimum: store.minPrice,
      maximum: store.maxPrice,
      availableOnly: store.inStockOnly,
      orderBy: store.sortBy,
      direction: store.sortDirection,
    ),
  );

  Future<void> _showFilters(BuildContext context, CustomerStore store) async {
    final minimum = TextEditingController(text: store.minPrice?.toString() ?? '');
    final maximum = TextEditingController(text: store.maxPrice?.toString() ?? '');
    var inStock = store.inStockOnly;
    var category = store.categoryId ?? '';
    var sort = '${store.sortBy}:${store.sortDirection}';
    final categories = <String, Category>{};
    for (final product in store.products.items) {
      for (final item in product.categories) { categories[item.id] = item; }
    }
    await showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      builder: (sheetContext) => StatefulBuilder(
        builder: (context, setSheetState) => Padding(
          padding: EdgeInsets.fromLTRB(20, 20, 20, MediaQuery.viewInsetsOf(context).bottom + 20),
          child: SingleChildScrollView(
            child: Column(mainAxisSize: MainAxisSize.min, crossAxisAlignment: CrossAxisAlignment.stretch, children: [
              Text('Filter and sort', style: Theme.of(context).textTheme.headlineSmall),
              const SizedBox(height: 16),
              DropdownButtonFormField<String>(
                initialValue: category,
                decoration: const InputDecoration(labelText: 'Category'),
                items: [
                  const DropdownMenuItem<String>(value: '', child: Text('All categories')),
                  ...categories.values.map((item) => DropdownMenuItem<String>(value: item.id, child: Text(item.name))),
                ],
                onChanged: (value) {
                  if (value != null) {
                    setSheetState(() => category = value);
                  }
                },
              ),
              const SizedBox(height: 12),
              Row(children: [
                Expanded(child: TextField(key: const Key('product-min-price'), controller: minimum, keyboardType: TextInputType.number, decoration: const InputDecoration(labelText: 'Minimum price'))),
                const SizedBox(width: 12),
                Expanded(child: TextField(key: const Key('product-max-price'), controller: maximum, keyboardType: TextInputType.number, decoration: const InputDecoration(labelText: 'Maximum price'))),
              ]),
              const SizedBox(height: 12),
              DropdownButtonFormField<String>(
                initialValue: sort,
                decoration: const InputDecoration(labelText: 'Sort'),
                items: const [
                  DropdownMenuItem(value: 'name:asc', child: Text('Name A-Z')),
                  DropdownMenuItem(value: 'price:asc', child: Text('Price low to high')),
                  DropdownMenuItem(value: 'price:desc', child: Text('Price high to low')),
                  DropdownMenuItem(value: 'newest:desc', child: Text('Newest first')),
                ],
                onChanged: (value) => setSheetState(() => sort = value!),
              ),
              SwitchListTile(contentPadding: EdgeInsets.zero, title: const Text('In-stock only'), value: inStock, onChanged: (value) => setSheetState(() => inStock = value)),
              const Text('Size and colour filters will appear after those catalog fields are exposed by the product API.', style: TextStyle(fontSize: 12, color: Colors.black54)),
              const SizedBox(height: 12),
              FilledButton(
                onPressed: () async {
                  final minimumText = minimum.text.trim();
                  final maximumText = maximum.text.trim();
                  final minimumValue = minimumText.isEmpty ? null : double.tryParse(minimumText);
                  final maximumValue = maximumText.isEmpty ? null : double.tryParse(maximumText);
                  if (minimumText.isNotEmpty && minimumValue == null) {
                    ScaffoldMessenger.of(this.context).showSnackBar(
                      const SnackBar(content: Text('Enter a valid minimum price.')),
                    );
                    return;
                  }
                  if (maximumText.isNotEmpty && maximumValue == null) {
                    ScaffoldMessenger.of(this.context).showSnackBar(
                      const SnackBar(content: Text('Enter a valid maximum price.')),
                    );
                    return;
                  }
                  final parts = sort.split(':');
                  Navigator.pop(sheetContext);
                  await runAction(this.context, () => store.applyFilters(
                    searchText: _search.text,
                    category: category.isEmpty ? null : category,
                    minimum: minimumValue,
                    maximum: maximumValue,
                    availableOnly: inStock,
                    orderBy: parts[0],
                    direction: parts[1],
                  ));
                },
                child: const Text('Apply'),
              ),
            ]),
          ),
        ),
      ),
    );
    minimum.dispose();
    maximum.dispose();
  }
}

class ProductTile extends StatelessWidget {
  const ProductTile({super.key, required this.product});
  final Product product;

  @override
  Widget build(BuildContext context) => Card(
    clipBehavior: Clip.antiAlias,
    child: InkWell(
      onTap: () => Navigator.push<void>(context, MaterialPageRoute(builder: (_) => StoreScope(store: StoreScope.of(context), child: ProductDetailScreen(product: product)))),
      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Expanded(child: ProductImage(product: product)),
        Padding(
          padding: const EdgeInsets.all(12),
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Text(product.categories.map((item) => item.name).join(' / '), maxLines: 1, style: Theme.of(context).textTheme.labelSmall),
            const SizedBox(height: 5),
            Text(product.name, maxLines: 2, overflow: TextOverflow.ellipsis, style: Theme.of(context).textTheme.titleMedium),
            const SizedBox(height: 6),
            Text('From ${money(product.minimumPrice)}', style: const TextStyle(fontWeight: FontWeight.bold)),
            const SizedBox(height: 5),
            Text(product.isAvailable ? 'In stock' : 'Unavailable', style: TextStyle(color: product.isAvailable ? Colors.green.shade700 : Colors.red.shade700)),
          ]),
        ),
      ]),
    ),
  );
}
