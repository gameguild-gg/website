# Authentication facade acceptance: #219

## Retained scope

Issue [#219](https://github.com/gameguild-gg/gameguild/issues/219) retains the
five criteria approved on 2026-10-03. Native duplicate #220 consolidates the
same orchestration requirement. The composite `IAuthService` contract inherits
`ILocalAuthService`, `IOAuthAuthService`, `IPasswordService` and
`IWeb3AuthService`; `AuthService` directly delegates their 18 operations.

This acceptance verifies the composite layer. Provider authentication,
credential validation, token persistence, account linking and other specialized
features retain their separate issues and acceptance criteria.

## Contract and operation matrix

| Specialized contract | Operations | Cancellation argument |
| --- | --- | --- |
| `ILocalAuthService` | Local sign-in, sign-up, refresh, refresh-token revocation | All four |
| `IOAuthAuthService` | GitHub, Google, Microsoft, Google ID token and Discord sign-in | All five |
| `IOAuthAuthService` | GitHub and Google authorization URLs | Neither contract accepts a token |
| `IPasswordService` | Send/verify email, request/reset/change password | All five |
| `IWeb3AuthService` | Generate challenge and verify signature | Both |

The independently declared operation matrix names each expected specialized
owner. A coverage assertion compares it with every inherited interface method
and every declared public facade method, preventing a supported operation from
silently escaping the verification matrix.

`AuthFacadeContractTests` records calls to all four collaborators and verifies
one call to the expected method with zero calls to the other collaborators.
Request objects and strings retain reference identity; GUIDs and cancellation
tokens retain their values. This includes password-change user ID, revocation
token/IP and both authorization redirect URIs. The returned task and response
identity are preserved, including unsuccessful response objects.

All 18 operations are checked for completion, faulted-task exception identity
and synchronous exception propagation. The 16 cancellable contracts are
checked with cancellation before invocation and after delegation while the
task is pending. Both the task and originating cancellation token are retained.
The URL contracts receive failure tests without inventing a cancellation
parameter absent from their interfaces. These **87 new cases** supplement the
existing `AuthServiceTests` success and failure cases.

## API registration and consumers

The active host composition calls `AddAuthenticationApplication()` and
`AddAuthenticationData(configuration)` from `InfrastructureLayerExtensions`.
The data registration declares all five contracts with scoped lifetime and the
real `AuthService`, `LocalAuthService`, `OAuthAuthService`, `PasswordService` and
`Web3AuthService` implementations.

`AuthFacadeApiRegistrationTests` starts the complete API through
`WebApplicationFactory<Program>`. Only the database provider changes to an
isolated EF InMemory database and startup initialization is disabled; the
authentication contracts and their dependencies are not replaced by mocks.
Five cases inspect the active descriptors and resolve the real implementation,
same instance within one scope and different instances across two scopes.
Eight more resolve the current facade-dependent CQRS handlers through their
registered `IRequestHandler` contracts:

| Handler | Current consumer evidence |
| --- | --- |
| `LocalSignInHandler` | `AuthController` local sign-in command |
| `LocalSignUpHandler` | `AuthController` local sign-up command |
| `RefreshTokenHandler` | `AuthController` refresh command |
| `RevokeTokenHandler` | `AuthController` revocation command |
| `GoogleIdTokenSignInHandler` | `AuthController` Google ID token command |
| `VerifyWeb3SignatureHandler` | `AuthController` Web3 verification command |
| `SocialSignInHandler` | Registered/resolvable social sign-in handler; no active caller found in the current source pass |
| `PolymorphicSignInHandler` | Registered/resolvable polymorphic sign-in handler; no active caller found in the current source pass |

The older `AuthenticationEndpoint.SignUp` also declares `IAuthService`, but
`MapAuthEndpoints` has no active invocation in the inspected host source. It is
retained as a historical entry point and is not counted as a demonstrated live
consumer. The commented registration in `AuthenticationExtensions` is not the
active DI registration.

## Original criterion mapping

| Approved requirement | Current evidence |
| --- | --- |
| Facade and covered flows delegate to their specialized contracts | All 18 explicit mappings, public contract coverage, task/result identity and single-owner assertions |
| No duplicate authentication, persistence or validation logic | Direct delegation-only source; no repositories/context in the facade; zero calls to other collaborators in each scenario |
| Preserve parameters, results, errors and supported cancellation | 87 contract cases including user ID, IP, redirect URI, response/task identity, synchronous/asynchronous errors and pre/during-call cancellation |
| API registration/resolution and documented consumers | Actual API host composition; five real scoped-service resolutions and eight registered handler resolutions; consumer matrix above |
| Executed delegation, success, failure and cancellation evidence | Fresh TRX runs for all 100 new cases and the complete Authentication unit suite |

## Verification status

On 2026-10-04, the complete Authentication unit suite passed **1,939/1,939**
cases, including the 87 new facade contract cases. Actual API registration
selection passed **13/13**. Both edited test projects built with zero warnings
and errors. The complete Authorization unit suite also passed **1,667/1,667**
after a warning-clean build. These selections total **3,619 local passing cases**
without counting the 87 facade cases twice. Results are retained under
`artifacts/test-results/issue-219-facade-20261004` in the reused isolated checkout.

The local test-project builds used `BuildProjectReferences=false` with unchanged
production assemblies from the preceding accepted API foundation build. The final exact-head CI below supplies fresh Release build and complete
API/Authentication verification. The two initial test-authoring
build errors were corrected and are excluded from successful evidence.

The previously pending CI and merge completed as recorded below. No claim is made that
these isolated facade/DI tests execute a provider's remote sandbox, a credential
algorithm or a production database flow.


## Final CI, merge and official closeout

[PR #682](https://github.com/gameguild-gg/gameguild/pull/682) merged at
`e5352249efcc076eb0fd825b400c679f94f3f541` on `2026-10-04T20:59:40Z`. Exact head
`10347655b091a9f1dacb2c04e418f07f58c26b41` passed **2,988 CI cases**: 1,035 API, 1,939 Authentication and
14 complete-application HTTP OpenAPI tests. Fresh Release API/integration
builds reported zero warnings/errors. All applicable required checks,
Codacy and four CodeQL analyses passed. The client gate was SKIPPED because
production contracts/source did not change.
[CI evidence](https://github.com/gameguild-gg/gameguild/actions/runs/37233005119/job/111526538164).

#219 was officially CLOSED/COMPLETED at `2026-10-04T21:00:27Z` with
its [full acceptance evidence](https://github.com/gameguild-gg/gameguild/issues/219#issuecomment-5984333216). All five approved requirements
and the other 327 scoped issues are preserved. The source implementation was
already present; the new 100 cases and actual-host evidence establish its
accepted orchestration behavior. The merged branch is removed and the isolated
checkout reused; all 55 primary local files remain unchanged.
