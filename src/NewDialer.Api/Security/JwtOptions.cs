namespace NewDialer.Api.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "NewDialer";

    public string Audience { get; set; } = "NewDialer.Desktop";

    public string SigningKey { get; set; } = "replace-this-with-a-long-random-signing-key-32chars";

    public int AccessTokenMinutes { get; set; } = 480;
}
