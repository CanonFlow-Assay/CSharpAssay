# Report-only migration inventory

`cs-assay migrate --report` inventories public OneOf and ValueOf exposure. It
does not edit source, select a replacement representation, or describe a
migration as mechanical find-and-replace.

```text
cs-assay migrate --report MySolution.slnx \
  --json artifacts/migration/report.json
```

The optional output path must end in `.json`. Workspace failure returns exit 3
and remains visible in `failures`; incomplete loading is never an empty success.

## What the report records

Each exposure binds its recommendation to:

- the complete public API signature and affected role;
- source path, line, and column;
- exposed type and recognized metadata identity;
- representation package and declaring API assembly identities/versions;
- project and target framework;
- representation-specific risks;
- compatibility/native behavior obligations;
- System.Text.Json, EF Core, ASP.NET Core/OpenAPI, and NativeAOT qualification
  obligations.

Source SHA-256 values identify the documents used by the analysis. Repeated
runs over the same build are byte deterministic. The schema is
[`eng/schemas/migration-report.schema.json`](../eng/schemas/migration-report.schema.json).

## Interpreting recommendations

A public OneOf or ValueOf type may already be part of source, binary, wire,
database, reflection, or generic contracts. The report therefore recommends a
baseline and reviewed decision for each exact API. It never assumes that native
unions improve allocation or that a record struct is stack allocated.

`required-unqualified` means the integration needs executed evidence before a
migration claim. `context-required` means CSharpAssay cannot infer from a public
signature whether that framework boundary is used. Neither status is a pass.

OneOf and ValueOf recognition is observation-only. Vogen, dunet, and
Thinktecture adapters remain disabled because no exact package version has
passed the package, generated-symbol, serialization, EF, ASP.NET, and AOT
corpus. Their popularity is not qualification evidence.

## Limitations

This is a compiled-symbol public API inventory, not a binary compatibility,
benchmark, serializer, ORM, endpoint, or NativeAOT executor. Reflection-only
use, dynamically loaded contracts, external consumer behavior, and unbuilt
target frameworks require separate evidence. Follow the emitted adapter
obligations and retain a rollback plan before changing a representation.
