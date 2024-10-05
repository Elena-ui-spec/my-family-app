namespace FamilyApp.API.Models
{
    public class TokenBlacklist
    {
        public string Id { get; set; }
        public string Token { get; set; }
        public DateTime ExpiryDate { get; set; }
    }

}
