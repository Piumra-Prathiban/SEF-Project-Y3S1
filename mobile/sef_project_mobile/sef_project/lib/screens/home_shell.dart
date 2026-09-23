import 'package:flutter/material.dart';
import 'cart_screen.dart';
import 'products_screen.dart';
import 'profile_screen.dart';
import 'wishlist_screen.dart';

class HomeShell extends StatefulWidget {
  const HomeShell({super.key});
  @override State<HomeShell> createState() => _HomeShellState();
}

class _HomeShellState extends State<HomeShell> {
  int index = 0;
  final Set<int> builtPages = {0};
  static const pages = [ProductsScreen(), WishlistScreen(), CartScreen(), ProfileScreen()];
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
        NavigationDestination(icon: Icon(Icons.storefront_outlined), selectedIcon: Icon(Icons.storefront), label: 'Shop'),
        NavigationDestination(icon: Icon(Icons.favorite_border), selectedIcon: Icon(Icons.favorite), label: 'Wishlist'),
        NavigationDestination(icon: Icon(Icons.shopping_cart_outlined), selectedIcon: Icon(Icons.shopping_cart), label: 'Cart'),
        NavigationDestination(icon: Icon(Icons.person_outline), selectedIcon: Icon(Icons.person), label: 'Profile'),
      ],
    ),
  );
}
