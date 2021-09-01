
using Microsoft.JSInterop;
using System.Diagnostics.CodeAnalysis;

namespace BlazeBin.Server.Services;
public class DummyJSRuntime : IJSRuntime
{
    public ValueTask<TValue> InvokeAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)] TValue>
        (string identifier, object?[]? args)
    {
        return new ValueTask<TValue>((TValue)default!);
    }

    public ValueTask<TValue> InvokeAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)] TValue>
        (string identifier, CancellationToken cancellationToken, object?[]? args)
    {
        return new ValueTask<TValue>((TValue)default!);
    }
}
