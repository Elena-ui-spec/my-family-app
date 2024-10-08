using FamilyApp.API.Repositories;
using FamilyApp.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Load sensitive data from environment variables or fallback to appsettings.json
var mongoConnectionString = Environment.GetEnvironmentVariable("MONGODB_CONNECTION_STRING") ?? builder.Configuration.GetConnectionString("MongoDb");
var jwtSecretKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY") ?? builder.Configuration["Jwt:SecretKey"];
var encryptionKey = Environment.GetEnvironmentVariable("ENCRYPTION_KEY") ?? builder.Configuration["Encryption:Key"];
var encryptionIV = Environment.GetEnvironmentVariable("ENCRYPTION_IV") ?? builder.Configuration["Encryption:IV"];
var serverBaseUrl = Environment.GetEnvironmentVariable("SERVER_BASE_URL") ?? builder.Configuration["ServerBaseUrl"];
var googleDriveServiceAccountJson = Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS_JSON"); // Get the JSON directly
var googleDriveSharedFolderId = Environment.GetEnvironmentVariable("GOOGLE_DRIVE_SHARED_FOLDER_ID") ?? builder.Configuration["GoogleDrive:SharedFolderId"];

if (string.IsNullOrEmpty(googleDriveServiceAccountJson))
{
    // Get the file path from appsettings.json
    var googleServiceAccountFilePath = builder.Configuration["GoogleDrive:ServiceAccountJsonFilePath"];

    // Ensure the file exists, then read it
    if (File.Exists(googleServiceAccountFilePath))
    {
        googleDriveServiceAccountJson = await File.ReadAllTextAsync(googleServiceAccountFilePath);
    }
    else
    {
        throw new FileNotFoundException($"Google Drive service account JSON file not found at: {googleServiceAccountFilePath}");
    }
}

builder.Services.AddSingleton<IMongoClient, MongoClient>(s => new MongoClient(mongoConnectionString));
builder.Services.AddScoped(s => s.GetRequiredService<IMongoClient>().GetDatabase("MyFamilyApp"));

// Register services and pass the encryption key and IV to the EncryptionService
builder.Services.AddSingleton<EncryptionService>(sp => new EncryptionService(encryptionKey, encryptionIV));
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<IUserRepository, UserRepository>();

builder.Services.AddScoped<MediaService>(sp =>
{
    var mongoClient = sp.GetRequiredService<IMongoClient>();
    var encryptionService = sp.GetRequiredService<EncryptionService>();

    return new MediaService(
        mongoClient,
        serverBaseUrl,
        googleDriveSharedFolderId,
        googleDriveServiceAccountJson, // Use JSON instead of a file path
        encryptionService
    );
});

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ITokenBlacklistRepository, TokenBlacklistRepository>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Allow CORS only for your frontend domain
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigin", builder =>
    {
        builder.WithOrigins("https://ciucureanu-radacini.onrender.com")
               .AllowAnyMethod()
               .AllowAnyHeader()
               .AllowCredentials(); // Allow credentials (cookies, auth)
    });
});
/*builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowLocalhost3000", builder =>
    {
        builder.WithOrigins("http://localhost:3000")
               .AllowAnyMethod()
               .AllowAnyHeader()
               .AllowCredentials();
    });
});*/

// Increase the limits for form data
builder.Services.Configure<FormOptions>(options =>
{
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartBodyLengthLimit = 524288000; // 500 MB
    options.MemoryBufferThreshold = int.MaxValue;
});

// Ensure that Kestrel's request body size limit is increased
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 524288000; // 500 MB
});

var key = Encoding.UTF8.GetBytes(jwtSecretKey); // Using UTF8 to encode the secret key
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = false,
        ValidateAudience = false,
        ClockSkew = TimeSpan.Zero,
        RoleClaimType = ClaimTypes.Role
    };
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            context.Token = context.Request.Cookies["AuthToken"];
            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            var claimsIdentity = context.Principal.Identity as ClaimsIdentity;
            if (claimsIdentity != null)
            {
                var roleClaim = claimsIdentity.FindFirst(ClaimTypes.Role);
                if (roleClaim != null)
                {
                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                    logger.LogInformation($"Role claim in token: {roleClaim.Value}");
                }
            }
            return Task.CompletedTask;
        },
        OnAuthenticationFailed = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogError("Token validation failed", context.Exception);
            return Task.CompletedTask;
        }
    };
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

using (var scope = app.Services.CreateScope())
{
    var userService = scope.ServiceProvider.GetRequiredService<UserService>();
    await userService.EnsureAdminUserExistsAsync();
}

// Middleware pipeline setup
app.UseHttpsRedirection();
app.UseRouting();

// Use CORS with specific policy for your frontend domain
app.UseCors("AllowSpecificOrigin");
//app.UseCors("AllowLocalhost3000");

app.UseAuthentication();
app.UseAuthorization();

app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
});

app.Run();
