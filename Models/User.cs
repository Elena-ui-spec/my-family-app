using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace FamilyApp.API.Models
{
    public class User
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        [BsonElement("username")]
        public string Username { get; set; }

        [BsonElement("passwordHash")]
        public string PasswordHash { get; set; }

        [BsonElement("isAdmin")]
        public bool IsAdmin { get; set; } = false;

        [BsonElement("isApproved")]
        public bool IsApproved { get; set; } = false;

        [BsonElement("refreshToken")]
        public string RefreshToken { get; set; }

        [BsonElement("refreshTokenExpiryTime")]
        public DateTime RefreshTokenExpiryTime { get; set; }
    }
}
