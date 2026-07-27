using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HotelManager.Data;
using Microsoft.AspNetCore.Identity;
using HotelManager.Models;

namespace HotelManager.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<Users> _userManager; // Changed from IdentityUser to Users

        public DashboardController(AppDbContext context, UserManager<Users> userManager) // Changed parameter type
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: /Dashboard/Index - Redirects based on user role
        public IActionResult Index()
        {
            if (User.IsInRole("Admin"))
            {
                return RedirectToAction("Admin");
            }
            return RedirectToAction("Customer");
        }

        // GET: /Dashboard/Customer - Customer dashboard showing available rooms and history
        [Authorize(Roles = "User")]
        public async Task<IActionResult> Customer()
        {
            var availableRooms = await _context.Rooms
                .Include(r => r.Hotel)
                .ToListAsync();
            return View(availableRooms);
        }

        // GET: /Dashboard/AvailableRooms - Shows available rooms for customers
        [Authorize(Roles = "User")]
        public async Task<IActionResult> AvailableRooms()
        {
            var availableRooms = await _context.Rooms
                .Include(r => r.Hotel)
                .ToListAsync();
            return View(availableRooms);
        }

        // GET: /Dashboard/MyBookings - Shows booking history for customers
        [Authorize(Roles = "User")]
        public async Task<IActionResult> MyBookings()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.Email == user.Email);

            if (customer == null)
            {
                return View(new List<Booking>());
            }

            var bookings = await _context.Bookings
                .Include(b => b.Room)
                .ThenInclude(r => r.Hotel)
                .Where(b => b.CustomerId == customer.CustomerId)
                .OrderByDescending(b => b.CheckInDate)
                .ToListAsync();

            return View(bookings);
        }

        // GET: /Dashboard/Admin - Admin dashboard for managing customers and rooms
        [Authorize(Roles = "Admin")]
        //public async Task<IActionResult> Admin()
        //{
        //    // Get recent customers (last 10)
        //    var recentCustomers = await _context.Customers
        //        .OrderByDescending(c => c.CustomerId)
        //        .Take(10)
        //        .ToListAsync();

        //    // Get recent rooms with hotel info
        //    var recentRooms = await _context.Rooms
        //        .Include(r => r.Hotel)
        //        .OrderByDescending(r => r.RoomId)
        //        .Take(10)
        //        .ToListAsync();

        //    // Pass data to view using ViewBag
        //    ViewBag.RecentCustomers = recentCustomers;
        //    ViewBag.RecentRooms = recentRooms;

        //    return View();
        //}
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
            ViewBag.TotalCustomers = await _context.Customers.CountAsync();
            ViewBag.TotalHotels = await _context.Hotels.CountAsync();
            ViewBag.TotalRooms = await _context.Rooms.SumAsync(r => r.Quantity);
            ViewBag.TotalBookings = await _context.Bookings.CountAsync();

            return View();
        }
        // GET: /Dashboard/ManageCustomers - Manage customers (Admin only)

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ManageCustomers(string searchString)
        {
            var customers = _context.Customers
                .Include(c => c.Bookings)
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
        // GET: /Dashboard/Rooms - List all rooms (Admin only)
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

        // GET: /Dashboard/CreateRoom - Create new room (Admin only)
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateRoom()
        {
            ViewBag.Hotels = await _context.Hotels.ToListAsync();
            return View();
        }

        // POST: /Dashboard/CreateRoom - Create new room (Admin only)
       
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateRoom(Room room)
        {
            if (room.Quantity <= 0)
            {
                ModelState.AddModelError("Quantity", "Quantity must be greater than zero.");
            }
            try
            {
                // Log the incoming data for debugging
                Console.WriteLine($"Creating Room - HotelId: {room.HotelId}, Description: {room.RoomDescription}, Price: {room.PricePerNight}");

                // Remove validation for navigation property if it's causing issues
                ModelState.Remove("Hotel");
                ModelState.Remove("Bookings");

                // Manual validation
                if (room.HotelId <= 0)
                {
                    ModelState.AddModelError("HotelId", "Please select a hotel.");
                }

                if (string.IsNullOrEmpty(room.RoomDescription))
                {
                    ModelState.AddModelError("RoomDescription", "Please select a room type.");
                }
                else if (!new[] { "Deluxe", "Premium", "Presidential" }.Contains(room.RoomDescription))
                {
                    ModelState.AddModelError("RoomDescription", "Invalid room type selected.");
                }

                if (room.PricePerNight <= 0)
                {
                    ModelState.AddModelError("PricePerNight", "Price must be greater than 0.");
                }

                if (ModelState.IsValid)
                {
                    // Verify hotel exists
                    var hotelExists = await _context.Hotels.AnyAsync(h => h.HotelId == room.HotelId);
                    if (!hotelExists)
                    {
                        TempData["Error"] = "Selected hotel does not exist.";
                        ViewBag.Hotels = await _context.Hotels.ToListAsync();
                        return View(room);
                    }

                    _context.Add(room);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = $"Room created successfully!";
                    return RedirectToAction(nameof(Rooms));
                }

            }

            catch (DbUpdateException ex)
            {
                var innerMessage = ex.InnerException?.Message ?? ex.Message;
                TempData["Error"] = $"Database error: {innerMessage}";
                Console.WriteLine($"Error creating room: {innerMessage}");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error creating room: {ex.Message}";
                Console.WriteLine($"Error creating room: {ex.Message}");
            }

            // If we got this far, something failed, redisplay form
            ViewBag.Hotels = await _context.Hotels.ToListAsync();
            return View(room);
        }

        // GET: /Dashboard/EditRoom/{id} - Edit room (Admin only)
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> EditRoom(int id)
        {
            var room = await _context.Rooms.FindAsync(id);
            if (room == null)
            {
                return NotFound();
            }
            ViewBag.Hotels = await _context.Hotels.ToListAsync();
            return View(room);
        }

        // POST: /Dashboard/EditRoom/{id} - Edit room (Admin only)
      
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditRoom(int id, Room room)
        {
            if (id != room.RoomId)
            {
                return NotFound();
            }
           
            try
            {
                // Remove navigation properties from ModelState validation
                ModelState.Remove("Hotel");
                ModelState.Remove("Bookings");

                // Manual validation
                if (room.HotelId <= 0)
                {
                    ModelState.AddModelError("HotelId", "Please select a hotel.");
                }

                if (string.IsNullOrEmpty(room.RoomDescription))
                {
                    ModelState.AddModelError("RoomDescription", "Please select a room type.");
                }
                else if (!new[] { "Deluxe", "Premium", "Presidential" }.Contains(room.RoomDescription))
                {
                    ModelState.AddModelError("RoomDescription", "Invalid room type selected.");
                }

                if (room.PricePerNight <= 0)
                {
                    ModelState.AddModelError("PricePerNight", "Price must be greater than 0.");
                }

                if (ModelState.IsValid)
                {
                    // Check if hotel exists
                    var hotelExists = await _context.Hotels.AnyAsync(h => h.HotelId == room.HotelId);
                    if (!hotelExists)
                    {
                        ModelState.AddModelError("HotelId", "Selected hotel does not exist.");
                        ViewBag.Hotels = await _context.Hotels.ToListAsync();
                        return View(room);
                    }

                    // Get the existing room from database
                    var existingRoom = await _context.Rooms
                        .AsNoTracking()
                        .FirstOrDefaultAsync(r => r.RoomId == id);

                    if (existingRoom == null)
                    {
                        return NotFound();
                    }

                    // Update only the fields that should be changed
                    existingRoom = new Room
                    {
                        RoomId = id,
                        HotelId = room.HotelId,
                        RoomDescription = room.RoomDescription,
                        PricePerNight = room.PricePerNight,
                        Quantity = room.Quantity
                    };
                 

                    _context.Update(existingRoom);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = $"Room {id} updated successfully!";
                    return RedirectToAction(nameof(Rooms));
                }
            }
            catch (DbUpdateConcurrencyException ex)
            {
                if (!RoomExists(room.RoomId))
                {
                    return NotFound();
                }
                else
                {
                    TempData["Error"] = $"Concurrency error: {ex.Message}";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error updating room: {ex.Message}";
            }

            // If we got this far, something failed, redisplay form
            ViewBag.Hotels = await _context.Hotels.ToListAsync();
            return View(room);
        }
       

        // POST: /Dashboard/DeleteRoom/{id} - Delete room (Admin only)
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteRoom(int id)
        {
            var room = await _context.Rooms.FindAsync(id);
            if (room != null)
            {
                // Check if room has any bookings
                var hasBookings = await _context.Bookings.AnyAsync(b => b.RoomId == id);
                if (hasBookings)
                {
                    TempData["Error"] = "Cannot delete room with existing bookings.";
                    return RedirectToAction(nameof(Rooms));
                }

                _context.Rooms.Remove(room);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Room deleted successfully!";
            }
            return RedirectToAction(nameof(Rooms));
        }
        // GET: /Dashboard/CustomerDetails/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CustomerDetails(int id)
        {


            var customer = await _context.Customers
    .Include(c => c.Bookings)
        .ThenInclude(b => b.Room)
            .ThenInclude(r => r.Hotel)
    .Include(c => c.Bookings)
        .ThenInclude(b => b.Room)
            .ThenInclude(r => r.Bookings)
    .FirstOrDefaultAsync(c => c.CustomerId == id);

            if (customer == null)
            {
                return NotFound();
            }

            return View(customer);
        }
        // GET: /Dashboard/RoomDetails/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> RoomDetails(int id)
        {
            var room = await _context.Rooms
                .Include(r => r.Hotel)
                .Include(r => r.Bookings)
                    .ThenInclude(b => b.Customer)
                .FirstOrDefaultAsync(r => r.RoomId == id);

            if (room == null)
            {
                return NotFound();
            }

            ViewBag.BookedRooms = room.Bookings.Count;
            ViewBag.AvailableRooms = room.Quantity - room.Bookings.Count;

            return View(room);
        }

        // POST: /Dashboard/DeleteCustomer/{id} - Delete customer (Admin only)
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCustomer(int id)
        {
            var customer = await _context.Customers.FindAsync(id);
            if (customer != null)
            {
                // Check if customer has any bookings
                var hasBookings = await _context.Bookings.AnyAsync(b => b.CustomerId == id);
                if (hasBookings)
                {
                    TempData["Error"] = "Cannot delete customer with existing bookings.";
                    return RedirectToAction(nameof(ManageCustomers));
                }

                // Also delete the associated Identity user
                var user = await _userManager.FindByEmailAsync(customer.Email);
                if (user != null)
                {
                    await _userManager.DeleteAsync(user);
                }

                _context.Customers.Remove(customer);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Customer deleted successfully!";
            }
            return RedirectToAction(nameof(ManageCustomers));
        }

        private bool RoomExists(int id)
        {
            return _context.Rooms.Any(e => e.RoomId == id);
        }
    }
}