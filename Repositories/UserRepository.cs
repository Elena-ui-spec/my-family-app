using FamilyApp.API.Models;
using FamilyApp.API.Services;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FamilyApp.API.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly IMongoCollection<User> _users;
        private readonly EncryptionService _encryptionService;

        public UserRepository(IMongoDatabase database, EncryptionService encryptionService)
        {
            _users = database.GetCollection<User>("Users");
            _encryptionService = encryptionService;
        }

        public async Task<User> GetAdminUserAsync()
        {
            var filter = Builders<User>.Filter.Eq(u => u.IsAdmin, true);
            return await _users.Find(filter).FirstOrDefaultAsync();
        }

        public async Task AddUserAsync(User user)
        {
            var encodedUser = EncodeUser(user);
            var existingUser = await _users.Find(u => u.Username == encodedUser.Username).FirstOrDefaultAsync();
            if (existingUser != null)
            {
                throw new Exception("Există deja un utilizator cu acest nume.");
            }
            await _users.InsertOneAsync(encodedUser);
        }

        public async Task<User> GetUserByUsernameAsync(string username)
        {
            var encodedUsername = _encryptionService.Encrypt(username);
            var user = await _users.Find(u => u.Username == encodedUsername).FirstOrDefaultAsync();
            return DecodeUser(user);
        }

        public async Task<User> GetUserByRefreshTokenAsync(string refreshToken)
        {
            var filter = Builders<User>.Filter.Eq(u => u.RefreshToken, refreshToken);
            var user = await _users.Find(filter).FirstOrDefaultAsync();
            return user;
        }


        public async Task<List<User>> GetPendingUsersAsync()
        {
            var users = await _users.Find(user => !user.IsApproved).ToListAsync();
            return DecodeUsers(users);
        }

        public async Task<User> GetUserByIdAsync(string userId)
        {
            var user = await _users.Find(user => user.Id == userId).FirstOrDefaultAsync();
            return DecodeUser(user);
        }

        public async Task ApproveUserAsync(string userId)
        {
            var update = Builders<User>.Update.Set(user => user.IsApproved, true);
            await _users.UpdateOneAsync(user => user.Id == userId, update);
        }

        public async Task MakeAdminUserAsync(string userId)
        {
            var update = Builders<User>.Update.Set(user => user.IsAdmin, true);
            await _users.UpdateOneAsync(user => user.Id == userId, update);
        }

        public async Task UpdateUserAsync(User user)
        {
            var filter = Builders<User>.Filter.Eq(u => u.Id, user.Id);
            await _users.ReplaceOneAsync(filter, user);
        }

        public async Task<List<User>> GetAllUsersAsync()
        {
            var users = await _users.Find(user => !user.IsAdmin).ToListAsync();
            return DecodeUsers(users);
        }

        private User EncodeUser(User user)
        {
            user.Username = _encryptionService.Encrypt(user.Username);
            user.PasswordHash = _encryptionService.Encrypt(user.PasswordHash);
            return user;
        }

        private User DecodeUser(User user)
        {
            if (user == null) return null;
            user.Username = _encryptionService.Decrypt(user.Username);
            user.PasswordHash = _encryptionService.Decrypt(user.PasswordHash);
            return user;
        }

        private List<User> DecodeUsers(List<User> users)
        {
            for (int i = 0; i < users.Count; i++)
            {
                users[i] = DecodeUser(users[i]);
            }
            return users;
        }

        public async Task DeleteUserAsync(string userId)
        {
            await _users.DeleteOneAsync(u => u.Id == userId);
        }
    }
}
