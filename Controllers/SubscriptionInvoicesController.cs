using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ZoneBill_Lloren.Data;

namespace ZoneBill_Lloren.Controllers
{
    [Authorize(Roles = "SuperAdmin")]
    public class SubscriptionInvoicesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SubscriptionInvoicesController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var invoices = await _context.SubscriptionInvoices
                .Include(i => i.Business)
                .Include(i => i.Plan)
                .OrderByDescending(i => i.IssuedAt)
                .ToListAsync();

            return View(invoices);
        }
    }
}
