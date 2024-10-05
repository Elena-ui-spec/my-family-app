using FamilyApp.API.Models;
using FamilyApp.API.Repositories;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Configuration;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FamilyApp.API.Services;

public class UserService
{
    private readonly EncryptionService _encryptionService;
    private readonly IUserRepository _userRepository;
    private readonly IConfiguration _configuration;
    private readonly string _jwtSecret;
    private readonly string _adminUsername;
    private readonly string _adminPassword;

    public UserService(EncryptionService encryptionService, IUserRepository userRepository, IConfiguration configuration)
    {
        _encryptionService = encryptionService;
        _userRepository = userRepository;
        _configuration = configuration;

        // Load the JWT secret and admin credentials from appsettings
        _jwtSecret = _configuration["Jwt:SecretKey"];
        _adminUsername = _configuration["Admin:Username"];
        _adminPassword = _configuration["Admin:Password"];
    }

    public string EncryptPassword(string password)
    {
        return _encryptionService.Encrypt(password);
    }

    public bool VerifyPassword(string plainTextPassword, string encryptedPassword)
    {
        plainTextPassword = _encryptionService.Decrypt(plainTextPassword);
        var decryptedPassword = _encryptionService.Decrypt(encryptedPassword);
        return plainTextPassword == decryptedPassword;
    }

    public string GenerateJwtToken(User user)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_jwtSecret);
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.IsAdmin ? "Admin" : "User")
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddDays(100),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);

        // Support for refresh token
        var refreshToken = Guid.NewGuid().ToString();
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(100);
        user.Username = _encryptionService.Encrypt(user.Username);
        user.PasswordHash = _encryptionService.Encrypt(user.PasswordHash);

        _userRepository.UpdateUserAsync(user).Wait();

        return tokenHandler.WriteToken(token);
    }

    public async Task RegisterUserAsync(User newUser)
    {
        await _userRepository.AddUserAsync(newUser);
    }

    public async Task<User> FindByUsernameAsync(string username)
    {
        return await _userRepository.GetUserByUsernameAsync(username);
    }

    public async Task<List<User>> GetPendingUsersAsync()
    {
        return await _userRepository.GetPendingUsersAsync();
    }

    public async Task ApproveUserAsync(string userId)
    {
        var user = await _userRepository.GetUserByIdAsync(userId);
        if (user == null)
        {
            throw new Exception("User not found");
        }

        if (user.IsApproved)
        {
            throw new Exception("User is already approved");
        }

        await _userRepository.ApproveUserAsync(userId);
    }

    public async Task MakeAdminUserAsync(string userId)
    {
        var user = await _userRepository.GetUserByIdAsync(userId);
        if (user == null)
        {
            throw new Exception("User not found");
        }

        if (user.IsAdmin)
        {
            throw new Exception("User is already admin");
        }

        await _userRepository.MakeAdminUserAsync(userId);
    }

    public async Task<List<User>> GetAllUsersAsync()
    {
        return await _userRepository.GetAllUsersAsync();
    }

    public async Task<User> GetUserByIdAsync(string userId)
    {
        var user = await _userRepository.GetUserByIdAsync(userId);

        if (user == null)
        {
            throw new Exception("Utilizatorul nu a fost găsit.");
        }

        return user;
    }

    public async Task DeleteUserAsync(string userId)
    {
        var user = await _userRepository.GetUserByIdAsync(userId);

        if (user == null)
        {
            throw new Exception("Utilizatorul nu a fost găsit.");
        }

        if (user.IsAdmin)
        {
            throw new InvalidOperationException("Acest utilizator nu poate fi șters.");
        }

        await _userRepository.DeleteUserAsync(userId);
    }

    public async Task EnsureAdminUserExistsAsync()
    {
        var adminUser = await _userRepository.GetAdminUserAsync();
        if (adminUser == null)
        {
            var defaultAdmin = new User
            {
                Username = _adminUsername,
                PasswordHash = _encryptionService.Encrypt(_adminPassword),
                IsAdmin = true,
                IsApproved = true
            };

            await _userRepository.AddUserAsync(defaultAdmin);
        }
    }

    public async Task UpdateUserAsync(User user)
    {
        await _userRepository.UpdateUserAsync(user);
    }

    public async Task<User> GetUserByRefreshTokenAsync(string refreshToken)
    {
        return await _userRepository.GetUserByRefreshTokenAsync(refreshToken);
    }
}
