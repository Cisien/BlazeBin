using System.Security.Cryptography;

namespace BlazeBin.Shared.Services
{
    public class AlphaKeyGeneratorService : IKeyGeneratorService
    {
        private const string keyspace = "abcdefghijklmnopqrstuvwxyz";

        public ReadOnlySpan<char> GenerateKey(int length)
        {
            var seed = RandomNumberGenerator.GetInt32(int.MaxValue);
            var prng = new Random(seed);

            var key = new Span<char>(new char[length]);

            for (var i = 0; i < key.Length; i++)
            {
                key[i] = keyspace[prng.Next(keyspace.Length)];
            }

            return key;
        }
    }
}
