using DinkToPdf.Contracts;
using IronPdf;
using IronPdf.Rendering;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Reporting.NETCore;
using QuotationSystem.Models;
using System.Data;
//using DinkToPdf;
//using DinkToPdf.Contracts;

namespace QuotationSystem.Controllers
{
    public class QuotationController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly string _ssrsServerUrl = "http://your-ssrs-server/ReportServer";
        private readonly string _reportPath = "/Reports/QuotationReport";

        //private readonly IConverter _converter;
        //public QuotationController(AppDbContext context, IWebHostEnvironment webHostEnvironment, IConverter converter)
        //{
        //    _context = context;
        //    _webHostEnvironment = webHostEnvironment;
        //    _converter = converter;
        //    System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        //}
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

        // NEW METHOD: HTML to PDF Download DinkToPdf
        //[HttpGet]
        //public async Task<IActionResult> DownloadQuotationHtmlPdfFree(int quotationId)
        //{
        //    var role = HttpContext.Session.GetString("UserRole");
        //    if (role != "Admin") return RedirectToAction("Login", "Account");

        //    // Fetch quotation data (same as before)
        //    var quotation = _context.Quotations
        //        .Where(q => q.QuotationId == quotationId)
        //        .Include(q => q.User)
        //        .Include(q => q.QuotationCourses)
        //            .ThenInclude(qc => qc.CourseOption)
        //                .ThenInclude(co => co.Course)
        //        .Include(q => q.QuotationCourses)
        //            .ThenInclude(qc => qc.CourseOption)
        //                .ThenInclude(co => co.CourseType)
        //        .Include(q => q.QuotationCourses)
        //            .ThenInclude(qc => qc.QuotationCoursePrice)
        //        .FirstOrDefault();

        //    if (quotation == null)
        //    {
        //        return NotFound("Quotation not found.");
        //    }

        //    // Generate HTML content (same method as before)
        //    var htmlContent = await GenerateQuotationHtml(quotation);

        //    // Convert using DinkToPdf
        //    var doc = new HtmlToPdfDocument()
        //    {
        //        GlobalSettings = {
        //    ColorMode = ColorMode.Color,
        //    Orientation = Orientation.Portrait,
        //    PaperSize = PaperKind.A4,
        //    Margins = new MarginSettings() { Top = 10, Bottom = 10, Left = 10, Right = 10 }
        //},
        //        Objects = {
        //    new ObjectSettings() {
        //        PagesCount = true,
        //        HtmlContent = htmlContent,
        //        WebSettings = { DefaultEncoding = "utf-8" }
        //    }
        //}
        //    };

        //    byte[] pdf = _converter.Convert(doc);
        //    return File(pdf, "application/pdf", $"Quotation_HTML_{quotationId}_{DateTime.Now:yyyyMMdd}.pdf");
        //}

        // NEW METHOD: HTML to PDF Download IronPDF
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
                    .ThenInclude(qc => qc.QuotationCoursePrice) // Include custom pricing
                .FirstOrDefault();

            if (quotation == null)
            {
                return NotFound("Quotation not found.");
            }

            // Generate HTML content
            var htmlContent = await GenerateQuotationHtml(quotation);

            // Convert HTML to PDF using IronPDF
            var renderer = new ChromePdfRenderer();

            // Configure PDF settings
            renderer.RenderingOptions.PaperSize = PdfPaperSize.A4;
            renderer.RenderingOptions.MarginTop = 10;
            renderer.RenderingOptions.MarginBottom = 10;
            renderer.RenderingOptions.MarginLeft = 10;
            renderer.RenderingOptions.MarginRight = 10;
            renderer.RenderingOptions.PrintHtmlBackgrounds = true;
            renderer.RenderingOptions.EnableJavaScript = false;
            renderer.RenderingOptions.Timeout = 60;

            // Generate PDF
            var pdf = renderer.RenderHtmlAsPdf(htmlContent);

