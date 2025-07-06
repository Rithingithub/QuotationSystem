using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuotationSystem.Models;

namespace QuotationSystem.Controllers
{
    public class QuotationController : Controller
    {
        private readonly AppDbContext _context;

        public QuotationController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Create()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            ViewBag.Courses = _context.Courses
                .Include(c => c.CourseOptions)
                    .ThenInclude(o => o.CourseType)
                .ToList();
            return View(new Quotation());
        }

        [HttpPost]
        public async Task<IActionResult> Create(Quotation quotation, List<SelectedOptionInput> selectedOptions)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return RedirectToAction("Login", "Account");

            // Clear ModelState errors for unselected options
            if (selectedOptions != null)
            {
                for (int i = 0; i < selectedOptions.Count; i++)
                {
                    if (!selectedOptions[i].IsSelected)
                    {
                        ModelState.Remove($"selectedOptions[{i}].FullCoursePrice");
                        ModelState.Remove($"selectedOptions[{i}].HalfCoursePrice");
                    }
                }
            }

            // Ensure at least one option is selected
            if (selectedOptions == null || !selectedOptions.Any(o => o.IsSelected))
            {
                ModelState.AddModelError("", "At least one course option must be selected.");
            }

            // Validate prices for selected options
            if (selectedOptions != null)
            {
                foreach (var option in selectedOptions.Where(o => o.IsSelected))
                {
                    if (!option.FullCoursePrice.HasValue || option.FullCoursePrice <= 0 ||
                        !option.HalfCoursePrice.HasValue || option.HalfCoursePrice <= 0)
                    {
                        ModelState.AddModelError("", $"Full and Half Course Prices must be positive for option {option.CourseOptionId}.");
                    }
                }
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                ViewBag.ModelErrors = errors;
                ViewBag.Courses = _context.Courses
                    .Include(c => c.CourseOptions)
                        .ThenInclude(o => o.CourseType)
                    .ToList();
                return View(quotation);
            }

            quotation.UserId = userId.Value;
            quotation.Status = "Pending";
            quotation.QuotationCourses = new List<QuotationCourse>();
            quotation.Enquiries = new List<Enquiry>();
            _context.Quotations.Add(quotation);
            await _context.SaveChangesAsync();

            foreach (var option in selectedOptions.Where(o => o.IsSelected))
            {
                var courseOption = _context.CourseOptions
                    .FirstOrDefault(co => co.CourseOptionId == option.CourseOptionId);

                if (courseOption != null)
                {
                    courseOption.FullCoursePrice = option.FullCoursePrice!.Value;
                    courseOption.HalfCoursePrice = option.HalfCoursePrice!.Value;
                    _context.CourseOptions.Update(courseOption);

                    _context.QuotationCourses.Add(new QuotationCourse
                    {
                        QuotationId = quotation.QuotationId,
                        CourseOptionId = option.CourseOptionId
                    });
                }
            }

            await _context.SaveChangesAsync();
            return RedirectToAction("List");
        }

        public IActionResult List()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var quotations = _context.Quotations
                .Where(q => q.UserId == userId)
                .Include(q => q.QuotationCourses)
                    .ThenInclude(qc => qc.CourseOption)
                        .ThenInclude(co => co.Course)
                .Include(q => q.QuotationCourses)
                    .ThenInclude(qc => qc.CourseOption)
                        .ThenInclude(co => co.CourseType)
                .OrderByDescending(q => q.CreatedAt)
                .ToList();

            return View(quotations);
        }

        public IActionResult Review()
        {
            var role = HttpContext.Session.GetString("UserRole");
            if (role != "Admin") return RedirectToAction("Login", "Account");

            var pendingQuotations = _context.Quotations
                .Where(q => q.Status == "Pending")
                .Include(q => q.User)
                .Include(q => q.QuotationCourses)
                    .ThenInclude(qc => qc.CourseOption)
                        .ThenInclude(co => co.Course)
                .Include(q => q.QuotationCourses)
                    .ThenInclude(qc => qc.CourseOption)
                        .ThenInclude(co => co.CourseType)
                .OrderBy(q => q.CreatedAt)
                .ToList();

            return View(pendingQuotations);
        }

        [HttpPost]
        public IActionResult UpdateStatus(int quotationId, string actionType)
        {
            var quotation = _context.Quotations.Find(quotationId);
            if (quotation != null && quotation.Status == "Pending")
            {
                quotation.Status = actionType == "Approve" ? "Approved" : "Declined";
                _context.SaveChanges();
            }
            return RedirectToAction("Review");
        }
    }
}