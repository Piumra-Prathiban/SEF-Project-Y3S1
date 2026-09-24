import { useState } from 'react';
import {
  createShipment,
  updateShipmentStatus,
  ShipmentStatus,
  ShipmentStatusName,
} from '../../services/orderService';
import { formatDateTime } from '../../utils/format';
import StatusBadge from '../../components/StatusBadge';
import { describeShipmentError } from './shipmentErrors';
import { useSessionGuard } from '../../hooks/useSessionGuard';
import './orders.css';

// Presentation only: the shipment lifecycle stages, used to draw progress.
// The backend owns the real state machine - this component deliberately does
// not encode which transitions are allowed; invalid ones are surfaced as 409s.
const PROGRESS_STEPS = [
  { status: ShipmentStatus.Pending, label: 'Pending' },
  { status: ShipmentStatus.Shipped, label: 'Shipped' },
  { status: ShipmentStatus.Delivered, label: 'Delivered' },
];

const CONFIRM_STATUSES = [ShipmentStatus.Delivered, ShipmentStatus.Cancelled];

function ShipmentProgress({ status }) {
  if (status === ShipmentStatus.Cancelled) {
    return (
      <p className="shipment-progress shipment-progress--cancelled">
        Shipment cancelled
      </p>
    );
  }

  const currentIndex = PROGRESS_STEPS.findIndex((step) => step.status === status);

  return (
    <ol className="shipment-progress" aria-label="Shipment progress">
      {PROGRESS_STEPS.map((step, index) => {
        const isCurrent = index === currentIndex;
        const isComplete = index <= currentIndex;

        return (
          <li
            key={step.status}
            className={[
              'shipment-progress__step',
              isComplete ? 'shipment-progress__step--complete' : '',
              isCurrent ? 'shipment-progress__step--current' : '',
            ]
              .filter(Boolean)
              .join(' ')}
            aria-current={isCurrent ? 'step' : undefined}
          >
            {step.label}
          </li>
        );
      })}
    </ol>
  );
}

function ShipmentUpdateForm({ token, orderId, shipment, onChanged }) {
  const guardSessionExpiry = useSessionGuard();
  const [selectedStatus, setSelectedStatus] = useState('');
  const [carrier, setCarrier] = useState(shipment.carrier ?? '');
  const [trackingNumber, setTrackingNumber] = useState(
    shipment.trackingNumber ?? '',
  );
  const [updating, setUpdating] = useState(false);
  const [error, setError] = useState(null);

  const statusOptions = Object.values(ShipmentStatus).filter(
    (statusOption) => statusOption !== shipment.status,
  );

  async function handleSubmit(event) {
    event.preventDefault();

    const targetStatus = Number(selectedStatus);

    if (!statusOptions.includes(targetStatus)) {
      setError('Select one of the available shipment statuses.');
      return;
    }

    if (
      CONFIRM_STATUSES.includes(targetStatus)
      && !window.confirm(
        `Mark this shipment as ${ShipmentStatusName[targetStatus]}?`,
      )
    ) {
      return;
    }

    setUpdating(true);
    setError(null);

    try {
      await updateShipmentStatus(token, orderId, shipment.id, {
        status: targetStatus,
        carrier: carrier.trim() || null,
        trackingNumber: trackingNumber.trim() || null,
      });

      setSelectedStatus('');
      await onChanged(`Shipment marked as ${ShipmentStatusName[targetStatus]}.`);
    } catch (err) {
      if (!guardSessionExpiry(err)) {
        setError(describeShipmentError(err));
      }
    } finally {
      setUpdating(false);
    }
  }

  return (
    <form className="shipment-form" onSubmit={handleSubmit}>
      <h4>Update shipment</h4>

      <label htmlFor={`shipment-status-${shipment.id}`}>
        New shipment status
        <select
          id={`shipment-status-${shipment.id}`}
          value={selectedStatus}
          onChange={(event) => setSelectedStatus(event.target.value)}
        >
          <option value="">Select a status…</option>
          {statusOptions.map((statusOption) => (
            <option key={statusOption} value={statusOption}>
              {ShipmentStatusName[statusOption]}
            </option>
          ))}
        </select>
      </label>

      <label htmlFor={`shipment-carrier-${shipment.id}`}>
        Carrier
        <input
          id={`shipment-carrier-${shipment.id}`}
          type="text"
          maxLength={100}
          value={carrier}
          onChange={(event) => setCarrier(event.target.value)}
        />
      </label>

      <label htmlFor={`shipment-tracking-${shipment.id}`}>
        Tracking number
        <input
          id={`shipment-tracking-${shipment.id}`}
          type="text"
          maxLength={100}
          value={trackingNumber}
          onChange={(event) => setTrackingNumber(event.target.value)}
        />
      </label>

      <button type="submit" disabled={updating || selectedStatus === ''}>
        {updating ? 'Saving…' : 'Update shipment'}
      </button>

      {error && (
        <p className="status-feedback status-feedback--error" role="alert">
          {error}
        </p>
      )}
    </form>
  );
}

