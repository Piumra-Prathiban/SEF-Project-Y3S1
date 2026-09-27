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

const Map<int, String> returnStatusNames = {
  0: 'Requested',
  1: 'Approved',
  2: 'Rejected',
  3: 'Received',
  4: 'Refunded',
  5: 'Cancelled',
};

const Map<int, String> returnReasonNames = {
  0: 'Wrong size',
  1: 'Damaged',
  2: 'Not as described',
  3: 'Changed my mind',
  4: 'Wrong item',
  5: 'Other',
};

const int orderStatusCancelled = 5;
const int orderStatusRefunded = 6;
const int orderStatusCompleted = 4;
const int returnStatusRequested = 0;

const int shipmentStatusPending = 0;
const int shipmentStatusShipped = 1;
const int shipmentStatusDelivered = 2;
const int shipmentStatusCancelled = 3;
