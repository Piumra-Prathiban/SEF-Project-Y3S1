import 'package:flutter/material.dart';

import '../models/order_enums.dart';

enum StatusKind { order, payment, shipment }

/// Mirrors the React `StatusBadge` component: a pill showing the status name
/// resolved from the shared integer wire values.
class StatusBadge extends StatelessWidget {
  const StatusBadge({super.key, required this.status, this.kind = StatusKind.order});

  final int status;
  final StatusKind kind;

  static const Map<StatusKind, Map<int, String>> _names = {
    StatusKind.order: orderStatusNames,
    StatusKind.payment: paymentStatusNames,
    StatusKind.shipment: shipmentStatusNames,
  };

  static const Map<String, Color> _colors = {
    'Pending': Color(0xFF6B7280),
    'Confirmed': Color(0xFF1D4ED8),
    'Preparing': Color(0xFF7C3AED),
    'Ready': Color(0xFF0F766E),
    'Completed': Color(0xFF15803D),
    'Cancelled': Color(0xFFB91C1C),
    'Failed': Color(0xFFB91C1C),
    'Refunded': Color(0xFF4B5563),
    'Shipped': Color(0xFF1D4ED8),
    'Delivered': Color(0xFF15803D),
  };

  @override
  Widget build(BuildContext context) {
    final name = _names[kind]?[status] ?? status.toString();
    final color = _colors[name] ?? const Color(0xFF6B7280);

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(999),
        border: Border.all(color: color.withValues(alpha: 0.4)),
      ),
      child: Text(
        name,
        style: TextStyle(
          color: color,
          fontSize: 12,
          fontWeight: FontWeight.w600,
        ),
      ),
    );
  }
}
