# Payment Retry & Dunning Configuration (issue #403)

The Payments module exposes two configuration-driven knobs for failed-payment recovery.
Both default to the behavior that was previously hardcoded, so no configuration is required
to keep today's semantics.

## Retry schedule — `Payments:Retry`

Bound to `PaymentRetryOptions` (registered in `PaymentsModule`, validated on startup).
Handlers and services resolve the options and pass them into `Payment.Create` /
`Payment.MarkAsFailed`; the entity itself stays free of configuration dependencies and
falls back to these same defaults when callers do not supply values.

```json
{
  "Payments": {
    "Retry": {
      "MaxRetries": 3,
      "BackoffBaseMinutes": 1.0,
      "BackoffMultiplier": 5.0
    }
  }
}
```

- `MaxRetries` — maximum retry attempts per payment (`0`–`20`). Persisted on the payment at
  creation time (`Payment.MaxRetries`).
- `BackoffBaseMinutes` — delay before the first retry (`0`–`1440`).
- `BackoffMultiplier` — exponential factor applied per consecutive failure.

The backoff delay before the next attempt is
`BackoffBaseMinutes * BackoffMultiplier^RetryCount` minutes. The defaults (`1 * 5^n`)
reproduce the legacy hardcoded `Math.Pow(5, RetryCount)` schedule: 1, 5, 25 minutes.

Consumers of the options today: `ProcessPaymentCommandHandler`, `RetryPaymentCommandHandler`,
`UpdatePaymentStatusCommandHandler`, `OrderPaymentService`, `OrderPaymentIntentService`.

## Dunning escalation ladder — `Payments:Dunning`

Bound to `PaymentDunningOptions` in the API composition root. The two subscription dunning
emails (payment failed → reminder; renewal failed → final notice) are driven by an
escalation ladder of steps, each with a day offset, template key, title, message and
priority. Messages support the `{date}` (failure date, `yyyy-MM-dd`) and `{reason}`
(provider failure reason) placeholders.

```json
{
  "Payments": {
    "Dunning": {
      "EscalationLadder": [
        {
          "Stage": "PaymentFailed",
          "DayOffset": 0,
          "Template": "PaymentFailedReminder",
          "Title": "Action required: your payment failed",
          "Message": "We were unable to process your most recent subscription payment on {date}. Reason: {reason}. Please update your payment method to avoid service interruption.",
          "Priority": "High"
        },
        {
          "Stage": "FinalNotice",
          "DayOffset": 3,
          "Template": "FinalNotice",
          "Title": "Final notice: your subscription could not be renewed",
          "Message": "Your subscription renewal on {date} failed. Reason: {reason}. If we cannot collect payment, your subscription will be suspended. Please update your payment method now to keep your service active.",
          "Priority": "Urgent"
        }
      ]
    }
  }
}
```

Stages:

- `PaymentFailed` — fired by `SubscriptionPaymentFailedEvent` (a subscription payment failed).
- `FinalNotice` — fired by `SubscriptionRenewalFailedEvent` (automatic renewal failed; the
  terminal dunning stage today).

Resolution (`PaymentDunningLadder.Resolve`): when a dunning event arrives, the highest rung
of the matching stage whose `DayOffset` has been reached (days since the failure date) is
sent. Events currently fire when the failure is recorded, so the first rung of each stage is
sent immediately and `DayOffset` documents the intended cadence; the automated retry queue
that will drive later rungs on schedule is tracked in #415. If a stage has no configured
step, the built-in default for that stage is used (fail-closed to the legacy message).

## Related tracking

- Refund approval workflow: #419 (dispute and refund approval cycle).
- Multiple payment gateway support: #413.
- Automated retry queue: #415.
