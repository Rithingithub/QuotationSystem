namespace QuotationSystem.Models
{
    public class Course
    {
        public int CourseId { get; set; }
        public string CourseName { get; set; }
        public string VehicleType { get; set; }  // e.g., "Bike", "Car"

        public ICollection<CourseOption> CourseOptions { get; set; }
    }


}