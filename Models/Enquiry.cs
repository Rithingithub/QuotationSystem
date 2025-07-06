namespace QuotationSystem.Models
{
    public class Enquiry
    {
        public int EnquiryId { get; set; }

        // Enquirer Info
        public string Name { get; set; }
        public string Email { get; set; }
        public string Message { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Relation to Quotation
        public int QuotationId { get; set; }
        public Quotation Quotation { get; set; }
    }
}
