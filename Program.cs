using FamilyApp.API.Repositories;
using FamilyApp.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Load sensitive data from appsettings.json
var mongoConnectionString = builder.Configuration.GetConnectionString("MongoDb");
var jwtSecretKey = builder.Configuration["Jwt:SecretKey"];
var encryptionKey = builder.Configuration["Encryption:Key"];
var encryptionIV = builder.Configuration["Encryption:IV"];
var serverBaseUrl = builder.Configuration["ServerBaseUrl"];
var googleDriveServiceAccountJsonPath = builder.Configuration["GoogleDrive:ServiceAccountJsonPath"];
var googleDriveSharedFolderId = builder.Configuration["GoogleDrive:SharedFolderId"];

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
        googleDriveServiceAccountJsonPath,
        encryptionService
    );
});

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ITokenBlacklistRepository, TokenBlacklistRepository>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowLocalhost3000", builder =>
    {
        builder.WithOrigins("http://localhost:3000")
               .AllowAnyMethod()
               .AllowAnyHeader()
               .AllowCredentials();
    });
});

// Increase the limits for form data
builder.Services.Configure<FormOptions>(options =>
{
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartBodyLengthLimit = 524288000; // 500 MB
    options.MemoryBufferThreshold = int.MaxValue;
});

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 524288000; // 500 MB
});

var key = Encoding.ASCII.GetBytes(jwtSecretKey);
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
        RoleClaimType = System.Security.Claims.ClaimTypes.Role
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

app.UseHttpsRedirection();
app.UseCors("AllowLocalhost3000");

// Serve static files from the media directory
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
        Path.Combine(builder.Environment.ContentRootPath, "media")),
    RequestPath = "/media"
});

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
});

app.Run();
