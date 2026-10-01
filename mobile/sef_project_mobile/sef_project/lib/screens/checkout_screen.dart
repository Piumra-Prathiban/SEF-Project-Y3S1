import 'package:flutter/material.dart';

import '../state/customer_store.dart';
import '../utils/formatters.dart';

class CheckoutScreen extends StatefulWidget {
  const CheckoutScreen({super.key});

  @override
  State<CheckoutScreen> createState() => _CheckoutScreenState();
}

class _CheckoutScreenState extends State<CheckoutScreen> {
  final _formKey = GlobalKey<FormState>();
  final _fullName = TextEditingController();
  final _line1 = TextEditingController();
  final _line2 = TextEditingController();
  final _city = TextEditingController();
  final _province = TextEditingController();
  final _postalCode = TextEditingController();
  final _country = TextEditingController(text: 'Sri Lanka');
  final _phone = TextEditingController();
  final _couponCode = TextEditingController();
  bool _initialized = false;
  bool _submitting = false;
  int _paymentMethod = 0;
  String? _error;

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    if (_initialized) return;
    _initialized = true;
    WidgetsBinding.instance.addPostFrameCallback((_) => _loadDefaults());
  }

  Future<void> _loadDefaults() async {
    final store = StoreScope.of(context);
    try {
      if (store.profile == null || store.addresses.isEmpty) {
        await store.loadProfile();
      }
      if (!mounted) return;
      final profile = store.profile;
      if (profile != null) {
        _fullName.text = '${profile.firstName} ${profile.lastName}'.trim();
      }
      if (store.addresses.isNotEmpty) {
        final address = store.addresses.firstWhere(
          (item) => item.isDefault,
          orElse: () => store.addresses.first,
        );
        _line1.text = address.addressLine1;
        _line2.text = address.addressLine2 ?? '';
        _city.text = address.city;
        _province.text = address.province ?? '';
        _postalCode.text = address.postalCode;
        _country.text = address.country;
      }
      setState(() {});
    } catch (exception) {
      if (mounted) setState(() => _error = exception.toString());
    }
  }

  @override
  void dispose() {
    for (final controller in [
      _fullName,
      _line1,
      _line2,
      _city,
      _province,
      _postalCode,
      _country,
      _phone,
      _couponCode,
    ]) {
      controller.dispose();
    }
    super.dispose();
  }

  String? _required(String? value) =>
      value == null || value.trim().isEmpty ? 'This field is required.' : null;

  Future<void> _submit() async {
    if (_formKey.currentState?.validate() != true) return;
    setState(() {
      _submitting = true;
      _error = null;
    });
    try {
      final order = await StoreScope.of(context).checkout(
        deliveryAddress: {
          'fullName': _fullName.text.trim(),
          'line1': _line1.text.trim(),
          'line2': _line2.text.trim().isEmpty ? null : _line2.text.trim(),
          'city': _city.text.trim(),
          'province': _province.text.trim().isEmpty
              ? null
              : _province.text.trim(),
          'postalCode': _postalCode.text.trim(),
          'country': _country.text.trim(),
          'phone': _phone.text.trim().isEmpty ? null : _phone.text.trim(),
        },
        paymentMethod: _paymentMethod,
        couponCode: _couponCode.text.trim().isEmpty
            ? null
            : _couponCode.text.trim().toUpperCase(),
      );
      if (!mounted) return;
      await showDialog<void>(
        context: context,
        barrierDismissible: false,
        builder: (dialogContext) => AlertDialog(
          icon: const Icon(Icons.check_circle_outline, size: 44),
          title: const Text('Order placed'),
          content: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                '${order.orderNumber} has been created. Your payment is pending staff confirmation.',
              ),
              if (order.hasDiscount) ...[
                const SizedBox(height: 12),
                Text(
                  order.couponCode == null
                      ? 'You saved ${formatCurrency(order.discountTotal, order.currency)}.'
                      : 'Coupon ${order.couponCode} saved you '
                            '${formatCurrency(order.discountTotal, order.currency)}.',
                  style: const TextStyle(fontWeight: FontWeight.w600),
                ),
              ] else if (order.couponCode != null) ...[
                const SizedBox(height: 12),
                Text('Coupon ${order.couponCode} applied.'),
              ],
            ],
          ),
          actions: [
            FilledButton(
              onPressed: () => Navigator.pop(dialogContext),
              child: const Text('View my orders'),
            ),
          ],
        ),
      );
      if (mounted) Navigator.pop(context, true);
    } catch (exception) {
      if (mounted) setState(() => _error = exception.toString());
    } finally {
      if (mounted) setState(() => _submitting = false);
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Checkout')),
    body: Form(
      key: _formKey,
      child: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          Text(
            'Delivery details',
            style: Theme.of(context).textTheme.titleLarge,
          ),
          const SizedBox(height: 12),
          TextFormField(
            controller: _fullName,
            decoration: const InputDecoration(labelText: 'Full name'),
            validator: _required,
          ),
          const SizedBox(height: 10),
          TextFormField(
            controller: _line1,
            decoration: const InputDecoration(labelText: 'Address line 1'),
            validator: _required,
          ),
          const SizedBox(height: 10),
          TextFormField(
            controller: _line2,
            decoration: const InputDecoration(
              labelText: 'Address line 2 (optional)',
            ),
          ),
          const SizedBox(height: 10),
          TextFormField(
            controller: _city,
            decoration: const InputDecoration(labelText: 'City'),
            validator: _required,
          ),
          const SizedBox(height: 10),
          TextFormField(
            controller: _province,
            decoration: const InputDecoration(labelText: 'Province (optional)'),
          ),
          const SizedBox(height: 10),
          TextFormField(
            controller: _postalCode,
            decoration: const InputDecoration(labelText: 'Postal code'),
            validator: _required,
          ),
          const SizedBox(height: 10),
          TextFormField(
            controller: _country,
            decoration: const InputDecoration(labelText: 'Country'),
            validator: _required,
          ),
          const SizedBox(height: 10),
          TextFormField(
            controller: _phone,
            keyboardType: TextInputType.phone,
            decoration: const InputDecoration(labelText: 'Phone (optional)'),
          ),
          const SizedBox(height: 24),
          Text('Offers', style: Theme.of(context).textTheme.titleLarge),
          const SizedBox(height: 8),
          TextFormField(
            key: const Key('checkout-coupon-code'),
            controller: _couponCode,
            textCapitalization: TextCapitalization.characters,
            decoration: const InputDecoration(
              labelText: 'Coupon code (optional)',
              prefixIcon: Icon(Icons.local_offer_outlined),
            ),
          ),
          const SizedBox(height: 24),
          Text('Payment method', style: Theme.of(context).textTheme.titleLarge),
          const SizedBox(height: 8),
          DropdownButtonFormField<int>(
            initialValue: _paymentMethod,
            decoration: const InputDecoration(labelText: 'Method'),
            items: const [
              DropdownMenuItem(value: 0, child: Text('Card')),
              DropdownMenuItem(value: 1, child: Text('Cash')),
              DropdownMenuItem(value: 2, child: Text('Online transfer')),
            ],
            onChanged: (value) => setState(() => _paymentMethod = value ?? 0),
          ),
          const SizedBox(height: 8),
          const Text(
            'No card number or security code is stored. The payment starts as Pending.',
          ),
          if (_error != null) ...[
            const SizedBox(height: 12),
            Text(
              _error!,
              style: TextStyle(color: Theme.of(context).colorScheme.error),
            ),
          ],
          const SizedBox(height: 20),
          FilledButton.icon(
            onPressed: _submitting ? null : _submit,
            icon: _submitting
                ? const SizedBox.square(
                    dimension: 18,
                    child: CircularProgressIndicator(strokeWidth: 2),
                  )
                : const Icon(Icons.lock_outline),
            label: Text(_submitting ? 'Placing order…' : 'Place order'),
          ),
        ],
      ),
    ),
  );
}
