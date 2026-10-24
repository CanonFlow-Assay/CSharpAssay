# SARIF schema provenance

The unmodified schema is the OASIS SARIF 2.1.0 schema with approved errata:
https://docs.oasis-open.org/sarif/sarif/v2.1.0/errata01/os/schemas/sarif-schema-2.1.0.json

Downloaded 2026-10-23; SHA-256
`c3b4bb2d6093897483348925aaa73af03b3e3f4bd4ca38cef26dcb4212a2682e`.
The schema identifies JSON Schema draft-04. Tests use pinned NJsonSchema 11.6.1
for actual schema validation and also check execution, rule linkage, URI,
suppression and presentation semantics. A deliberately invalid invocation type
must fail validation. Tests do not download a schema or vulnerability database.

OASIS specification and copyright notices:
https://docs.oasis-open.org/sarif/sarif/v2.1.0/errata01/os/sarif-v2.1.0-errata01-os-complete.html
