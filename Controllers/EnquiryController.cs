using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuotationSystem.Models;

namespace QuotationSystem.Controllers
{
    public class EnquiryController : Controller
    {
        private readonly AppDbContext _context;

        public EnquiryController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public IActionResult Submit(Enquiry enquiry)
        {
            _context.Enquiries.Add(enquiry);
            _context.SaveChanges();
            TempData["Success"] = "Your enquiry has been submitted successfully!";
            return RedirectToAction("Index", "Home");
        }


        public IActionResult AdminView()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var enquiries = _context.Enquiries
                .Include(e => e.Quotation)
                .Where(e => e.Quotation.UserId == userId)
                .OrderByDescending(e => e.CreatedAt)
                .ToList();

            return View(enquiries);
        }

    }
}