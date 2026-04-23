namespace PlannerApi.Services;

public class PasswordService : IPasswordService
{
    public string HashPassword(string password) => BCrypt.Net.BCrypt.HashPassword(password);

    public bool VerifyPassword(string hash, string password)
    {
        if (string.IsNullOrWhiteSpace(hash)) return false;
        if (hash.StartsWith("plain:", StringComparison.OrdinalIgnoreCase))
            return string.Equals(hash[6..], password, StringComparison.Ordinal);
        return BCrypt.Net.BCrypt.Verify(password, hash);
    }
}
