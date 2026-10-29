using System.Text.RegularExpressions;

namespace DotnetSvelte.Core.Errors;

public static partial class Validate
{
    public static string Length(string? value, string field, int min, int max)
    {
        if (value is null) throw AppException.Invalid($"{field} is required");
        if (value.Length < min || value.Length > max)
            throw AppException.Invalid($"{field} must be between {min} and {max} characters");
        return value;
    }

    public static void MaxLength(string? value, string field, int max)
    {
        if (value is not null && value.Length > max)
            throw AppException.Invalid($"{field} must be at most {max} characters");
    }

    public static string Email(string? value, string field = "email")
    {
        var v = Length(value, field, 3, 255);
        if (!EmailPattern().IsMatch(v)) throw AppException.Invalid($"{field} is not a valid email address");
        return v;
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailPattern();
}
