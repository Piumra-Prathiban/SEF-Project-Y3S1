import {
  OrderStatusName,
  PaymentStatusName,
  ShipmentStatusName,
} from '../services/orderService';
import './StatusBadge.css';

const NAME_MAPS = {
  order: OrderStatusName,
  payment: PaymentStatusName,
  shipment: ShipmentStatusName,
};

function StatusBadge({ status, kind = 'order' }) {
  const nameMap = NAME_MAPS[kind];
  const name = nameMap[status] ?? String(status);

  return (
    <span className={`status-badge status-badge--${name.toLowerCase()}`}>
      {name}
    </span>
  );
}

export default StatusBadge;
