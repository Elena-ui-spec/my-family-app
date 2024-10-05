using FamilyApp.API.Models;

namespace FamilyApp.API.Repositories
{
    public interface IUserRepository
    {
        Task AddUserAsync(User user);
        Task<User> GetUserByUsernameAsync(string username);
        Task<List<User>> GetPendingUsersAsync();
        Task ApproveUserAsync(string userId);
        Task<User> GetUserByIdAsync(string userId);
        Task<List<User>> GetAllUsersAsync();
        Task MakeAdminUserAsync(string userId);
        Task DeleteUserAsync(string userId); 
        Task<User> GetAdminUserAsync();
        Task UpdateUserAsync(User user);
        Task<User> GetUserByRefreshTokenAsync(string refreshToken);
    }
}
