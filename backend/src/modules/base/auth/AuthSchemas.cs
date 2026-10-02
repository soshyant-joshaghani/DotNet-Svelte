namespace DotnetSvelte.Modules.Base.Auth;

public sealed record Token(string AccessToken, string TokenType = "bearer");

/// <summary>Documents the urlencoded login form in OpenAPI; the endpoint reads the form itself.</summary>
public sealed record LoginForm(string Username, string Password);
