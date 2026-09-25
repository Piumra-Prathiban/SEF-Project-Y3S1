import { useState } from 'react';

// A button that asks for inline confirmation before running `onConfirm`.
export default function ConfirmAction({
  label,
  confirmLabel = 'Yes, continue',
  question = 'Are you sure?',
  onConfirm,
  busy = false,
  tone = 'danger',
}) {
  const [asking, setAsking] = useState(false);

  if (!asking) {
    return (
      <button
        type="button"
        className={`button button-${tone}`}
        onClick={() => setAsking(true)}
        disabled={busy}
      >
        {label}
      </button>
    );
  }

  return (
    <div className="confirm" role="group" aria-label={question}>
      <span>{question}</span>
      <button
        type="button"
        className={`button button-${tone}`}
        disabled={busy}
        onClick={async () => {
          await onConfirm();
          setAsking(false);
        }}
      >
        {busy ? 'Working…' : confirmLabel}
      </button>
      <button
        type="button"
        className="button button-secondary"
        onClick={() => setAsking(false)}
        disabled={busy}
      >
        Cancel
      </button>
    </div>
  );
}
