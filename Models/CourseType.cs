namespace QuotationSystem.Models
{
    public class CourseType
    {
        public int CourseTypeId { get; set; }
        public string TypeName { get; set; }  // e.g., "VIP", "Normal"

        public ICollection<CourseOption> CourseOptions { get; set; }
    }

}
