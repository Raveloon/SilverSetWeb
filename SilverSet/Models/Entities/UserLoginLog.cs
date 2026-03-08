namespace SilverSet.Models.Entities
{
    public class UserLoginLog
    {
        public int Id { get; set; }
        public string UserName { get; set; }
        public DateTime LoginTime { get; set; }
        public string? Email { get; set; }
        public bool IsSuccessful { get; set; }
        public string IpAddress { get; set; }
    }
}
