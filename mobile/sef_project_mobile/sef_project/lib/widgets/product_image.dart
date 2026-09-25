import 'package:flutter/material.dart';

import '../models/shopping_models.dart';

class ProductImage extends StatelessWidget {
  const ProductImage({super.key, required this.product});
  final Product product;

  @override
  Widget build(BuildContext context) => Container(
    color: Theme.of(context).colorScheme.primaryContainer,
    child: product.images.isEmpty
      ? const Center(child: Icon(Icons.image_outlined, size: 54))
      : Image.network(
          product.images.first,
          fit: BoxFit.cover,
          errorBuilder: (_, _, _) => const Center(
            child: Icon(Icons.broken_image_outlined, size: 54),
          ),
        ),
  );
}
