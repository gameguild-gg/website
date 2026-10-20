# #212: comparable behavioral location identities

Refs #212. Implementation and full acceptance remain pending matching-source hosted verification.

The original issue has an empty body and requests anomaly detection and behavioral risk scoring.
The existing facade, risk bands, IP/device/time signals, configurable historical window and low-data
confidence handling remain part of its requirement review. The shared service behavior is also
relevant to #217. Pre-token geolocation input and adaptive sign-in policy remain tracked in #211.

## Reproduced defect

`AuthAttemptService` records structured country code/city and the display value `City, Country name`.
The behavioral scorer compared the display string with `Country-City`. Real-writer-shaped known
locations could gain 25 risk points. Hosted source `a3b8824a01b2e2e24aa409190cb61fd731c8f872`
reproduced this with 11 focused failures and eight passing controls; no other test failed. Artifact
`12235103260` has SHA-256 `b0d34fe20d06ca0a8f813efa544a088c981451797821eb7e6c4c7a2056ff6181`.
This red result diagnoses the gap; it is not acceptance evidence.

## Correction and compatibility

Behavioral location grouping now uses comparable typed country/city keys. Explicit current country
codes and persisted structured fields take priority. Known code/name pairs can be compared through
the writer's display label using its already-known city prefix. Surrounding whitespace/case are normalized.
Unambiguous legacy two-letter-code/city strings remain supported, with hyphens in city names intact.
Grouping occurs after normalization. Public history strings, database columns and HTTP contracts
are unchanged.

Missing city/country, incomparable country representations and ambiguous display-only history
contribute no behavioral location anomaly. Other existing risk factors continue to be evaluated. The code does
not infer a city from arbitrary comma-separated text or invent country/city aliases.

The login-analysis reader uses the same historical identity reader. Its former hyphen-only parser
could interpret the entire actual writer display string as a country. Country comparisons now prefer
compatible codes or names and reject nonpositive elapsed time. The existing coarse cross-country,
under-one-hour suspicion heuristic remains explicit; this is not coordinate-based velocity proof.
Pre-token geolocation input and velocity-policy integration remain in #211.

## Authored validation and required acceptance

- 31 focused unit cases cover current/legacy storage, country-code priority, labels, whitespace/case,
  changed locations and unknown data. Four exercise the actual authentication writer with a
  controlled geolocation provider and repository boundaries, including comma/hyphen city names.
- 13 additional unit cases compose the real login scorer and threat detector over controlled history
  and SIEM boundaries, and cover direct compatible-country comparisons and invalid chronology.
- Five cases use the actual API composition, migrated PostgreSQL, authentication writer and
  repository, and registered anomaly facade/behavioral scorer, including isolation from another
  user's baseline and actual login-analysis geographic signals. Only the geolocation provider is controlled. History
  timestamps cover every hour to isolate location scoring. This is backend-service/storage
  integration; it does not establish HTTP sign-in risk input or external geolocation acceptance.
- Existing facade, login analysis, threat-detection and behavioral suites must pass on the corrected
  source. Source/DI/functional evidence must be reconciled with the complete title-level issue scope
  before resolving #212 or recertifying #217.

All 49 new definitions await matching-source hosted execution. No local SDK, build, restore, tests,
database, containers, application server or external geolocation execution was performed.
