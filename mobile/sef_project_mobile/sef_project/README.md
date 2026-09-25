# Clothic customer shopping app

Flutter client for product discovery, wishlist, cart, customer profile, and address management.

## Run locally

1. Start `backend/SEF_Project.Api` with its HTTP launch profile.
2. Run `flutter pub get` in this directory.
3. Start an Android emulator and run `flutter run`.

The Android emulator defaults to `http://10.0.2.2:5193/api`. Override the API root for a physical device or another platform:

```sh
flutter run --dart-define=API_BASE_URL=http://YOUR_HOST:5193/api
```

Use HTTPS for non-development deployments. Android cleartext traffic is allowed by the main manifest (`android:usesCleartextTraffic="true"`) as well as the debug manifest, so HTTP works against the local API in every build type.

## Verification

```sh
flutter analyze
flutter test
```

The current product discovery API supplies products, categories, variants, prices, and inventory availability. The UI is ready to render image, size, and colour fields, but the backend must expose those fields before the corresponding live filters and media can be enabled.
