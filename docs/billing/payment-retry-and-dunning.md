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
      "MaxRetries": 4,
      "BackoffBaseMinutes": 1.0,
      "BackoffMultiplier": 5.0,
      "JitterRatio": 0.2
    }
  }
}
```

- `MaxRetries` — maximum retry attempts per payment (`0`–`20`), default `4` (issue #415).
  Persisted on the payment at creation time (`Payment.MaxRetries`).
- `BackoffBaseMinutes` — delay before the first retry (`0`–`1440`).
- `BackoffMultiplier` — exponential factor applied per consecutive failure.
- `JitterRatio` — fraction of the computed delay added as random jitter (`0`–`0.5`, default
  `0.2`), applied by the failure-code retry policy so simultaneously-failing payments spread
  out instead of thundering the gateway on one tick. `0` disables jitter.

The backoff delay before the next attempt is
`BackoffBaseMinutes * BackoffMultiplier^RetryCount` minutes (plus jitter). Unclassified
failure codes follow this schedule; classified codes follow the failure-code ladders below.

Consumers of the options today: `ProcessPaymentCommandHandler`, `RetryPaymentCommandHandler`,
`UpdatePaymentStatusCommandHandler`, `OrderPaymentService`, `OrderPaymentIntentService`,
`PaymentRetryPolicyResolver`.

### Failure-code retry policy (issue #415)

`PaymentRetryPolicyResolver` maps gateway failure codes to internal retry classes and is
injected at the handler layer (the entity stays config-free):

- **Hard declines — non-retriable immediately** (`IsRetriable=false`, never scheduled):
  `expired_card`, `card_expired`, `incorrect_cvc`, `invalid_cvc`, `cvc_check_failed`,
  `incorrect_number`, `invalid_number`, `invalid_card_number`, `card_not_supported`,
  `fraudulent`, `fraud_detected`, `card_declined_fraud`, `stolen_card`. The first failure
  with one of these codes marks the payment non-retriable and leaves it out of the queue.
- **`insufficient_funds`** — 3d / 7d / 14d ladder.
- **`do_not_honor`** — 1d / 7d ladder.
- **`processing_error`**, `temporarily_unavailable`, `try_again_later` — 0d / 1d / 2d / 3d
  fast ladder.
- **Everything else** (including generic `card_declined`, which is a decline and not a
  transient error) — the configurable exponential backoff above with jitter.

When a failure-code ladder is shorter than `MaxRetries`, the ladder length caps the payment's
effective retry budget. Every failed payment records its class; exhausted or hard-declined
payments leave the retry queue.

## Automated retry queue — `Payments:RetryQueue` (issue #415)

```json
{
  "Payments": {
    "RetryQueue": {
      "Enabled": true,
      "PollIntervalMinutes": 60,
      "MaxBatchSize": 100
    }
  }
}
```

`PaymentRetryQueueBackgroundService` (hosted, opt-in — **default disabled**, matching the
sibling `RevenueAuditing:WorkerEnabled` worker, because it triggers real charges) polls
`PaymentRepository.GetDueForRetryAsync` — failed, retriable payments with budget left whose
scheduled next-retry time has passed — and dispatches each through the existing
`RetryPaymentCommand` path with per-item error isolation and a `MaxBatchSize` cap per cycle.
The handler's persisted Failed → Pending transition is the claim that prevents double-fire.
Queue dispatches run as a well-known system actor for audit attribution; interactive admin
retries stay attributed to the administrator.

Metrics (`GameGuild.Payments.RetryQueue` meter, OTel conventions per the billing webhook
retry worker): `gameguild_payments_retry_queue_dispatched_total`,
`gameguild_payments_retry_queue_recovered_total`,
`gameguild_payments_retry_queue_exhausted_total` (with a `payments.provider` dimension) and
`gameguild_payments_retry_queue_cycle_duration_ms`.

## Retry pause on payment-method change (issue #415)

`PaymentRetryMethodChangeService` (`PauseForMethodChangeAsync` / `ResumeAfterMethodChangeAsync`)
wraps the subscription-checkout method swap: while a customer changes their payment method
mid-dunning, scheduled retries for that subscription's failed payments are paused
(`NextRetryAt` cleared) so nothing charges the failed instrument; once the gateway confirms
the new default method, the paused payments are re-armed — re-pointed at the new method — and
the next queue pass retries them. Exhausted and hard-declined payments are never resurrected.

## Recovery lifecycle (issue #415)

When a retry attempt fails and the payment can no longer be retried (budget exhausted or a
hard decline), `RetryPaymentCommandHandler` suspends the subscription through the standard
subscription lifecycle with the `payment_retry_exhausted` reason and triggers the
final-notice dunning email (`SubscriptionRenewalFailedEvent`). A subsequent successful retry
of that payment reactivates the subscription — guarded by the recorded suspension reason, so
manually suspended subscriptions are never reactivated automatically.

## Retry-attempt history (issue #415)

Every executed retry attempt (manual or queued) is persisted as a `PaymentRetryAttempt`
row: attempt number, scheduled-for and executed-at timestamps, outcome, gateway, failure
code/message and gateway transaction reference. Exposed via
`GET api/v{version}/payments/{paymentId}:retry-attempts`.

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
