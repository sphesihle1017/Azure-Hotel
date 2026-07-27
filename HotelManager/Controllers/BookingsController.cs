using HotelManager.Data;
using HotelManager.Models;
using HotelManager.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

namespace HotelManager.Controllers
{
   
    [Authorize(Roles = "User")]
    public class BookingsController : Controller
    {

        private readonly AppDbContext _context;
        private readonly UserManager<Users> _userManager;
        public BookingsController(
    AppDbContext context,
    UserManager<Users> userManager)
        {
            _context = context;
            _userManager = userManager;
        }


        public async Task<IActionResult> Create(int roomId)
        {
            var room = await _context.Rooms
                .Include(r => r.Hotel)
                .FirstOrDefaultAsync(r => r.RoomId == roomId);

            if (room == null)
            {
                return NotFound();
            }

            var model = new BookingViewModel
            {
                RoomId = room.RoomId,
                HotelName = room.Hotel.Name,
                RoomDescription = room.RoomDescription,
                PricePerNight = room.PricePerNight,
                Quantity = room.Quantity,

                CheckInDate = DateTime.Today,
                CheckOutDate = DateTime.Today.AddDays(1)
            };

            // Get logged in user
            var user = await _userManager.GetUserAsync(User);


            if (user != null)
            {
                var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.Email == model.Email);

                if (customer != null)
                {
                    model.FirstName = customer.FirstName;
                    model.LastName = customer.LastName;
                    model.Email = customer.Email;
                    model.PhoneNumber = customer.PhoneNumber;
                }
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(BookingViewModel model)
        {
            // Reload room information if validation fails
            var room = await _context.Rooms
                .Include(r => r.Hotel)
                .FirstOrDefaultAsync(r => r.RoomId == model.RoomId);

            if (room == null)
            {
                return NotFound();
            }

            model.HotelName = room.Hotel.Name;
            model.RoomDescription = room.RoomDescription;
            model.PricePerNight = room.PricePerNight;
            model.Quantity = room.Quantity;

            // Validate dates
            if (model.CheckInDate < DateTime.Today)
            {
                ModelState.AddModelError("CheckInDate", "Check-in date cannot be in the past.");
            }

            if (model.CheckOutDate <= model.CheckInDate)
            {
                ModelState.AddModelError("CheckOutDate", "Check-out date must be after check-in.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Check room availability
            int bookedRooms = await _context.Bookings.CountAsync(b =>
                b.RoomId == model.RoomId &&
                model.CheckInDate < b.CheckOutDate &&
                model.CheckOutDate > b.CheckInDate);

            if (bookedRooms >= room.Quantity)
            {
                ModelState.AddModelError("", "No rooms are available for the selected dates.");
                return View(model);
            }

            // Check if customer already exists
            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.Email == model.Email);

            if (customer == null)
            {
                customer = new Customer
                {
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    Email = model.Email,
                    PhoneNumber = model.PhoneNumber
                };

                _context.Customers.Add(customer);
                await _context.SaveChangesAsync();
            }

            // Calculate booking cost
            int nights = (model.CheckOutDate - model.CheckInDate).Days;

            decimal total = nights * room.PricePerNight;

            // Create booking
            Booking booking = new Booking
            {
                CustomerId = customer.CustomerId,
                RoomId = room.RoomId,
                CheckInDate = model.CheckInDate,
                CheckOutDate = model.CheckOutDate,
                TotalAmount = total
            };

            _context.Bookings.Add(booking);

            await _context.SaveChangesAsync();

            TempData["Success"] = "Your booking has been confirmed.";

            return RedirectToAction("MyBookings", "Dashboard");
        }
        [HttpPost]
        public async Task<IActionResult> CheckAvailability(
        int roomId,
        DateTime checkInDate,
        DateTime checkOutDate)
        {
            if (checkInDate < DateTime.Today)
            {
                return Json(new
                {
                    success = false,
                    message = "Check-in date cannot be in the past."
                });
            }

            if (checkOutDate <= checkInDate)
            {
                return Json(new
                {
                    success = false,
                    message = "Check-out must be after check-in."
                });
            }

            var room = await _context.Rooms.FindAsync(roomId);

            if (room == null)
            {
                return Json(new
                {
                    success = false,
                    message = "Room not found."
                });
            }

            int bookedRooms = await _context.Bookings.CountAsync(b =>
                b.RoomId == roomId &&
                checkInDate < b.CheckOutDate &&
                checkOutDate > b.CheckInDate);

            int availableRooms = room.Quantity - bookedRooms;

            if (availableRooms <= 0)
            {
                return Json(new
                {
                    success = false,
                    message = "No rooms available for the selected dates."
                });
            }

            int nights = (checkOutDate - checkInDate).Days;

            decimal total = nights * room.PricePerNight;

            return Json(new
            {
                success = true,
                message = $"{availableRooms} room(s) available",
                availableRooms,
                nights,
                total
            });




        }
        [Authorize(Roles = "User")]
        public async Task<IActionResult> ViewBookings()
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


        [Authorize(Roles = "User")]
        public async Task<IActionResult> Details(int id)
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
                return Unauthorized();
            }

            var booking = await _context.Bookings
                .Include(b => b.Customer)
                .Include(b => b.Room)
                    .ThenInclude(r => r.Hotel)
                .FirstOrDefaultAsync(b =>
                    b.BookingId == id &&
                    b.CustomerId == customer.CustomerId);

            if (booking == null)
            {
                return NotFound();
            }

            return View(booking);
        }
    }
}