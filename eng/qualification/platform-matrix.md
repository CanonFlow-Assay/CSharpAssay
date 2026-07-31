# Platform qualification matrix

| Capability | Stable lane | Native-preview lane |
| --- | --- | --- |
| SDK | 10.0.301 | unavailable |
| Target | net10.0 plus analyzer netstandard2.0 | net11.0 |
| Language | C# 14 | preview |
| Roslyn packages | 5.6.0 | same adapter, not qualified |
| `.csproj` loading | required | pending |
| `.sln` loading | required | pending |
| `.slnx` loading | required | pending |
| all-TFM enumeration | literal SDK-style TFMs implemented | pending preview execution |
| native union symbol surface | fails closed when absent | pending installed SDK |
| release authority | structurally supported, rules not admitted | forbidden |

The absence of a native SDK is evidence, not a compatibility success. Native
qualification must run in an isolated preview job and cannot block the stable
lane until project policy explicitly promotes it.
