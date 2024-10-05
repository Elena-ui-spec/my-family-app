namespace FamilyApp.API.Repositories
{
    public interface ITokenBlacklistRepository
    {
        Task AddTokenAsync(string token, DateTime expiryDate);
        Task<bool> IsTokenBlacklistedAsync(string token);
    }
}
