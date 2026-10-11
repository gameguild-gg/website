# PR812 credential compensation CI repair

Issue169 capture source passed its API and audit suites in hosted run38153474271 at source28df84da36ee06b489725341e3e40690bf84cfbc. Authentication stopped the affected suite sequence:3085passed and1failed; the later authorization/shared suites and full-application OpenAPI did not run. This run is not accepted for merge. Prior signed-source acceptance remains separately recorded and is not transferred to the repaired revision.

The failing test was NoCredentialWork_RunsDummyVerificationAtTheConfiguredWorkFactor. Its reference median was319.6963ms, but the next real bcrypt operation took105.5630ms. The assertion required at least143.8633ms by multiplying the reference duration by0.45. Consecutive wall-clock samples do not reliably identify the work factor on a shared runner.

The repair preserves public hasher contracts, the same reload-aware cost policy, real BCrypt operation, caller cancellation and the unchanged400ms monotonic response floor. An internal helper returns the synthetic credential hash from the same operation used by the public completion-only dummy method. Native tests verify the encoded cost and BCrypt verification of that synthetic output, including configuration reload10to11. No output is exposed to API consumers or included in logs.

The compensation test invokes the real hasher and checks its completed operation at the configured cost exactly once. A pre-aged deterministic monotonic clock rejects any unnecessary timer. The completed-work branch uses a strict hasher mock to prove that no dummy method is invoked. This replaces both scheduler-dependent ratios. Existing fresh-window, monotonic-floor, cancellation and boundary tests remain.

One native reload case was added. No local SDK/database/application/browser runner was launched. All native tests, OpenAPI/client consistency and required quality/security/CLA gates must qualify the exact repaired PR head in hosted CI before normal merge. Issue169 remains open for its retained full scope.

## Hosted qualification and fixture diagnostics

At signed source `dde3ad9c98c712db086af9d1617807469cf43fdc`, hosted run38156853036 passed all9590 distinct native cases across six suites, with zero failures or skipped cases. The16 full-application OpenAPI HTTP cases also passed and were verified as repetitions of API integration cases:9606 total executions. Web, OpenAPI client consistency and the PR Required Gate passed. This is native qualification of that exact revision, not approval to merge.

Codacy reported five unused fixture parameters in the deterministic clock and test logger. The follow-up retains their override/interface signatures and records meaningful timer diagnostics, validates logger scope input, respects enabled log levels, and records/formats logger entries. The real dummy-work regression checks the completed debug entry along with exactly one configured cost. No production code or endpoint contract changes in this follow-up. All quality gates and native tests must qualify its new signed head before merge; no waiver or manual CI restart/cancellation is used.
