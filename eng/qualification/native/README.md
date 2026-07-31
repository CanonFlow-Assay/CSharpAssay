# Native union qualification lane

This directory intentionally contains no passing native-union claim.

The lane requires:

1. an installed, pinned .NET 11 SDK;
2. `<LangVersion>preview</LangVersion>` while unions remain preview;
3. Roslyn symbol and operation snapshots;
4. compiler exhaustiveness/nullability positive and negative specimens;
5. JSON, ASP.NET, OpenAPI, AOT, reflection, and multi-TFM round trips;
6. byte-stable evidence across repeated runs.

Until those obligations execute, `UnionCapabilities` fails closed and
`--profile native` produces missing evidence.