            // Return PDF file
            var pdfBytes = pdf.BinaryData;
            return File(pdfBytes, "application/pdf", $"Quotation_HTML_{quotationId}_{DateTime.Now:yyyyMMdd}.pdf");
        }

        // Helper method to generate HTML content
        private async Task<string> GenerateQuotationHtml(Quotation quotation)
        {
            // Get the base URL for images
            var request = HttpContext.Request;
            var baseUrl = $"{request.Scheme}://{request.Host}";

            var html = $@"
<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Quotation - {quotation.QuotationId}</title>
    <style>
        body {{
            font-family: Arial, sans-serif;
            margin: 0;
            padding: 20px;
            background-color: #f5f5f5;
        }}
        .quotation-container {{
            background: white;
            border: 2px solid #e97318;
            max-width: 800px;
            margin: 0 auto;
        }}
        .header {{
            background: linear-gradient(135deg, #e97318 0%, #f4a460 100%);
            color: white;
            padding: 20px;
            display: flex;
            align-items: center;
            justify-content: space-between;
        }}
        .logo-section {{
            display: flex;
            align-items: center;
        }}
        .logo {{
            width: 60px;
            height: 60px;
            margin-right: 15px;
        }}
        .company-info h1 {{
            margin: 0;
            font-size: 20px;
            font-weight: bold;
        }}
        .company-info p {{
            margin: 5px 0 0 0;
            font-size: 12px;
        }}
        .date-info {{
            text-align: right;
            font-size: 12px;
        }}
        .quotation-title {{
            background: #e97318;
            color: white;
            text-align: center;
            padding: 10px;
            font-size: 24px;
            font-weight: bold;
            margin: 0;
        }}
        .form-section {{
            padding: 20px;
            background: #fff2e6;
        }}
        .form-row {{
            display: flex;
            margin-bottom: 10px;
            align-items: center;
        }}
        .form-label {{
            width: 120px;
            font-weight: bold;
            color: #333;
        }}
        .form-value {{
            flex: 1;
            padding: 5px;
            border-bottom: 1px solid #ccc;
        }}
        .courses-table {{
            width: 100%;
            border-collapse: collapse;
            margin: 20px 0;
        }}
        .courses-table th {{
            background: #8b4513;
            color: white;
            padding: 10px;
            text-align: center;
            font-size: 12px;
            border: 1px solid #654321;
        }}
        .courses-table td {{
            padding: 10px;
            text-align: center;
            border: 1px solid #ddd;
            font-size: 11px;
        }}
        .courses-table tr:nth-child(even) {{
            background-color: #f9f9f9;
        }}
        .vehicle-img {{
            width: 40px;
            height: 30px;
            object-fit: contain;
        }}
        .course-name {{
            background: #e97318;
            color: white;
            padding: 5px;
            margin: 2px 0;
            font-weight: bold;
        }}
        .price-highlight {{
            font-weight: bold;
            color: #8b4513;
        }}
        .notes-section {{
            background: #e97318;
            color: white;
            text-align: center;
            padding: 10px;
            font-weight: bold;
            margin: 20px 0 0 0;
        }}
        .notes-content {{
            background: #fff2e6;
            padding: 15px;
            font-size: 12px;
            line-height: 1.4;
        }}
        .footer-note {{
            text-align: center;
            font-size: 11px;
            margin: 10px 0;
            font-style: italic;
        }}
        .signature-section {{
            display: flex;
            justify-content: space-between;
            margin: 30px 0;
            padding: 0 20px;
        }}
        .signature-box {{
            text-align: center;
            border-top: 1px solid #333;
            padding-top: 5px;
            width: 150px;
        }}
        .thank-you {{
            text-align: center;
            font-weight: bold;
            color: #8b4513;
            font-size: 16px;
            margin: 20px 0;
        }}
        .custom-price-badge {{
            background: #ffc107;
            color: #000;
            padding: 2px 6px;
            border-radius: 3px;
            font-size: 10px;
            margin-left: 5px;
        }}
    </style>
</head>
<body>
    <div class='quotation-container'>
        <div class='header'>
            <div class='logo-section'>
                <img src='{baseUrl}/images/logo.png' alt='Logo' class='logo' />
                <div class='company-info'>
                    <h1>ALRAYAH DRIVING SCHOOL</h1>
                    <p>مدرسة الراية لتعليم قيادة السيارات</p>
                </div>
            </div>
            <div class='date-info'>
                <div>Date: {DateTime.Now:dd/MM/yyyy}</div>
                <div>Quotation: {quotation.QuotationId:000-000-000}</div>
            </div>
        </div>

        <div class='quotation-title'>QUOTATION</div>

        <div class='form-section'>
            <div class='form-row'>
                <div class='form-label'>Quotation For:</div>
                <div class='form-value'>{quotation.User?.Name ?? "N/A"}</div>
            </div>
            <div class='form-row'>
                <div class='form-label'>Company Name:</div>
                <div class='form-value'>{quotation.CompanyName}</div>
                <div class='form-label' style='margin-left: 20px;'>Contact Person:</div>
                <div class='form-value'>{quotation.ContactDetails}</div>
            </div>
            <div class='form-row'>
                <div class='form-label'>Contact Number:</div>
                <div class='form-value'>{quotation.ContactDetails}</div>
                <div class='form-label' style='margin-left: 20px;'>Email:</div>
                <div class='form-value'>{quotation.Email}</div>
            </div>
        </div>

        <table class='courses-table'>
            <thead>
                <tr>
                    <th style='width: 15%;'>License Type</th>
                    <th style='width: 15%;'>Vehicles</th>
                    <th style='width: 20%;'>Course Type</th>
                    <th style='width: 25%;'>Course Price</th>
                    <th style='width: 12.5%;'>Full Course</th>
                    <th style='width: 12.5%;'>Half Course</th>
                </tr>
            </thead>
            <tbody>";

            foreach (var qc in quotation.QuotationCourses)
            {
                var actualFullPrice = qc.QuotationCoursePrice?.FullCoursePrice ?? qc.CourseOption.FullCoursePrice;
                var actualHalfPrice = qc.QuotationCoursePrice?.HalfCoursePrice ?? qc.CourseOption.HalfCoursePrice;
                var isCustomPrice = qc.QuotationCoursePrice?.IsCustomPrice ?? false;

                // Get vehicle image based on vehicle type
                var vehicleImage = GetVehicleImage(qc.CourseOption.Course.VehicleType);

                html += $@"
                <tr>
                    <td>
                        <div class='course-name'>{qc.CourseOption.Course.VehicleType}</div>
                    </td>
                    <td>
                        <img src='{baseUrl}/images/{vehicleImage}' alt='{qc.CourseOption.Course.VehicleType}' class='vehicle-img' />
                    </td>
                    <td>
                        <div style='background: #8b4513; color: white; padding: 5px; margin: 2px 0;'>
                            {qc.CourseOption.CourseType.TypeName}
                        </div>
                        <div style='font-size: 10px;'>{qc.CourseOption.Course.CourseName}</div>
                    </td>
                    <td class='price-highlight'>
                        QR {actualFullPrice:F3}
                        {(isCustomPrice ? "<span class='custom-price-badge'>Custom Price</span>" : "<div style='font-size: 10px; color: #666;'>Standard Price</div>")}
                    </td>
                    <td class='price-highlight'>QR {actualFullPrice:F1}</td>
                    <td class='price-highlight'>QR {actualHalfPrice:F1}</td>
                </tr>";
            }

            var totalFullPrice = quotation.QuotationCourses.Sum(qc => qc.QuotationCoursePrice?.FullCoursePrice ?? qc.CourseOption.FullCoursePrice);
            var totalHalfPrice = quotation.QuotationCourses.Sum(qc => qc.QuotationCoursePrice?.HalfCoursePrice ?? qc.CourseOption.HalfCoursePrice);

            html += $@"
            </tbody>
        </table>

        <div class='notes-section'>
            SPECIAL NOTES AND INSTRUCTIONS
        </div>
        <div class='notes-content'>
            <div>• Signal, Theory, Computer Class = 350 QR</div>
            <div>• Test Fees = 50 QR</div>
            <div>• Eye test = 50 QR</div>
            <br>
            <div style='font-weight: bold;'>Above information is not an invoice and only an estimate of services described above.</div>
            <div>Payment will be collected in prior to provision of services described in this quote.</div>
            <br>
            <div style='font-weight: bold;'>Please confirm your acceptance of this quote by signing below and return this document.</div>
        </div>

        <div class='signature-section'>
            <div class='signature-box'>
                <div>Signature</div>
            </div>
            <div class='signature-box'>
                <div>Name</div>
            </div>
            <div class='signature-box'>
                <div>Date</div>
            </div>
        </div>

        <div class='thank-you'>
            Thank you for your Business!
        </div>
    </div>
</body>
</html>";

            return html;
        }

        // Helper method to get vehicle image filename
        private string GetVehicleImage(string vehicleType)
        {
            return vehicleType.ToLower() switch
            {
                "light vehicle" => "car.png",
                "motorcycle" => "motorcycle.png",
                "bus" => "bus.png",
                "trailer" => "truck.png",
                "medium truck" => "truck.png",
                "shovel" => "excavator.png",
                "excavator" => "excavator.png",
                "forklift" => "forklift.png",
                "crane" => "crane.png",
                _ => "car.png"
            };
        }

        // EXISTING METHOD: Microsoft Report Builder PDF (Keep this as it was)
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