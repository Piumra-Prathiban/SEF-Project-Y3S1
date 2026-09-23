import 'package:flutter/material.dart';

String money(num value, [String currency = 'LKR']) => '$currency ${value.toStringAsFixed(2)}';

class AsyncPanel extends StatelessWidget {
  const AsyncPanel({super.key, this.loading = false, this.error, this.empty = false, required this.child, this.onRetry, this.emptyTitle = 'Nothing here yet', this.emptyMessage = ''});
  final bool loading;
  final String? error;
  final bool empty;
  final Widget child;
  final VoidCallback? onRetry;
  final String emptyTitle;
  final String emptyMessage;
  @override
  Widget build(BuildContext context) {
    if (loading) return const Center(child: CircularProgressIndicator());
    if (error != null) return Center(child: Padding(padding: const EdgeInsets.all(24), child: Column(mainAxisSize: MainAxisSize.min, children: [const Icon(Icons.error_outline, size: 44), const SizedBox(height: 12), Text(error!, textAlign: TextAlign.center), if (onRetry != null) ...[const SizedBox(height: 12), FilledButton.tonal(onPressed: onRetry, child: const Text('Try again'))]])));
    if (empty) return Center(child: Padding(padding: const EdgeInsets.all(24), child: Column(mainAxisSize: MainAxisSize.min, children: [const Icon(Icons.inbox_outlined, size: 48), const SizedBox(height: 12), Text(emptyTitle, style: Theme.of(context).textTheme.titleLarge), const SizedBox(height: 6), Text(emptyMessage, textAlign: TextAlign.center)])));
    return child;
  }
}

void showMessage(BuildContext context, String message) => ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(message)));
Future<void> runAction(BuildContext context, Future<void> Function() action, {String? success}) async {
  try { await action(); if (context.mounted && success != null) showMessage(context, success); }
  catch (error) { if (context.mounted) showMessage(context, error.toString()); }
}
