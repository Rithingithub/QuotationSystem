using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Reporting.NETCore;
using QuotationSystem.Models;
using System.Data;
using DinkToPdf;
using DinkToPdf.Contracts;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace QuotationSystem.Controllers
{
    public class QuotationController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IConverter _converter;
        private readonly string _ssrsServerUrl = "http://your-ssrs-server/ReportServer";
        private readonly string _reportPath = "/Reports/QuotationReport";

        public QuotationController(AppDbContext context, IWebHostEnvironment webHostEnvironment, IConverter converter)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _converter = converter;
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

            // Get statistics for the view
            ViewBag.TotalQuotations = _context.Quotations.Count();
            ViewBag.ApprovedCount = _context.Quotations.Count(q => q.Status == "Approved");
            ViewBag.DeclinedCount = _context.Quotations.Count(q => q.Status == "Declined");

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

        [HttpGet]
        public async Task<IActionResult> DownloadQuotationHtmlPdf(int quotationId)
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
                    .ThenInclude(qc => qc.QuotationCoursePrice)
                .FirstOrDefault();

            if (quotation == null)
            {
                return NotFound("Quotation not found.");
            }

            // Generate HTML from template
            var htmlContent = await RenderViewToStringAsync("QuotationPdfTemplate", quotation);
            ViewData["BasePath"] = _webHostEnvironment.WebRootPath; 
            var doc = new HtmlToPdfDocument()
            {
                GlobalSettings = {
                    ColorMode = ColorMode.Color,
                    Orientation = Orientation.Portrait,
                    PaperSize = PaperKind.A4,
                    Margins = new MarginSettings { Top = 10, Bottom = 10, Left = 10, Right = 10 },
                    DocumentTitle = $"Quotation {quotationId}"
                },
                Objects = {
                    new ObjectSettings() {
                        PagesCount = true,
                        HtmlContent = htmlContent,
                        WebSettings = { DefaultEncoding = "utf-8" },
                        HeaderSettings = { FontSize = 9, Right = "Page [page] of [toPage]", Line = true },
                        FooterSettings = { FontSize = 9, Center = "Al Rayah Driving School", Line = true }
                    }
                }
            };

            byte[] pdf = _converter.Convert(doc);
            return File(pdf, "application/pdf", $"Quotation_{quotationId}_HTML.pdf");
        }

        // New method with Base64 image handling
        [HttpGet]
        public async Task<IActionResult> DownloadQuotationHtmlPdfWithBase64(int quotationId)
        {
            var role = HttpContext.Session.GetString("UserRole");
            if (role != "Admin") return RedirectToAction("Login", "Account");

            // Fetch quotation data
            var quotation = await _context.Quotations
                .Where(q => q.QuotationId == quotationId)
                .Include(q => q.User)
                .Include(q => q.QuotationCourses)
                    .ThenInclude(qc => qc.CourseOption)
                        .ThenInclude(co => co.Course)
                .Include(q => q.QuotationCourses)
                    .ThenInclude(qc => qc.CourseOption)
                        .ThenInclude(co => co.CourseType)
                .Include(q => q.QuotationCourses)
                    .ThenInclude(qc => qc.QuotationCoursePrice)
                .FirstOrDefaultAsync();

            if (quotation == null)
            {
                return NotFound("Quotation not found.");
            }

            // Prepare Base64 images
            var logoPath = Path.Combine(_webHostEnvironment.WebRootPath, "images", "logo.png");
            string logoBase64 = "";
            if (System.IO.File.Exists(logoPath))
            {
                var logoBytes = await System.IO.File.ReadAllBytesAsync(logoPath);
                logoBase64 = Convert.ToBase64String(logoBytes);
            }
            else
            {
                // Handle missing logo gracefully (e.g., log error or use placeholder)
                logoBase64 = ""; // Optionally, set a placeholder Base64 string
            }

            var vehicleImages = new Dictionary<string, string>();
            var vehicleImageFiles = Directory.GetFiles(Path.Combine(_webHostEnvironment.WebRootPath, "images", "vehicles"));
            foreach (var file in vehicleImageFiles)
            {
                var bytes = await System.IO.File.ReadAllBytesAsync(file);
                vehicleImages[Path.GetFileName(file)] = Convert.ToBase64String(bytes);
            }

            // Pass Base64 data to the view
            ViewData["LogoBase64"] = logoBase64;
            ViewData["VehicleImages"] = vehicleImages;

            // Generate HTML from template
            var htmlContent = await RenderViewToStringAsync("QuotationPdfTemplate", quotation);

            var doc = new HtmlToPdfDocument()
            {
                GlobalSettings = {
                    ColorMode = ColorMode.Color,
                    Orientation = Orientation.Portrait,
                    PaperSize = PaperKind.A4,
                    Margins = new MarginSettings { Top = 10, Bottom = 10, Left = 10, Right = 10 },
                    DocumentTitle = $"Quotation {quotationId}"
                },
                Objects = {
                    new ObjectSettings() {
                        PagesCount = true,
                        HtmlContent = htmlContent,
                        WebSettings = { DefaultEncoding = "utf-8", LoadImages = true },
                        //HeaderSettings = { FontSize = 9, Right = "Page [page] of [toPage]", Line = true },
                        //FooterSettings = { FontSize = 9, Center = "Al Rayah Driving School", Line = true }
                    }
                }
            };

            byte[] pdf = _converter.Convert(doc);
            return File(pdf, "application/pdf", $"Quotation_{quotationId}_Base64.pdf");
        }

        private async Task<string> RenderViewToStringAsync(string viewName, object model)
        {
            ViewData.Model = model;
            using (var writer = new StringWriter())
            {
                var viewEngine = HttpContext.RequestServices.GetRequiredService<ICompositeViewEngine>();
                var viewResult = viewEngine.FindView(ControllerContext, viewName, false);

                if (!viewResult.Success)
                {
                    throw new ArgumentNullException($"Unable to find view '{viewName}'");
                }

                var viewContext = new ViewContext(
                    ControllerContext,
                    viewResult.View,
                    ViewData,
                    TempData,
                    writer,
                    new HtmlHelperOptions()
                );

                await viewResult.View.RenderAsync(viewContext);
                return writer.GetStringBuilder().ToString();
            }
        }
    }
}