using FamilyApp.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Route("api/[controller]")]
[ApiController]
public class UserController : ControllerBase
{
    private readonly UserService _userService;
    private readonly IWebHostEnvironment _env;
    private readonly string _jwtSecret;

    public UserController(UserService userService, IWebHostEnvironment env)
    {
        _userService = userService;
        _env = env;
        _jwtSecret = "TmVyMjRkYmRaZXNlcnQyU0lNaTZIN2NYRVRlc1pDWWY="; 
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterRequest registerRequest)
    {
        try
        {
            if (registerRequest == null || string.IsNullOrEmpty(registerRequest.Username) || string.IsNullOrEmpty(registerRequest.Password))
            {
                return BadRequest(new { message = "Numele de utilizator și parola sunt obligatorii." });
            }

            var newUser = new User
            {
                Username = registerRequest.Username,
                PasswordHash = registerRequest.Password,
                IsAdmin = false,
                IsApproved = registerRequest.IsInvited
            };

            await _userService.RegisterUserAsync(newUser);
            return Ok(new { message = "Registration successful, pending approval." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }


    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest loginRequest)
    {
        var user = await _userService.FindByUsernameAsync(loginRequest.Username);

        if (user == null || !_userService.VerifyPassword(loginRequest.Password, user.PasswordHash))
        {
            return Unauthorized(new { message = "Invalid credentials" });
        }

        if (!user.IsApproved)
        {
            return Unauthorized(new { message = "Your account is not yet approved." });
        }

        var accessToken = _userService.GenerateJwtToken(user);
        var refreshToken = Guid.NewGuid().ToString(); 
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddMinutes(1); 
        await _userService.UpdateUserAsync(user);

        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            Expires = DateTime.UtcNow.AddHours(1),
            Path = "/"
        };

        Response.Cookies.Append("AuthToken", accessToken, cookieOptions);

        return Ok(new
        {
            accessToken = accessToken,
            refreshToken = refreshToken,
            user = new
            {
                Username = user.Username,
                IsAdmin = user.IsAdmin,
                IsApproved = user.IsApproved
            }
        });
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public IActionResult Logout()
    {
        var token = Request.Cookies["AuthToken"];

        if (string.IsNullOrEmpty(token))
        {
            return Unauthorized(new { message = "No token found in cookies." });
        }
        Response.Cookies.Delete("AuthToken");
        return Ok(new { message = "Logout successful" });
    }

    [HttpGet("pending-users")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetPendingUsers()
    {
        try
        {
            var pendingUsers = await _userService.GetPendingUsersAsync();
            return Ok(pendingUsers);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while retrieving pending users." });
        }
    }

    [HttpPost("approve-user")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ApproveUser([FromBody] string userId)
    {
        try
        {
            await _userService.ApproveUserAsync(userId);
            return Ok(new { message = "User approved successfully" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("all")]
    [Authorize(Roles ="Admin")] 
    public async Task<IActionResult> GetAllUsers()
    {
        try
        {
            var users = await _userService.GetAllUsersAsync();
            return Ok(users);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Internal server error: {ex.Message}" });
        }
    }

    [HttpPost("delete-user")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteUser([FromBody] string userId)
    {
        try
        {
            await _userService.DeleteUserAsync(userId);
            return Ok(new { message = "Utilizatorul a fost șterss." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Internal server error: {ex.Message}" });
        }
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest refreshRequest)
    {
        var user = await _userService.GetUserByRefreshTokenAsync(refreshRequest.RefreshToken);

        if (user == null || IsRefreshTokenExpired(user))
        {
            return Unauthorized(new { message = "Invalid or expired refresh token" });
        }

        var newAccessToken = _userService.GenerateJwtToken(user);
        return Ok(new { accessToken = newAccessToken });
    }

    private bool IsRefreshTokenExpired(string refreshToken)
    {
        var user = _userService.GetUserByRefreshTokenAsync(refreshToken).Result;

        if (user == null || user.RefreshToken != refreshToken)
        {
            return true;
        }

        return DateTime.UtcNow >= user.RefreshTokenExpiryTime;
    }


    private bool IsRefreshTokenExpired(User user)
    {
        return user.RefreshTokenExpiryTime <= DateTime.UtcNow;
    }

}
