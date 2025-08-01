namespace QuotationSystem.Models
{
    public class QuotationCourse
    {
        public int QuotationCourseId { get; set; }
        public int QuotationId { get; set; }
        public Quotation Quotation { get; set; }
        public int CourseOptionId { get; set; }
        public CourseOption CourseOption { get; set; }

        // Add this relationship to store custom pricing
        public QuotationCoursePrice QuotationCoursePrice { get; set; }
    }
}