namespace NewDialer.Contracts.Auth;

public sealed record AdminSignupRequest(
    string FullName,
    string Email,
    string CompanyName,
    string Password,
    string PhoneNumber);
