namespace QuotationSystem.Models
{
    public class QuotationCoursePrice
    {
        public int QuotationCoursePriceId { get; set; }
        public int QuotationCourseId { get; set; }
        public QuotationCourse QuotationCourse { get; set; }
        public decimal FullCoursePrice { get; set; }
        public decimal HalfCoursePrice { get; set; }
        public bool IsCustomPrice { get; set; } // True if salesperson modified the price
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}