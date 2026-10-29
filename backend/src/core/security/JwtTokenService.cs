using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DotnetSvelte.Core.Config;
using Microsoft.AspNetCore.WebUtilities;

namespace DotnetSvelte.Core.Security;

/// <summary>HS256 JWT with claims sub and exp. Hand-rolled so any SECRET_KEY length interoperates with the other FoxG backends.</summary>
public sealed class JwtTokenService(Settings settings)
{
    private static readonly string Header = Base64UrlTextEncoder.Encode("""{"alg":"HS256","typ":"JWT"}"""u8.ToArray());

    public string Create(Guid subject, TimeSpan lifetime)
    {
        var exp = DateTimeOffset.UtcNow.Add(lifetime).ToUnixTimeSeconds();
        var payload = JsonSerializer.SerializeToUtf8Bytes(new { sub = subject.ToString(), exp });
        var signingInput = $"{Header}.{Base64UrlTextEncoder.Encode(payload)}";
        return $"{signingInput}.{Base64UrlTextEncoder.Encode(Sign(signingInput))}";
    }

    public Guid? Validate(string token)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3) return null;

            var header = JsonDocument.Parse(Base64UrlTextEncoder.Decode(parts[0])).RootElement;
            if (header.GetProperty("alg").GetString() != "HS256") return null;

            var expected = Sign($"{parts[0]}.{parts[1]}");
            if (!CryptographicOperations.FixedTimeEquals(expected, Base64UrlTextEncoder.Decode(parts[2]))) return null;

            var payload = JsonDocument.Parse(Base64UrlTextEncoder.Decode(parts[1])).RootElement;
            if (payload.GetProperty("exp").GetInt64() <= DateTimeOffset.UtcNow.ToUnixTimeSeconds()) return null;

            return Guid.TryParse(payload.GetProperty("sub").GetString(), out var id) ? id : null;
        }
        catch (Exception ex) when (ex is JsonException or FormatException or KeyNotFoundException
                                       or InvalidOperationException or ArgumentException)
        {
            return null;
        }
    }

    private byte[] Sign(string input) =>
        HMACSHA256.HashData(Encoding.UTF8.GetBytes(settings.SecretKey), Encoding.ASCII.GetBytes(input));
}
