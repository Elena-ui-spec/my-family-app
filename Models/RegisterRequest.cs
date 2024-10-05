namespace FamilyApp.API.Models
{
    public class RegisterRequest
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public bool IsInvited { get; set; }
    }
}
