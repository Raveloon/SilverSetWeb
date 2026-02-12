namespace SilverSet.Models.Entities
{
    public class LoginLog
    {
        public int Id { get; set; }
        public string Email { get; set; }
        public DateTime LoginTime { get; set; } = DateTime.Now;
        public string IpAddress { get; set; } = string.Empty;
        public bool IsSuccessful { get; set; } 

    }
}
