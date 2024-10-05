using FamilyApp.API.Models;
using MongoDB.Driver;

namespace FamilyApp.API.Repositories
{
    public class TokenBlacklistRepository : ITokenBlacklistRepository
    {
        private readonly IMongoCollection<TokenBlacklist> _blacklist;

        public TokenBlacklistRepository(IMongoDatabase database)
        {
            _blacklist = database.GetCollection<TokenBlacklist>("TokenBlacklist");
        }

        public async Task AddTokenAsync(string token, DateTime expiryDate)
        {
            var blacklistedToken = new TokenBlacklist
            {
                Token = token,
                ExpiryDate = expiryDate
            };
            await _blacklist.InsertOneAsync(blacklistedToken);
        }

        public async Task<bool> IsTokenBlacklistedAsync(string token)
        {
            var count = await _blacklist.CountDocumentsAsync(t => t.Token == token);
            return count > 0;
        }
    }
}
