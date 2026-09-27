import 'package:flutter/material.dart';

import '../config/api_config.dart';
import '../core/api/api_client.dart';
import '../features/promotions/data/promotion_repository.dart';
import 'cart_screen.dart';
import 'products_screen.dart';
import 'profile_screen.dart';
import 'recommendations_screen.dart';
import 'wishlist_screen.dart';
import 'customer_orders_screen.dart';

class HomeShell extends StatefulWidget {
  const HomeShell({super.key});
  @override
  State<HomeShell> createState() => _HomeShellState();
}

class _HomeShellState extends State<HomeShell> {
  int index = 0;
  final Set<int> builtPages = {0};
  late final ApiClient _promotionClient;
  late final PromotionRepository _promotionRepository;
  late final List<Widget> pages;

  @override
  void initState() {
    super.initState();
    _promotionClient = ApiClient(baseUri: Uri.parse(apiBaseUrl));
    _promotionRepository = ApiPromotionRepository(_promotionClient);
    pages = [
      ProductsScreen(promotionRepository: _promotionRepository),
      const RecommendationsScreen(),
      const WishlistScreen(),
      const CartScreen(),
      const CustomerOrdersScreen(),
      const ProfileScreen(),
    ];
  }

  @override
  void dispose() {
    _promotionClient.close();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    body: IndexedStack(
      index: index,
      children: List.generate(
        pages.length,
        (pageIndex) => builtPages.contains(pageIndex)
            ? pages[pageIndex]
            : const SizedBox.shrink(),
      ),
    ),
    bottomNavigationBar: NavigationBar(
      selectedIndex: index,
      onDestinationSelected: (value) => setState(() {
        index = value;
        builtPages.add(value);
      }),
      destinations: const [
        NavigationDestination(
          icon: Icon(Icons.storefront_outlined),
          selectedIcon: Icon(Icons.storefront),
          label: 'Shop',
        ),
        NavigationDestination(
          icon: Icon(Icons.auto_awesome_outlined),
          selectedIcon: Icon(Icons.auto_awesome),
          label: 'Stylist',
        ),
        NavigationDestination(
          icon: Icon(Icons.favorite_border),
          selectedIcon: Icon(Icons.favorite),
          label: 'Wishlist',
        ),
        NavigationDestination(
          icon: Icon(Icons.shopping_cart_outlined),
          selectedIcon: Icon(Icons.shopping_cart),
          label: 'Cart',
        ),
        NavigationDestination(
          icon: Icon(Icons.receipt_long_outlined),
          selectedIcon: Icon(Icons.receipt_long),
          label: 'Orders',
        ),
        NavigationDestination(
          icon: Icon(Icons.person_outline),
          selectedIcon: Icon(Icons.person),
          label: 'Profile',
        ),
      ],
    ),
  );
}
