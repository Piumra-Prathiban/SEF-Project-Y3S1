import 'package:flutter/material.dart';

import '../models/shopping_models.dart';
import '../state/customer_store.dart';
import '../widgets/common.dart';

class ProfileScreen extends StatefulWidget {
  const ProfileScreen({super.key});

  @override
  State<ProfileScreen> createState() => _ProfileScreenState();
}

class _ProfileScreenState extends State<ProfileScreen> {
  bool _loaded = false;

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    if (!_loaded) {
      _loaded = true;
      WidgetsBinding.instance.addPostFrameCallback((_) {
        StoreScope.of(context).loadProfile().catchError((_) {});
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final store = StoreScope.of(context);
    return Scaffold(
      appBar: AppBar(
        title: const Text('Profile'),
        actions: [
          IconButton(
            tooltip: 'Log out',
            onPressed: store.logout,
            icon: const Icon(Icons.logout),
          ),
        ],
      ),
      floatingActionButton: store.profile == null
          ? null
          : FloatingActionButton.extended(
              onPressed: () => _editAddress(context, store, null),
              icon: const Icon(Icons.add_location_alt_outlined),
              label: const Text('Add address'),
            ),
      body: AsyncPanel(
        loading: store.loading && store.profile == null,
        error: store.error,
        empty: store.profile == null,
        emptyTitle: 'Profile unavailable',
        emptyMessage: 'Pull to refresh or try again.',
        onRetry: store.loadProfile,
        child: RefreshIndicator(
          onRefresh: store.loadProfile,
          child: ListView(
            physics: const AlwaysScrollableScrollPhysics(),
            padding: const EdgeInsets.fromLTRB(16, 16, 16, 96),
            children: [
              Center(
                child: ConstrainedBox(
                  constraints: const BoxConstraints(maxWidth: 760),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      if (store.profile != null)
                        _ProfileCard(profile: store.profile!),
                      const SizedBox(height: 24),
                      Text('Addresses',
                          style: Theme.of(context).textTheme.headlineSmall),
                      const SizedBox(height: 10),
                      if (store.addresses.isEmpty)
                        const Card(
                          child: Padding(
                            padding: EdgeInsets.all(20),
                            child: Text(
                              'No saved addresses. Add one for faster delivery setup.',
                            ),
                          ),
                        )
                      else
                        ...store.addresses.map(
                          (address) => Padding(
                            padding: const EdgeInsets.only(bottom: 10),
                            child: _AddressCard(address: address),
                          ),
                        ),
                    ],
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _ProfileCard extends StatelessWidget {
  const _ProfileCard({required this.profile});

  final CustomerProfile profile;

  @override
  Widget build(BuildContext context) => Card(
        child: Padding(
          padding: const EdgeInsets.all(20),
          child: Row(
            children: [
              const CircleAvatar(radius: 32, child: Icon(Icons.person, size: 34)),
              const SizedBox(width: 16),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text('${profile.firstName} ${profile.lastName}'.trim(),
                        style: Theme.of(context).textTheme.titleLarge),
                    Text(profile.email),
                  ],
                ),
              ),
              IconButton(
                tooltip: 'Edit profile',
                onPressed: () => _editProfile(context, profile),
                icon: const Icon(Icons.edit_outlined),
              ),
            ],
          ),
        ),
      );
}

class _AddressCard extends StatelessWidget {
  const _AddressCard({required this.address});

  final CustomerAddress address;

  @override
  Widget build(BuildContext context) {
    final store = StoreScope.of(context);
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Expanded(
                  child: Row(
                    children: [
                      Text(address.label,
                          style: Theme.of(context).textTheme.titleMedium),
                      if (address.isDefault) ...[
                        const SizedBox(width: 8),
                        const Chip(label: Text('Default')),
                      ],
                    ],
                  ),
                ),
                IconButton(
                  tooltip: 'Edit address',
                  onPressed: () => _editAddress(context, store, address),
                  icon: const Icon(Icons.edit_outlined),
                ),
                IconButton(
                  tooltip: 'Delete address',
                  onPressed: address.id == null
                      ? null
                      : () => _deleteAddress(context, store, address),
                  icon: const Icon(Icons.delete_outline),
                ),
              ],
            ),
            Text(address.addressLine1),
            if (address.addressLine2?.isNotEmpty == true)
              Text(address.addressLine2!),
            Text([
              address.city,
              if (address.province?.isNotEmpty == true) address.province!,
              address.postalCode,
            ].join(', ')),
            Text(address.country),
            if (!address.isDefault && address.id != null) ...[
              const SizedBox(height: 8),
              TextButton.icon(
                onPressed: () => runAction(
                  context,
                  () => store.saveAddress(_copyAddress(address, isDefault: true)),
                  success: 'Default address updated.',
                ),
                icon: const Icon(Icons.check_circle_outline),
                label: const Text('Make default'),
              ),
            ],
          ],
        ),
      ),
    );
  }
}

Future<void> _editProfile(
    BuildContext context, CustomerProfile profile) async {
  final store = StoreScope.of(context);
  final formKey = GlobalKey<FormState>();
  final email = TextEditingController(text: profile.email);
  final firstName = TextEditingController(text: profile.firstName);
  final lastName = TextEditingController(text: profile.lastName);
  try {
    await showDialog<void>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('Edit profile'),
        content: Form(
          key: formKey,
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              TextFormField(
                controller: firstName,
                decoration: const InputDecoration(labelText: 'First name'),
                validator: (value) => _requiredWithLimit(value, 50),
              ),
              const SizedBox(height: 12),
              TextFormField(
                controller: lastName,
                decoration: const InputDecoration(labelText: 'Last name'),
                validator: (value) => _requiredWithLimit(value, 50),
              ),
              const SizedBox(height: 12),
              TextFormField(
                controller: email,
                keyboardType: TextInputType.emailAddress,
                decoration: const InputDecoration(labelText: 'Email'),
                validator: (value) => value == null ||
                        value.length > 320 ||
                        !RegExp(r'^[^@\s]+@[^@\s]+\.[^@\s]+$').hasMatch(value)
                    ? 'Enter a valid email.'
                    : null,
              ),
            ],
          ),
        ),
        actions: [
          TextButton(
              onPressed: () => Navigator.pop(dialogContext),
              child: const Text('Cancel')),
          FilledButton(
            onPressed: () async {
              if (!formKey.currentState!.validate()) return;
              await runAction(
                dialogContext,
                () => store.saveProfile(CustomerProfile(
                  email: email.text.trim(),
                  firstName: firstName.text.trim(),
                  lastName: lastName.text.trim(),
                )),
              );
              if (dialogContext.mounted && store.error == null) {
                Navigator.pop(dialogContext);
              }
            },
            child: const Text('Save'),
          ),
        ],
      ),
    );
  } finally {
    email.dispose();
    firstName.dispose();
    lastName.dispose();
  }
}

