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
        private readonly string _ssrsServerUrl = "http://your-ssrs-server/ReportServer"; // Update with your SSRS server URL
        private readonly string _reportPath = "/Reports/QuotationReport"; // Update with your report path

        public QuotationController(AppDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            // Required for AspNetCore.Reporting to resolve font issues
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

        [HttpGet]
        public IActionResult DownloadQuotationPdf(int quotationId)
        {
            var role = HttpContext.Session.GetString("UserRole");
            if (role != "Admin") return RedirectToAction("Login", "Account");

            // Fetch quotation data
            var quotation = _context.Quotations
                .Where(q => q.QuotationId == quotationId)
                .Include(q => q.User)
                .Include(q => q.QuotationCourses)
                    .ThenInclude(qc => qc.CourseOption)
                        .ThenInclude(co => co.Course)
                .Include(q => q.QuotationCourses)
                    .ThenInclude(qc => qc.CourseOption)
                        .ThenInclude(co => co.CourseType)
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

            foreach (var qc in quotation.QuotationCourses)
            {
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
                    qc.CourseOption.FullCoursePrice,
                    qc.CourseOption.HalfCoursePrice
                );
            }

            // Render report using AspNetCore.Reporting
            var report = new LocalReport();
            report.ReportPath = Path.Combine(_webHostEnvironment.WebRootPath, "Reports", "QuotationReport.rdl"); // Local RDL file path
            // For SSRS server, uncomment below and comment out ReportPath
            // report.ReportPath = _reportPath; // e.g., "/Reports/QuotationReport"
            // report.IsServerReport = true;
            // report.ReportServerUrl = new Uri(_ssrsServerUrl);

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