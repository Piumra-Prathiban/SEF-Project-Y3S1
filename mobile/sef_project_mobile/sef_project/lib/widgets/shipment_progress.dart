import 'package:flutter/material.dart';

import '../models/order_enums.dart';

/// Mirrors the React shipment progress indicator: Pending -> Shipped ->
/// Delivered, with the current stage emphasised. Cancelled shipments show only
/// their status badge, so this renders nothing for them.
class ShipmentProgress extends StatelessWidget {
  const ShipmentProgress({super.key, required this.status});

  final int status;

  static const List<int> _stages = [
    shipmentStatusPending,
    shipmentStatusShipped,
    shipmentStatusDelivered,
  ];

  @override
  Widget build(BuildContext context) {
    if (status == shipmentStatusCancelled) {
      return const SizedBox.shrink();
    }

    final name = shipmentStatusNames[status] ?? status.toString();
    final currentIndex = _stages.indexOf(status);

    return Semantics(
      label: 'Shipment progress: $name',
      child: Wrap(
        spacing: 8,
        runSpacing: 4,
        children: _stages.map((stage) {
          final index = _stages.indexOf(stage);
          final isCurrent = index == currentIndex;
          final isComplete = index <= currentIndex;
          final color = isComplete
              ? Theme.of(context).colorScheme.primary
              : Theme.of(context).colorScheme.outline;

          return Container(
            padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
            decoration: BoxDecoration(
              color: isComplete ? color.withValues(alpha: 0.12) : null,
              borderRadius: BorderRadius.circular(999),
              border: Border.all(color: color),
            ),
            child: Text(
              shipmentStatusNames[stage] ?? '$stage',
              style: TextStyle(
                color: color,
                fontSize: 12,
                fontWeight: isCurrent ? FontWeight.w700 : FontWeight.w500,
              ),
            ),
          );
        }).toList(),
      ),
    );
  }
}