Future<void> _editAddress(BuildContext context, CustomerStore store,
    CustomerAddress? address) async {
  final saved = await showModalBottomSheet<CustomerAddress>(
    context: context,
    isScrollControlled: true,
    useSafeArea: true,
    builder: (context) => _AddressEditor(address: address),
  );
  if (saved != null && context.mounted) {
    await runAction(context, () => store.saveAddress(saved),
        success: 'Address saved.');
  }
}

Future<void> _deleteAddress(BuildContext context, CustomerStore store,
    CustomerAddress address) async {
  final confirmed = await showDialog<bool>(
    context: context,
    builder: (context) => AlertDialog(
      title: const Text('Delete address?'),
      content: Text('Remove ${address.label} from your saved addresses?'),
      actions: [
        TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('Cancel')),
        FilledButton(
            onPressed: () => Navigator.pop(context, true),
            child: const Text('Delete')),
      ],
    ),
  );
  if (confirmed == true && context.mounted) {
    await runAction(context, () => store.deleteAddress(address.id!),
        success: 'Address deleted.');
  }
}

class _AddressEditor extends StatefulWidget {
  const _AddressEditor({this.address});

  final CustomerAddress? address;

  @override
  State<_AddressEditor> createState() => _AddressEditorState();
}

class _AddressEditorState extends State<_AddressEditor> {
  final _formKey = GlobalKey<FormState>();
  late final TextEditingController _label;
  late final TextEditingController _line1;
  late final TextEditingController _line2;
  late final TextEditingController _city;
  late final TextEditingController _province;
  late final TextEditingController _postalCode;
  late final TextEditingController _country;
  late bool _isDefault;

