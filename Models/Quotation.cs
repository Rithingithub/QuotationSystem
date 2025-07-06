using System.ComponentModel.DataAnnotations;

namespace QuotationSystem.Models
{
    public class Quotation
    {
        public int QuotationId { get; set; }
        [Required]
        public int UserId { get; set; }
        [Required]
        public string CompanyName { get; set; }
        [Required]
        public string ContactDetails { get; set; }
        [Required]
        [EmailAddress]
        public string Email { get; set; }
        [Required]
        public string Status { get; set; } = "Pending"; // Default to Pending
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public User? User { get; set; } // Navigation property, nullable
        public List<QuotationCourse> QuotationCourses { get; set; } = new List<QuotationCourse>();
        public List<Enquiry> Enquiries { get; set; } = new List<Enquiry>();
    }
}