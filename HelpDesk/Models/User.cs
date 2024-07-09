namespace HelpdeskApp.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Nume { get; set; }
        public string Parola { get; set; }
        public string Name { get; set; }
        public DateTime? LastLoginTimestamp { get; set; }
        public DateTime? LastFailedLogin { get; set; }
        public int? FailedLoginsCount { get; set; }
        public DateTime InsTime { get; set; }
        public DateTime? ModTime { get; set; }
        public int? InsUserId { get; set; }
        public int? ModUserId { get; set; }
        public bool IsDeleted { get; set; }
    }
}
