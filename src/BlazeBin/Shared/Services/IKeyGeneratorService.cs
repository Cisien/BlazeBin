using System;

namespace BlazeBin.Shared.Services
{
    public interface IKeyGeneratorService
    {
        ReadOnlySpan<char> GenerateKey(int length);
    }
}