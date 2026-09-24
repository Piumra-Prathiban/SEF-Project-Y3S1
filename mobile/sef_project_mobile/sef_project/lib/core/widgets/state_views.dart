import 'package:flutter/material.dart';

import '../api/api_exception.dart';
import '../state/async_controller.dart';

class LoadingView extends StatelessWidget {
  const LoadingView({super.key, this.label = 'Loading…'});

  final String label;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Semantics(
        liveRegion: true,
        label: label,
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const CircularProgressIndicator(),
            const SizedBox(height: 16),
            Text(label),
          ],
        ),
      ),
    );
  }
}

class MessageView extends StatelessWidget {
  const MessageView({
    super.key,
    required this.icon,
    required this.title,
    this.message,
    this.action,
  });

  final IconData icon;
  final String title;
  final String? message;
  final Widget? action;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Center(
      child: SingleChildScrollView(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(icon, size: 48, color: theme.colorScheme.onSurfaceVariant),
            const SizedBox(height: 16),
            Text(title, style: theme.textTheme.titleMedium, textAlign: TextAlign.center),
            if (message != null) ...[
              const SizedBox(height: 8),
              Text(message!, textAlign: TextAlign.center),
            ],
            if (action != null) ...[
              const SizedBox(height: 16),
              action!,
            ],
          ],
        ),
      ),
    );
  }
}

class EmptyView extends StatelessWidget {
  const EmptyView({super.key, required this.title, this.message});

  final String title;
  final String? message;

  @override
  Widget build(BuildContext context) =>
      MessageView(icon: Icons.inbox_outlined, title: title, message: message);
}

class ErrorView extends StatelessWidget {
  const ErrorView({super.key, required this.error, required this.onRetry});

  final Object error;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return MessageView(
      icon: Icons.error_outline,
      title: describeError(error),
      action: FilledButton.icon(
        onPressed: onRetry,
        icon: const Icon(Icons.refresh),
        label: const Text('Try again'),
      ),
    );
  }
}

/// Renders loading / error / empty / data for an [AsyncController].
class AsyncStateView<T> extends StatelessWidget {
  const AsyncStateView({
    super.key,
    required this.controller,
    required this.builder,
    this.isEmpty,
    this.empty,
    this.loadingLabel = 'Loading…',
  });

  final AsyncController<T> controller;
  final Widget Function(BuildContext context, T data) builder;
  final bool Function(T data)? isEmpty;
  final Widget? empty;
  final String loadingLabel;

  @override
  Widget build(BuildContext context) {
    return ListenableBuilder(
      listenable: controller,
      builder: (context, _) => switch (controller.state) {
        Loading<T>() => LoadingView(label: loadingLabel),
        Failed<T>(:final error) => ErrorView(error: error, onRetry: controller.load),
        Loaded<T>(:final data) => (isEmpty?.call(data) ?? false) && empty != null
            ? empty!
            : builder(context, data),
      },
    );
  }
}
