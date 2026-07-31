using System.Collections.Generic;
using System.Threading.Tasks;

namespace MigrationSurface;

public sealed class CustomerId : ValueOf.ValueOf<System.Guid, CustomerId>;

public sealed class NamedOutcome :
    OneOf.OneOfBase<OneOf.OneOf<string, int>>;

public sealed class PublicApi
{
    public NamedOutcome Current { get; } = new();

    public OneOf.OneOf<CustomerId, int> Decide(CustomerId customerId) => new();

    public Task<IReadOnlyList<OneOf.OneOf<CustomerId, int>[]>> NestedAsync() =>
        Task.FromResult<IReadOnlyList<OneOf.OneOf<CustomerId, int>[]>>([]);

    public string this[CustomerId customerId] => string.Empty;
}

public sealed class GenericSurface<T>
    where T : OneOf.OneOfBase<OneOf.OneOf<string, int>>;

internal sealed class HiddenApi
{
    public OneOf.OneOf<string, int> Hidden() => new();
}
