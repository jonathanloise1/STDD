using System.Security.Cryptography;
using System.Text;

namespace MyApp.Monitor.Investigator;

/// <summary>The rules text of the protocol as the pack carries it (<c>protocol.md</c>, the block between the markers, trimmed).</summary>
public static class ProtocolBlock
{
    /// <summary>
    /// SHA-256 of the block as <c>models/model.lock</c> records it: the bytes between the markers (models.md 8.1), which in
    /// <c>support.agent.md</c> are one line break, the text and one line break. The pack keeps the trimmed text; this puts the breaks back.
    /// </summary>
    public static string Sha256(string packText) =>
        Convert.ToHexString(SHA256.HashData(new UTF8Encoding(false).GetBytes("\n" + packText.Trim() + "\n"))).ToLowerInvariant();

    /// <summary>The system message: the block without the surrounding line breaks, LF line ends.</summary>
    public static string SystemText(string packText) => packText.Replace("\r\n", "\n", StringComparison.Ordinal).Trim('\n');
}
