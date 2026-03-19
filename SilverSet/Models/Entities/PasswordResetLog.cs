namespace SilverSet.Models.Entities
{
    public class PasswordResetLog
    {
        public int Id { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public DateTime ResetTime { get; set; }
        public bool IsSuccessful { get; set; }
        public string? Note { get; set; }
    }
}