using FamilyApp.API.Repositories;
using FamilyApp.API.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;

namespace FamilyApp.API.Services
{
    public class AuthService
    {
        private readonly ITokenBlacklistRepository _tokenBlacklistRepository;
        private readonly IUserRepository _userRepository;
        private readonly string _jwtSecret;
        private readonly TimeSpan _tokenExpiration;
        private readonly TimeSpan _refreshTokenExpiration;

        public AuthService(ITokenBlacklistRepository tokenBlacklistRepository, IUserRepository userRepository, IConfiguration configuration)
        {
            _tokenBlacklistRepository = tokenBlacklistRepository;
            _userRepository = userRepository;
            _jwtSecret = configuration["Jwt:SecretKey"];
            _tokenExpiration = TimeSpan.FromHours(1);
            _refreshTokenExpiration = TimeSpan.FromDays(7);
        }

        public async Task<(string AccessToken, string RefreshToken)> GenerateTokensAsync(User user)
        {
            var accessToken = GenerateAccessToken(user);
            var refreshToken = GenerateRefreshToken();

            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiryTime = DateTime.UtcNow.Add(_refreshTokenExpiration);
            await _userRepository.UpdateUserAsync(user);

            return (accessToken, refreshToken);
        }

        private string GenerateAccessToken(User user)
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
                Expires = DateTime.UtcNow.Add(_tokenExpiration),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        private string GenerateRefreshToken()
        {
            return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        }

        public async Task<string> RefreshTokenAsync(string refreshToken)
        {
            var user = await _userRepository.GetUserByRefreshTokenAsync(refreshToken);

            if (user == null || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
            {
                return null;
            }

            var newAccessToken = GenerateAccessToken(user);
            var newRefreshToken = GenerateRefreshToken();

            user.RefreshToken = newRefreshToken;
            user.RefreshTokenExpiryTime = DateTime.UtcNow.Add(_refreshTokenExpiration);
            await _userRepository.UpdateUserAsync(user);

            return newAccessToken;
        }

        public async Task LogoutAsync(string token)
        {
            var expiryDate = DateTime.UtcNow.AddDays(1);
            await _tokenBlacklistRepository.AddTokenAsync(token, expiryDate);
        }

        public async Task<bool> IsTokenValid(string token)
        {
            return !await _tokenBlacklistRepository.IsTokenBlacklistedAsync(token);
        }
    }
}
