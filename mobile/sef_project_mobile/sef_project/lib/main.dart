import 'package:flutter/material.dart';

import 'core/api/api_client.dart';
import 'core/config/app_config.dart';
import 'features/promotions/data/promotion_repository.dart';
import 'features/promotions/screens/promotions_screen.dart';

void main() {
  final client = ApiClient(baseUri: AppConfig.apiBaseUri);
  runApp(SefApp(promotionRepository: ApiPromotionRepository(client)));
}

class SefApp extends StatelessWidget {
  const SefApp({super.key, required this.promotionRepository});

  final PromotionRepository promotionRepository;

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'SEF Project',
      theme: ThemeData(
        colorScheme: ColorScheme.fromSeed(seedColor: const Color(0xFF2A78D6)),
      ),
      darkTheme: ThemeData(
        colorScheme: ColorScheme.fromSeed(
          seedColor: const Color(0xFF2A78D6),
          brightness: Brightness.dark,
        ),
      ),
      home: PromotionsScreen(repository: promotionRepository),
    );
  }
}