function ShipmentsSection({ order, token, canManage, onRefresh }) {
  const guardSessionExpiry = useSessionGuard();
  const [carrier, setCarrier] = useState('');
  const [trackingNumber, setTrackingNumber] = useState('');
  const [creating, setCreating] = useState(false);
  const [error, setError] = useState(null);
  const [success, setSuccess] = useState(null);

  async function handleCreate(event) {
    event.preventDefault();

    setCreating(true);
    setError(null);
    setSuccess(null);

    try {
      await createShipment(token, order.id, {
        carrier: carrier.trim() || null,
        trackingNumber: trackingNumber.trim() || null,
      });

      await onRefresh();
      setCarrier('');
      setTrackingNumber('');
      setSuccess('Shipment created.');
    } catch (err) {
      if (!guardSessionExpiry(err)) {
        setError(describeShipmentError(err));
      }
    } finally {
      setCreating(false);
    }
  }

  async function handleChanged(message) {
    setError(null);
    setSuccess(null);

    try {
      await onRefresh();
      setSuccess(message);
    } catch (err) {
      if (!guardSessionExpiry(err)) {
        setError(describeShipmentError(err));
      }
    }
  }

  return (
    <section className="order-detail__shipments" aria-label="Shipments">
      <h2>Shipments</h2>

      {order.shipments.length === 0 ? (
        <p className="order-detail__empty">No shipments recorded.</p>
      ) : (
        <div className="shipment-list">
          {order.shipments.map((shipment) => (
            <article className="shipment" key={shipment.id}>
              <div className="shipment__header">
                <StatusBadge status={shipment.status} kind="shipment" />
                <ShipmentProgress status={shipment.status} />
              </div>

              <dl className="shipment__details">
                <div>
                  <dt>Carrier</dt>
                  <dd>{shipment.carrier ?? '—'}</dd>
                </div>
                <div>
                  <dt>Tracking number</dt>
                  <dd>{shipment.trackingNumber ?? '—'}</dd>
                </div>
                <div>
                  <dt>Shipped at</dt>
                  <dd>
                    {shipment.shippedAt ? (
                      <time dateTime={shipment.shippedAt}>
                        {formatDateTime(shipment.shippedAt)}
                      </time>
                    ) : (
                      '—'
                    )}
                  </dd>
                </div>
                <div>
                  <dt>Delivered at</dt>
                  <dd>
                    {shipment.deliveredAt ? (
                      <time dateTime={shipment.deliveredAt}>
                        {formatDateTime(shipment.deliveredAt)}
                      </time>
                    ) : (
                      '—'
                    )}
                  </dd>
                </div>
              </dl>

              {canManage && (
                <ShipmentUpdateForm
                  token={token}
                  orderId={order.id}
                  shipment={shipment}
                  onChanged={handleChanged}
                />
              )}
            </article>
          ))}
        </div>
      )}

      {success && (
        <p className="status-feedback status-feedback--success" role="status">
          {success}
        </p>
      )}

      {error && (
        <p className="status-feedback status-feedback--error" role="alert">
          {error}
        </p>
      )}

      {canManage && order.shipments.length === 0 && (
        <form className="shipment-form" onSubmit={handleCreate}>
          <h4>Create shipment</h4>

          <label htmlFor="shipment-carrier">
            Carrier
            <input
              id="shipment-carrier"
              type="text"
              maxLength={100}
              value={carrier}
              onChange={(event) => setCarrier(event.target.value)}
            />
          </label>

          <label htmlFor="shipment-tracking">
            Tracking number
            <input
              id="shipment-tracking"
              type="text"
              maxLength={100}
              value={trackingNumber}
              onChange={(event) => setTrackingNumber(event.target.value)}
            />
          </label>

          <button type="submit" disabled={creating}>
            {creating ? 'Creating…' : 'Create shipment'}
          </button>

          <p className="shipment-form__note">
            Carrier and tracking number are optional here — they can also be
            added when the shipment is marked as shipped. The server validates
            shipment eligibility.
          </p>
        </form>
      )}
    </section>
  );
}

export default ShipmentsSection;
