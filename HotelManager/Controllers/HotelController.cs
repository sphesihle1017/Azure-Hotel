using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HotelManager.Data;
using HotelManager.Models;
using System.Linq;
using System.Threading.Tasks;

namespace HotelManager.Controllers
{
    // Admin-facing hotel management: create, list, edit and delete hotels.
    [Authorize(Roles = "Admin")]
    public class HotelsController : Controller
    {
        private readonly AppDbContext _context;

        public HotelsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: /Hotels
        public async Task<IActionResult> Index()
        {
            var hotels = await _context.Hotels
                .Include(h => h.Rooms)
                .OrderBy(h => h.Name)
                .ToListAsync();


            return View(hotels);
        }

        // GET: /Hotels/Details/{id}
        public async Task<IActionResult> Details(int id)
        {
            var hotel = await _context.Hotels
                .Include(h => h.Rooms)
                .FirstOrDefaultAsync(h => h.HotelId == id);

            if (hotel == null) return NotFound();

            return View(hotel);
        }

        // GET: /Hotels/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: /Hotels/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Hotel hotel)
        {
            ModelState.Remove("Rooms");

            if (!string.IsNullOrWhiteSpace(hotel.Name))
            {
                bool nameExists = await _context.Hotels
                    .AnyAsync(h => h.Name.ToLower() == hotel.Name.ToLower());
                if (nameExists)
                {
                    ModelState.AddModelError("Name", "A hotel with this name already exists.");
                }
            }

            if (ModelState.IsValid)
            {
                _context.Hotels.Add(hotel);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Hotel '{hotel.Name}' created successfully!";
                return RedirectToAction(nameof(Index));
            }

            return View(hotel);
        }

        // GET: /Hotels/Edit/{id}
        public async Task<IActionResult> Edit(int id)
        {
            var hotel = await _context.Hotels.FindAsync(id);
            if (hotel == null) return NotFound();

            return View(hotel);
        }

        // POST: /Hotels/Edit/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Hotel hotel)
        {
            if (id != hotel.HotelId) return NotFound();

            ModelState.Remove("Rooms");

            if (string.IsNullOrWhiteSpace(hotel.Name))
            {
                ModelState.AddModelError("Name", "Hotel name is required.");
            }
            else
            {
                bool nameExists = await _context.Hotels
                    .AnyAsync(h => h.HotelId != id && h.Name.ToLower() == hotel.Name.ToLower());
                if (nameExists)
                {
                    ModelState.AddModelError("Name", "A hotel with this name already exists.");
                }
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existing = await _context.Hotels.FindAsync(id);
                    if (existing == null) return NotFound();

                    // Update only the editable fields — keeps navigation/related data intact.
                    existing.Name = hotel.Name;
                    existing.Location = hotel.Location;

                    await _context.SaveChangesAsync();

                    TempData["Success"] = $"Hotel '{hotel.Name}' updated successfully!";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await _context.Hotels.AnyAsync(h => h.HotelId == id))
                        return NotFound();
                    throw;
                }
            }

            return View(hotel);
        }

        // POST: /Hotels/Delete/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var hotel = await _context.Hotels
                .Include(h => h.Rooms)
                .FirstOrDefaultAsync(h => h.HotelId == id);

            if (hotel == null)
            {
                TempData["Error"] = "Hotel not found.";
                return RedirectToAction(nameof(Index));
            }

            if (hotel.Rooms != null && hotel.Rooms.Any())
            {
                TempData["Error"] = "Cannot delete a hotel that still has rooms. Delete or reassign its rooms first.";
                return RedirectToAction(nameof(Index));
            }

            _context.Hotels.Remove(hotel);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Hotel deleted successfully!";
            return RedirectToAction(nameof(Index));
        }
    }
}