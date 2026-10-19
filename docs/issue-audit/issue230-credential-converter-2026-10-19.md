# Credential converter review — issue 230

The original title requests DTO conversion and polymorphic credential
deserialization. Its recorded contract rejects unknown types and unrecognized
shapes with `JsonException`. Current source supports email, phone, username,
OAuth, Web3 and certificate DTOs.

## Corrected finding

The initial review considered the exceptions from `JsonElement.TryGetProperty`
and `JsonElement.GetString` in isolation. Those methods can throw
`InvalidOperationException`, but that observation did not trace the public
`JsonSerializer.Deserialize` boundary. The .NET 10 framework marks these exceptions
as rethrowable and the serializer wraps them in `JsonException`.

The claimed malformed-input failure through the serializer is therefore withdrawn.
The added production guards were removed; the converter is byte-identical to the
accepted develop implementation. This finding was corrected through source tracing;
no locally executed baseline or HTTP500 reproduction is claimed.

- [TryGetProperty exception contract](https://learn.microsoft.com/en-us/dotnet/api/system.text.json.jsonelement.trygetproperty?view=net-10.0)
- [GetString exception contract](https://learn.microsoft.com/en-us/dotnet/api/system.text.json.jsonelement.getstring?view=net-10.0)
- [Framework serializer exception translation](https://github.com/dotnet/runtime/blob/v10.0.0/src/libraries/System.Text.Json/src/System/Text/Json/Serialization/JsonConverterOfT.ReadCore.cs)
- [Framework rethrowable exception source](https://github.com/dotnet/runtime/blob/v10.0.0/src/libraries/System.Text.Json/src/System/Text/Json/ThrowHelper.cs)

The temporary reopening is corrected by restoring the previous closed state,
without claiming a newly implemented feature or a newly verified completion.
The full current-state recertification remains pending the executable evidence below.
The typed production sign-in endpoint uses a separate request contract.

## Required verification

The original thirteen converter cases remain, with the email serialization test
strengthened to assert its discriminator and actual round-trip properties.
Eighteen additional cases cover explicit/auto certificate detection, all six
declared type round-trips, malformed roots/discriminators and null compatibility.
The malformed-input cases characterize the serializer's existing error contract;
they are not claimed to fail on the previous implementation. Their expectations
remain independent of the converter's internal operations.

Exact-source hosted compilation and all 31 converter results, plus the existing
explicit-null error and related typed sign-in/DTO native tests, must pass before
the current-state recertification is accepted. No local SDK, test, database,
container or application execution was performed. Production converter, route,
schema, client and global MVC registration are unchanged.
