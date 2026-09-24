import 'package:flutter/foundation.dart';

sealed class LoadState<T> {
  const LoadState();
}

final class Loading<T> extends LoadState<T> {
  const Loading();
}

final class Loaded<T> extends LoadState<T> {
  const Loaded(this.data);

  final T data;
}

final class Failed<T> extends LoadState<T> {
  const Failed(this.error);

  final Object error;
}

/// Loads data once and exposes it as a [LoadState]. Screens listen with
/// `ListenableBuilder`; this is the app's (built-in) state management.
class AsyncController<T> extends ChangeNotifier {
  AsyncController(this._loader);

  final Future<T> Function() _loader;

  LoadState<T> _state = Loading<T>();
  bool _disposed = false;
  int _generation = 0;

  LoadState<T> get state => _state;

  /// Shows the loading state, then the result.
  Future<void> load() {
    _set(Loading<T>());
    return _run();
  }

  /// Reloads while keeping current data visible (pull-to-refresh).
  Future<void> refresh() => _run();

  Future<void> _run() async {
    final generation = ++_generation;

    try {
      final data = await _loader();
      if (generation == _generation) {
        _set(Loaded<T>(data));
      }
    } catch (error) {
      if (generation == _generation) {
        _set(Failed<T>(error));
      }
    }
  }

  void _set(LoadState<T> state) {
    if (_disposed) {
      return;
    }
    _state = state;
    notifyListeners();
  }

  @override
  void dispose() {
    _disposed = true;
    super.dispose();
  }
}
