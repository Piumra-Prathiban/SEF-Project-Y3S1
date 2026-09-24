/// Enum wire contracts shared with the React client: the API serializes C#
/// enums as their underlying integers, so these maps are the single source of
/// display names on the Flutter side too.
const Map<int, String> orderStatusNames = {
  0: 'Pending',
  1: 'Confirmed',
  2: 'Preparing',
  3: 'Ready',
  4: 'Completed',
  5: 'Cancelled',
  6: 'Refunded',
};

const Map<int, String> paymentStatusNames = {
  0: 'Pending',
  1: 'Completed',
  2: 'Failed',
  3: 'Refunded',
};

const Map<int, String> paymentMethodNames = {
  0: 'Card',
  1: 'Cash',
  2: 'OnlineTransfer',
};

const Map<int, String> shipmentStatusNames = {
  0: 'Pending',
  1: 'Shipped',
  2: 'Delivered',
  3: 'Cancelled',
};
