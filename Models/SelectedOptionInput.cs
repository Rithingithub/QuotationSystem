using System.ComponentModel.DataAnnotations;

namespace QuotationSystem.Models
{
    public class SelectedOptionInput
    {
        public bool IsSelected { get; set; }
        public int CourseOptionId { get; set; }
        public string SelectedDuration { get; set; } // "Full" or "Half"
        [Range(0.01, double.MaxValue, ErrorMessage = "Full Course Price must be positive.")]
        public decimal? FullCoursePrice { get; set; }
        [Range(0.01, double.MaxValue, ErrorMessage = "Half Course Price must be positive.")]
        public decimal? HalfCoursePrice { get; set; }
    }
}