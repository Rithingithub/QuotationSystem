using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuotationSystem.Models;
using System.Diagnostics;

namespace QuotationSystem.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _context;

        public HomeController(AppDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            var quotations = _context.Quotations
                .Where(q => q.Status == "Approved")
                .Include(q => q.User)
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

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
