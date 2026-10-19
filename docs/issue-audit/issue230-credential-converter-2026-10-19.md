# Credential converter review — issue 230

The original title requests DTO conversion and polymorphic credential
deserialization. Its recorded contract rejects unknown types and unrecognized
shapes with `JsonException`. Current source supports email, phone, username,
OAuth, Web3 and certificate DTOs.

## Current gap

The accepted develop converter called `JsonElement.TryGetProperty` before
checking for an object root, and `JsonElement.GetString` before checking the
discriminator kind. Arrays/scalars and non-string discriminator values can
therefore raise `InvalidOperationException` instead of the recorded JSON error.
This was established from source and the documented framework contracts; no local
baseline was executed.

- [TryGetProperty exception contract](https://learn.microsoft.com/en-us/dotnet/api/system.text.json.jsonelement.trygetproperty?view=net-10.0)
- [GetString exception contract](https://learn.microsoft.com/en-us/dotnet/api/system.text.json.jsonelement.getstring?view=net-10.0)

The correction validates the root and discriminator before these operations.
Explicit null discriminators keep their existing unknown-type error, and normal
serializer null payloads retain their default null behavior. Valid explicit types,
legacy property detection, serialization and credential fields retain their
existing contracts. The typed production sign-in endpoint uses a separate request
contract; this library finding is not an executed HTTP500 reproduction.

## Required verification

The original thirteen converter cases remain, with the email serialization test
strengthened to assert its discriminator and actual round-trip properties.
Eighteen additional cases cover explicit/auto certificate detection, all six
declared type round-trips, malformed roots/discriminators and null compatibility.
The eight malformed non-null-kind cases distinguish the identified old source
behavior; this statement is a source counterexample, not a locally run baseline.

Exact-source hosted compilation and all 31 converter results, plus the existing
explicit-null error and related typed sign-in/DTO native tests, must pass before
this reopened issue is considered complete. No local SDK, test, database,
container or application execution was performed. No route, schema, client or
global MVC converter registration is changed.
