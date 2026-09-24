import 'package:flutter_test/flutter_test.dart';
import 'package:sef_project/main.dart';

import 'support/fixtures.dart';

void main() {
  testWidgets('app starts on the promotions screen', (tester) async {
    final repository = FakePromotionRepository();

    await tester.pumpWidget(SefApp(promotionRepository: repository));
    await tester.pumpAndSettle();

    expect(find.text('Promotions'), findsOneWidget);
    expect(find.text('Pizza 20% Off'), findsOneWidget);
    expect(repository.activeCalls, 1);
  });
}
