import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sef_project/screens/product_detail_screen.dart';
import 'package:sef_project/state/customer_store.dart';
import 'package:sef_project/widgets/product_image.dart';

import '../fake_customer_repository.dart';
import '../support/fixtures.dart';

Widget _screen(Widget child) => StoreScope(
  store: CustomerStore(FakeCustomerRepository()),
  child: MaterialApp(home: child),
);

void main() {
  testWidgets('ProductImage renders imageUrl when supplied', (tester) async {
    await tester.pumpWidget(
      MaterialApp(
        home: SizedBox(
          width: 300,
          height: 300,
          child: ProductImage(product: shoppingProductWithImage),
        ),
      ),
    );

    final image = tester.widget<Image>(find.byType(Image));
    expect(
      (image.image as NetworkImage).url,
      'https://example.com/images/linen-shirt.jpg',
    );
    expect(find.byIcon(Icons.image_outlined), findsNothing);
  });

  testWidgets('ProductImage keeps the fallback when imageUrl is null', (
    tester,
  ) async {
    await tester.pumpWidget(
      MaterialApp(
        home: SizedBox(
          width: 300,
          height: 300,
          child: ProductImage(product: shoppingProductWithoutImage),
        ),
      ),
    );

    expect(find.byType(Image), findsNothing);
    expect(find.byIcon(Icons.image_outlined), findsOneWidget);
  });

  testWidgets('product detail shows variant size and colour selection', (
    tester,
  ) async {
    await tester.binding.setSurfaceSize(const Size(800, 1000));
    addTearDown(() => tester.binding.setSurfaceSize(null));

    await tester.pumpWidget(
      _screen(ProductDetailScreen(product: shoppingProductWithoutImage)),
    );

    expect(find.widgetWithText(ChoiceChip, 'M / Blue'), findsOneWidget);
    expect(find.widgetWithText(ChoiceChip, 'L / Green'), findsOneWidget);
    expect(find.text('Size: M'), findsOneWidget);
    expect(find.text('Colour: Blue'), findsOneWidget);
    expect(find.text('5 available'), findsOneWidget);

    await tester.tap(find.widgetWithText(ChoiceChip, 'L / Green'));
    await tester.pump();

    expect(find.text('Size: L'), findsOneWidget);
    expect(find.text('Colour: Green'), findsOneWidget);
    expect(find.text('3 available'), findsOneWidget);
  });
}
