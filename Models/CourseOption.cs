namespace QuotationSystem.Models
{
    public class CourseOption
    {
        public int CourseOptionId { get; set; }
        public int CourseId { get; set; }
        public Course Course { get; set; }
        public int CourseTypeId { get; set; }
        public CourseType CourseType { get; set; }
        public decimal FullCoursePrice { get; set; }
        public decimal HalfCoursePrice { get; set; }
    }
}