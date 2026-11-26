using System.Security.Cryptography;
using System.Text;

namespace AgentMon.Server;

/// <summary>The write key (collectors) and optional read-only key (dashboards).</summary>
public sealed class Keys
{
    public const int MinimumLength = 32;
    public const string Header = "X-AgentMon-Key";

    private readonly byte[] write;
    private readonly byte[] read;

    private Keys(string writeKey, string? readKey)
    {
        write = Hash(writeKey);
        read = Hash(string.IsNullOrEmpty(readKey) ? writeKey : readKey);
    }

    /// <summary>Checks the keys and returns them, or returns null with a message saying what to fix.</summary>
    public static Keys? Load(string? writeKey, string? readKey, out string? problem)
    {
        problem = null;
        if (string.IsNullOrEmpty(writeKey) || writeKey.Length < MinimumLength)
            problem = $"Set AGENTMON_API_KEY to a random secret of at least {MinimumLength} characters.";
        else if (!string.IsNullOrEmpty(readKey) && (readKey.Length < MinimumLength || readKey == writeKey))
            problem = $"Set AGENTMON_READ_KEY to a different random secret of at least {MinimumLength} characters, or leave it unset.";
        return problem is null ? new Keys(writeKey!, readKey) : null;
    }

    public bool CanWrite(string? supplied) => Matches(supplied, write);

    /// <summary>Reading accepts the read key, or the write key when no read key is set.</summary>
    public bool CanRead(string? supplied) => Matches(supplied, read);

    private static bool Matches(string? supplied, byte[] expected) =>
        !string.IsNullOrEmpty(supplied) && CryptographicOperations.FixedTimeEquals(Hash(supplied), expected);

    private static byte[] Hash(string value) => SHA256.HashData(Encoding.UTF8.GetBytes(value));
}
