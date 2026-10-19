# Quiz grading product heuristics

These heuristics help classify findings. They are comparison questions, not
implicit requirements. A missing advanced feature must not be reported as a
bug unless a product contract already promises it.

| Area                   | Questions to ask                                                                                 | Minimum evidence                         | Default classification when absent                                                    |
| ---------------------- | ------------------------------------------------------------------------------------------------ | ---------------------------------------- | ------------------------------------------------------------------------------------- |
| Authoring              | Can an author validate, save, reopen, preview, and publish without changing the item?            | Package round-trip plus author UI reload | Essential when data changes; otherwise UX improvement                                 |
| Publication            | Are attempts pinned to an immutable definition while later edits create a new candidate?         | API revision IDs and persisted attempt   | Essential for academic correctness                                                    |
| Learner delivery       | Is author-only material removed and is every supported control usable?                           | Learner payload plus browser UI          | Essential for answer-key leaks; otherwise UX/accessibility                            |
| Attempts               | Are start, submit, retry, limit, reload, and resume semantics explicit?                          | API state plus learner observation       | Essential for duplicate/lost final submissions; draft resume needs a product decision |
| Availability           | Do future/open/closed/late states fail closed and explain the state?                             | Policy API and direct URL test           | Necessary for safe operation                                                          |
| Automated grading      | Are correct, incorrect, partial, omitted, pending, and unsupported distinct?                     | Integer item/total results               | Essential for academic correctness                                                    |
| Human review           | Does the reviewer see only allowed evidence and can an instructor override with attribution?     | Reviewer UI/API and audit data           | Essential for academic correctness                                                    |
| Self/peer review       | Is the actor contextual, peer identity protected, and reciprocal work prevented?                 | Two isolated learner sessions            | Essential for privacy/correctness                                                     |
| Release                | Are score, feedback, and answer disclosure independently governed by the declared policy?        | Before/after learner projections         | Essential for academic correctness                                                    |
| Regrade                | Does a new round preserve history and prevent stale release from replacing the active result?    | Round IDs and learner projection         | Essential for academic correctness                                                    |
| Gradebook              | Are integer score units, weights, zero-weight practice, and contribution applied exactly once?   | Gradebook projection and totals          | Essential for academic correctness                                                    |
| Authorization          | Are enrollment, contextual grants, tenant, and direct endpoint access checked server-side?       | Real-session positive and negative tests | Necessary for safe operation                                                          |
| Audit                  | Are actor, action, reason, correlation, revision, and round attributable for academic mutations? | Durable audit/outbox evidence            | Necessary for safe operation                                                          |
| Recovery               | Do retries, replay, worker restart, and scheduling remain idempotent?                            | Deterministic integration tests          | Necessary for safe operation                                                          |
| Accessibility          | Do controls have names, keyboard operation, visible focus, and stable layout?                    | Testing Library and Playwright           | UX/accessibility unless it prevents completion                                        |
| Performance            | Does validated behavior remain correct under an approved load threshold?                         | NBomber report after deterministic tests | Operational; never substitutes correctness tests                                      |
| Notifications/passback | Are external effects emitted once and only after the relevant release?                           | Outbox receipt and target evidence       | Advanced until integration is enabled; correctness-critical once enabled              |

## Finding categories

- **Essential for academic correctness:** wrong score, leaked key, wrong
  revision, lost final submission, stale release, or incorrect gradebook.
- **Necessary for safe operation:** authorization, tenant isolation, audit,
  retry, concurrency, and non-duplicated effects.
- **UX/accessibility improvement:** the contract works but interaction or
  assistive access is materially poor.
- **Advanced/optional capability:** useful behavior without an approved current
  product promise.
- **Out of current scope:** requires another domain such as an AI provider or
  asset infrastructure and the grading behavior already fails closed.

Record uncertainty as `decision-needed`; do not choose product behavior during
an audit.
