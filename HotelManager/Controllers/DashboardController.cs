using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HotelManager.Data;
using HotelManager.Models;

namespace HotelManager.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<Users> _userManager;

        public DashboardController(
            AppDbContext context,
            UserManager<Users> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        //==========================================================
        // Redirect
        //==========================================================

        public IActionResult Index()
        {
            if (User.IsInRole("Admin"))
                return RedirectToAction(nameof(Admin));

            return RedirectToAction(nameof(Customer));
        }

        //==========================================================
        // CUSTOMER DASHBOARD
        //==========================================================

        [Authorize(Roles = "User")]
        public async Task<IActionResult> Customer()
        {
            var rooms = await _context.Rooms
                .Include(r => r.Hotel)
                .Where(r => r.Quantity > 0)
                .OrderBy(r => r.Hotel.Name)
                .ThenBy(r => r.RoomDescription)
                .ToListAsync();

            return View(rooms);
        }

        //==========================================================
        // AVAILABLE ROOMS
        //==========================================================

        [Authorize(Roles = "User")]
        public async Task<IActionResult> AvailableRooms()
        {
            var rooms = await _context.Rooms
                .Include(r => r.Hotel)
                .Where(r => r.Quantity > 0)
                .OrderBy(r => r.Hotel.Name)
                .ThenBy(r => r.RoomDescription)
                .ToListAsync();

            return View(rooms);
        }

        //==========================================================
        // MY BOOKINGS
        //==========================================================

        [Authorize(Roles = "User")]
        public async Task<IActionResult> MyBookings()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return RedirectToAction("Login", "Account");

            var customer = await _context.Customers
                .FirstOrDefaultAsync(c =>
                    c.Email == user.Email &&
                    c.IsActive);

            if (customer == null)
            {
                TempData["Error"] =
                    "Your account has been deactivated. Please contact the administrator.";

                return RedirectToAction("Logout", "Account");
            }

            var bookings = await _context.Bookings
                .Include(b => b.Room)
                    .ThenInclude(r => r.Hotel)
                .Where(b =>
                    b.CustomerId == customer.CustomerId &&
                    b.IsActive)
                .OrderByDescending(b => b.BookingId)
                .ToListAsync();

            return View(bookings);
        }

        //==========================================================
        // ADMIN DASHBOARD
        //==========================================================

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Admin()
        {
            var recentCustomers = await _context.Customers
                .OrderByDescending(c => c.CustomerId)
                .Take(10)
                .ToListAsync();

            var recentRooms = await _context.Rooms
                .Include(r => r.Hotel)
                .OrderByDescending(r => r.RoomId)
                .Take(10)
                .ToListAsync();

            var recentBookings = await _context.Bookings
                .Include(b => b.Customer)
                .Include(b => b.Room)
                    .ThenInclude(r => r.Hotel)
                .OrderByDescending(b => b.BookingId)
                .Take(10)
                .ToListAsync();

            var recentHotels = await _context.Hotels
                .Include(h => h.Rooms)
                .OrderByDescending(h => h.HotelId)
                .Take(10)
                .ToListAsync();

            ViewBag.RecentCustomers = recentCustomers;
            ViewBag.RecentRooms = recentRooms;
            ViewBag.RecentBookings = recentBookings;
            ViewBag.RecentHotels = recentHotels;

            // Dashboard Statistics
            ViewBag.TotalCustomers = await _context.Customers
                .CountAsync(c => c.IsActive);

            ViewBag.TotalHotels = await _context.Hotels
                .CountAsync();

            ViewBag.TotalRooms = await _context.Rooms
                .SumAsync(r => r.Quantity);

            ViewBag.TotalBookings = await _context.Bookings
                .CountAsync(b => b.IsActive);

            ViewBag.CancelledBookings = await _context.Bookings
                .CountAsync(b => !b.IsActive);

            ViewBag.InactiveCustomers = await _context.Customers
                .CountAsync(c => !c.IsActive);

            return View();
        }
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ManageCustomers(string searchString)
        {
            var customers = _context.Customers
                .Include(c => c.Bookings)
                .OrderByDescending(c => c.CustomerId)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                customers = customers.Where(c =>
                    c.FirstName.Contains(searchString) ||
                    c.LastName.Contains(searchString) ||
                    c.Email.Contains(searchString) ||
                    c.PhoneNumber.Contains(searchString));
            }

            ViewBag.SearchString = searchString;

            return View(await customers.ToListAsync());
        }
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Rooms()
        {
            var rooms = await _context.Rooms
                .Include(r => r.Hotel)
                .Include(r => r.Bookings)
                .OrderBy(r => r.Hotel.Name)
                .ThenBy(r => r.RoomDescription)
                .ToListAsync();

            return View(rooms);
        }
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ViewBookings()
        {
            var bookings = await _context.Bookings
                .Include(b => b.Customer)
                .Include(b => b.Room)
                    .ThenInclude(r => r.Hotel)
                .OrderByDescending(b => b.BookingId)
                .ToListAsync();

            return View(bookings);
        }
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelBooking(int id)
        {
            try
            {
                var booking = await _context.Bookings
                    .Include(b => b.Customer)
                    .FirstOrDefaultAsync(b => b.BookingId == id);

                if (booking == null)
                {
                    TempData["Error"] = "Booking not found.";
                    return RedirectToAction(nameof(ViewBookings));
                }

                booking.IsActive = !booking.IsActive;

                _context.Bookings.Update(booking);

                await _context.SaveChangesAsync();

                TempData["Success"] = booking.IsActive
                    ? "Booking activated successfully."
                    : "Booking cancelled successfully.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(nameof(ViewBookings));
        }
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCustomer(int id)
        {
            try
            {
                var customer = await _context.Customers
                    .Include(c => c.Bookings)
                    .FirstOrDefaultAsync(c => c.CustomerId == id);

                if (customer == null)
                {
                    TempData["Error"] = "Customer not found.";
                    return RedirectToAction(nameof(ManageCustomers));
                }

                customer.IsActive = !customer.IsActive;

                foreach (var booking in customer.Bookings)
                {
                    booking.IsActive = customer.IsActive;
                }

                _context.Update(customer);

                await _context.SaveChangesAsync();

                TempData["Success"] = customer.IsActive
                    ? "Customer activated successfully."
                    : "Customer deactivated successfully.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(nameof(ManageCustomers));
        }
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CustomerDetails(int id)
        {
            var customer = await _context.Customers
                .Include(c => c.Bookings)
                    .ThenInclude(b => b.Room)
                        .ThenInclude(r => r.Hotel)
                .FirstOrDefaultAsync(c => c.CustomerId == id);

            if (customer == null)
                return NotFound();

            return View(customer);
        }
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> RoomDetails(int id)
        {
            var room = await _context.Rooms
                .Include(r => r.Hotel)
                .Include(r => r.Bookings)
                    .ThenInclude(b => b.Customer)
                .FirstOrDefaultAsync(r => r.RoomId == id);

            if (room == null)
                return NotFound();

            ViewBag.BookedRooms = room.Bookings.Count(b => b.IsActive);

            ViewBag.AvailableRooms =
                room.Quantity - room.Bookings.Count(b => b.IsActive);

            return View(room);
        }
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateRoom()
        {
            ViewBag.Hotels = await _context.Hotels.ToListAsync();
            return View();
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateRoom(Room room)
        {
            try
            {
                ModelState.Remove("Hotel");
                ModelState.Remove("Bookings");

                if (room.HotelId <= 0)
                    ModelState.AddModelError("HotelId", "Please select a hotel.");

                if (string.IsNullOrWhiteSpace(room.RoomDescription))
                    ModelState.AddModelError("RoomDescription", "Please select a room type.");

                if (room.PricePerNight <= 0)
                    ModelState.AddModelError("PricePerNight", "Price must be greater than zero.");

                if (room.Quantity <= 0)
                    ModelState.AddModelError("Quantity", "Quantity must be greater than zero.");

                if (!ModelState.IsValid)
                {
                    ViewBag.Hotels = await _context.Hotels.ToListAsync();
                    return View(room);
                }

                var hotelExists = await _context.Hotels
                    .AnyAsync(h => h.HotelId == room.HotelId);

                if (!hotelExists)
                {
                    TempData["Error"] = "Selected hotel does not exist.";
                    ViewBag.Hotels = await _context.Hotels.ToListAsync();
                    return View(room);
                }

                _context.Rooms.Add(room);

                await _context.SaveChangesAsync();

                TempData["Success"] = "Room created successfully.";

                return RedirectToAction(nameof(Rooms));
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;

                ViewBag.Hotels = await _context.Hotels.ToListAsync();

                return View(room);
            }
        }
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> EditRoom(int id)
        {
            var room = await _context.Rooms.FindAsync(id);

            if (room == null)
                return NotFound();

            ViewBag.Hotels = await _context.Hotels.ToListAsync();

            return View(room);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditRoom(int id, Room room)
        {
            if (id != room.RoomId)
                return NotFound();

            try
            {
                ModelState.Remove("Hotel");
                ModelState.Remove("Bookings");

                if (room.HotelId <= 0)
                    ModelState.AddModelError("HotelId", "Please select a hotel.");

                if (string.IsNullOrWhiteSpace(room.RoomDescription))
                    ModelState.AddModelError("RoomDescription", "Please select a room type.");

                if (room.PricePerNight <= 0)
                    ModelState.AddModelError("PricePerNight", "Price must be greater than zero.");

                if (room.Quantity <= 0)
                    ModelState.AddModelError("Quantity", "Quantity must be greater than zero.");

                if (!ModelState.IsValid)
                {
                    ViewBag.Hotels = await _context.Hotels.ToListAsync();
                    return View(room);
                }

                var existingRoom = await _context.Rooms.FindAsync(id);

                if (existingRoom == null)
                    return NotFound();

                existingRoom.HotelId = room.HotelId;
                existingRoom.RoomDescription = room.RoomDescription;
                existingRoom.PricePerNight = room.PricePerNight;
                existingRoom.Quantity = room.Quantity;

                await _context.SaveChangesAsync();

                TempData["Success"] = "Room updated successfully.";

                return RedirectToAction(nameof(Rooms));
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;

                ViewBag.Hotels = await _context.Hotels.ToListAsync();

                return View(room);
            }
        }
    }
}