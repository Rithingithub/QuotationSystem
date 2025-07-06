namespace QuotationSystem.Models
{
    public class Notification
    {
        public int NotificationId { get; set; }
        public int? UserId { get; set; }
        public int QuotationId { get; set; }
        public string Message { get; set; }
        public bool IsSeen { get; set; } = false;

        public User? User { get; set; }
        public Quotation Quotation { get; set; }
    }

}