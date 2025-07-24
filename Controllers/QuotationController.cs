using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Reporting.NETCore;
using QuotationSystem.Models;
using System.Data;

namespace QuotationSystem.Controllers
{
    public class QuotationController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly string _ssrsServerUrl = "http://your-ssrs-server/ReportServer";
        private readonly string _reportPath = "/Reports/QuotationReport";

        public QuotationController(AppDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
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

            if (selectedOptions == null || !selectedOptions.Any(o => o.IsSelected))
            {
                ModelState.AddModelError("", "At least one course option must be selected.");
            }

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
                    // Create QuotationCourse
                    var quotationCourse = new QuotationCourse
                    {
                        QuotationId = quotation.QuotationId,
                        CourseOptionId = option.CourseOptionId
                    };
                    _context.QuotationCourses.Add(quotationCourse);
                    await _context.SaveChangesAsync(); // Save to get the ID

                    // Check if prices are different from default (custom pricing)
                    bool isCustomPrice = option.FullCoursePrice.Value != courseOption.FullCoursePrice ||
                                       option.HalfCoursePrice.Value != courseOption.HalfCoursePrice;

                    // Create QuotationCoursePrice record
                    var quotationCoursePrice = new QuotationCoursePrice
                    {
                        QuotationCourseId = quotationCourse.QuotationCourseId,
                        FullCoursePrice = option.FullCoursePrice.Value,
                        HalfCoursePrice = option.HalfCoursePrice.Value,
                        IsCustomPrice = isCustomPrice
                    };
                    _context.QuotationCoursePrices.Add(quotationCoursePrice);
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
                .Include(q => q.QuotationCourses)
                    .ThenInclude(qc => qc.QuotationCoursePrice) // Include custom pricing
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
                .Include(q => q.QuotationCourses)
                    .ThenInclude(qc => qc.QuotationCoursePrice) // Include custom pricing for review
                .OrderBy(q => q.CreatedAt)
                .ToList();

            return View(pendingQuotations);
        }

        [HttpGet]
        public IActionResult Approved(string searchTerm)
        {
            var role = HttpContext.Session.GetString("UserRole");
            if (role != "Admin") return RedirectToAction("Login", "Account");

            var approvedQuotations = _context.Quotations
                .Where(q => q.Status == "Approved")
                .Include(q => q.User)
                .Include(q => q.QuotationCourses)
                    .ThenInclude(qc => qc.CourseOption)
                        .ThenInclude(co => co.Course)
                .Include(q => q.QuotationCourses)
                    .ThenInclude(qc => qc.CourseOption)
                        .ThenInclude(co => co.CourseType)
                .Include(q => q.QuotationCourses)
                    .ThenInclude(qc => qc.QuotationCoursePrice) // Include custom pricing
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchTerm))
            {
                approvedQuotations = approvedQuotations.Where(q =>
                    q.CompanyName.Contains(searchTerm) ||
                    q.Email.Contains(searchTerm) ||
                    q.ContactDetails.Contains(searchTerm)
                );
            }

            return View(approvedQuotations.OrderByDescending(q => q.CreatedAt).ToList());
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

        [HttpGet]
        public IActionResult DownloadQuotationPdf(int quotationId)
        {
            var role = HttpContext.Session.GetString("UserRole");
            if (role != "Admin") return RedirectToAction("Login", "Account");

            // Fetch quotation data with custom pricing
            var quotation = _context.Quotations
                .Where(q => q.QuotationId == quotationId)
                .Include(q => q.User)
                .Include(q => q.QuotationCourses)
                    .ThenInclude(qc => qc.CourseOption)
                        .ThenInclude(co => co.Course)
                .Include(q => q.QuotationCourses)
                    .ThenInclude(qc => qc.CourseOption)
                        .ThenInclude(co => co.CourseType)
                .Include(q => q.QuotationCourses)
                    .ThenInclude(qc => qc.QuotationCoursePrice) // Include custom pricing
                .FirstOrDefault();

            if (quotation == null)
            {
                return NotFound("Quotation not found.");
            }

            // Create DataTable for report
            DataTable dt = new DataTable("QuotationData");
            dt.Columns.Add("QuotationId", typeof(int));
            dt.Columns.Add("CompanyName", typeof(string));
            dt.Columns.Add("ContactDetails", typeof(string));
            dt.Columns.Add("Email", typeof(string));
            dt.Columns.Add("Status", typeof(string));
            dt.Columns.Add("CreatedAt", typeof(DateTime));
            dt.Columns.Add("UserName", typeof(string));
            dt.Columns.Add("CourseName", typeof(string));
            dt.Columns.Add("VehicleType", typeof(string));
            dt.Columns.Add("CourseTypeName", typeof(string));
            dt.Columns.Add("FullCoursePrice", typeof(decimal));
            dt.Columns.Add("HalfCoursePrice", typeof(decimal));
            dt.Columns.Add("IsCustomPrice", typeof(bool)); // Add this to show if custom pricing was used

            foreach (var qc in quotation.QuotationCourses)
            {
                // Use custom pricing if available, otherwise use default from CourseOption
                var fullPrice = qc.QuotationCoursePrice?.FullCoursePrice ?? qc.CourseOption.FullCoursePrice;
                var halfPrice = qc.QuotationCoursePrice?.HalfCoursePrice ?? qc.CourseOption.HalfCoursePrice;
                var isCustom = qc.QuotationCoursePrice?.IsCustomPrice ?? false;

                dt.Rows.Add(
                    quotation.QuotationId,
                    quotation.CompanyName,
                    quotation.ContactDetails,
                    quotation.Email,
                    quotation.Status,
                    quotation.CreatedAt,
                    quotation.User?.Name,
                    qc.CourseOption.Course.CourseName,
                    qc.CourseOption.Course.VehicleType,
                    qc.CourseOption.CourseType.TypeName,
                    fullPrice,
                    halfPrice,
                    isCustom
                );
            }

            // Render report using AspNetCore.Reporting
            var report = new LocalReport();
            report.ReportPath = Path.Combine(_webHostEnvironment.WebRootPath, "Reports", "QuotationReport.rdl");

            report.DataSources.Add(new ReportDataSource("QuotationData", dt));

            // Set report parameter
            var parameters = new List<ReportParameter>
            {
                new ReportParameter("QuotationId", quotationId.ToString())
            };
            report.SetParameters(parameters);

            // Render as PDF
            string mimeType, encoding, extension;
            string[] streams;
            Warning[] warnings;
            byte[] pdfBytes = report.Render("PDF", null, out mimeType, out encoding, out extension, out streams, out warnings);

            // Return PDF file
            return File(pdfBytes, "application/pdf", $"Quotation_{quotationId}.pdf");
        }
    }
}