  @override
  void initState() {
    super.initState();
    final address = widget.address;
    _label = TextEditingController(text: address?.label ?? 'Home');
    _line1 = TextEditingController(text: address?.addressLine1 ?? '');
    _line2 = TextEditingController(text: address?.addressLine2 ?? '');
    _city = TextEditingController(text: address?.city ?? '');
    _province = TextEditingController(text: address?.province ?? '');
    _postalCode = TextEditingController(text: address?.postalCode ?? '');
    _country = TextEditingController(text: address?.country ?? 'Sri Lanka');
    _isDefault = address?.isDefault ?? false;
  }

  @override
  void dispose() {
    _label.dispose();
    _line1.dispose();
    _line2.dispose();
    _city.dispose();
    _province.dispose();
    _postalCode.dispose();
    _country.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => Padding(
        padding: EdgeInsets.fromLTRB(
            20, 20, 20, MediaQuery.viewInsetsOf(context).bottom + 20),
        child: Form(
          key: _formKey,
          child: ListView(
            shrinkWrap: true,
            children: [
              Text(widget.address == null ? 'Add address' : 'Edit address',
                  style: Theme.of(context).textTheme.headlineSmall),
              const SizedBox(height: 16),
              _field(_label, 'Label', maximumLength: 50),
              _field(_line1, 'Address line 1', maximumLength: 200),
              _field(_line2, 'Address line 2', isRequired: false, maximumLength: 200),
              _field(_city, 'City', maximumLength: 100),
              _field(_province, 'Province', isRequired: false, maximumLength: 100),
              _field(
                _postalCode,
                'Postal code',
                maximumLength: 20,
                validator: (value) {
                  final requiredError = _required(value);
                  if (requiredError != null) return requiredError;
                  if (value!.length < 2 ||
                      value.length > 20 ||
                      !RegExp(r'^[A-Za-z0-9][A-Za-z0-9 -]*$')
                          .hasMatch(value)) {
                    return 'Enter a valid postal code.';
                  }
                  return null;
                },
              ),
              _field(_country, 'Country', maximumLength: 100),
              SwitchListTile(
                contentPadding: EdgeInsets.zero,
                title: const Text('Default address'),
                value: _isDefault,
                onChanged: (value) => setState(() => _isDefault = value),
              ),
              FilledButton(
                onPressed: _save,
                child: const Text('Save address'),
              ),
            ],
          ),
        ),
      );

  Widget _field(
    TextEditingController controller,
    String label, {
    bool isRequired = true,
    int? maximumLength,
    String? Function(String?)? validator,
  }) =>
      Padding(
        padding: const EdgeInsets.only(bottom: 12),
        child: TextFormField(
          controller: controller,
          decoration: InputDecoration(labelText: label),
          validator: validator ?? (value) {
            if (isRequired) {
              final requiredError = _required(value);
              if (requiredError != null) return requiredError;
            }
            if ((value?.length ?? 0) > (maximumLength ?? 1000000)) {
              return 'Use no more than $maximumLength characters.';
            }
            return null;
          },
        ),
      );

  void _save() {
    if (!_formKey.currentState!.validate()) return;
    Navigator.pop(
      context,
      CustomerAddress(
        id: widget.address?.id,
        label: _label.text.trim(),
        addressLine1: _line1.text.trim(),
        addressLine2:
            _line2.text.trim().isEmpty ? null : _line2.text.trim(),
        city: _city.text.trim(),
        province:
            _province.text.trim().isEmpty ? null : _province.text.trim(),
        postalCode: _postalCode.text.trim(),
        country: _country.text.trim(),
        isDefault: _isDefault,
      ),
    );
  }
}

String? _required(String? value) =>
    value == null || value.trim().isEmpty ? 'This field is required.' : null;

String? _requiredWithLimit(String? value, int maximumLength) {
  final requiredError = _required(value);
  if (requiredError != null) return requiredError;
  return value!.trim().length > maximumLength
      ? 'Use no more than $maximumLength characters.'
      : null;
}

CustomerAddress _copyAddress(CustomerAddress address,
        {required bool isDefault}) =>
    CustomerAddress(
      id: address.id,
      label: address.label,
      addressLine1: address.addressLine1,
      addressLine2: address.addressLine2,
      city: address.city,
      province: address.province,
      postalCode: address.postalCode,
      country: address.country,
      isDefault: isDefault,
    );
