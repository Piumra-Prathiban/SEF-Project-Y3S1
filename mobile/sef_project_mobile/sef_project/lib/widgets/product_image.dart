import 'package:flutter/material.dart';

import '../config/api_config.dart';
import '../models/shopping_models.dart';

class ProductImage extends StatelessWidget {
  const ProductImage({super.key, required this.product});
  final Product product;

  String? get _imageUrl {
    final configuredUrl = product.imageUrl?.trim();
    final legacyUrl = product.images.isEmpty
        ? null
        : product.images.first.trim();
    final value = configuredUrl?.isNotEmpty == true ? configuredUrl : legacyUrl;
    if (value == null || value.isEmpty) return null;

    final uri = Uri.tryParse(value);
    if (uri != null && uri.hasScheme) return uri.toString();
    return Uri.parse(apiBaseUrl).resolve(value).toString();
  }

  @override
  Widget build(BuildContext context) {
    final imageUrl = _imageUrl;
    if (imageUrl == null) return const _ProductImageFallback();

    return Image.network(
      imageUrl,
      fit: BoxFit.cover,
      errorBuilder: (_, _, _) =>
          const _ProductImageFallback(icon: Icons.broken_image_outlined),
    );
  }
}

class _ProductImageFallback extends StatelessWidget {
  const _ProductImageFallback({this.icon = Icons.image_outlined});

  final IconData icon;

  @override
  Widget build(BuildContext context) => DecoratedBox(
    decoration: BoxDecoration(
      gradient: LinearGradient(
        begin: Alignment.topLeft,
        end: Alignment.bottomRight,
        colors: [
          Theme.of(context).colorScheme.primaryContainer,
          Theme.of(context).colorScheme.secondaryContainer,
        ],
      ),
    ),
    child: Center(child: Icon(icon, size: 54)),
  );
}
