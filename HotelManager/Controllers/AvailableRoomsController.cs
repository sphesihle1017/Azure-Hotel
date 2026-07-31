using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HotelManager.Data;
using Microsoft.AspNetCore.Identity;
using HotelManager.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace HotelManager.Controllers
{
    [Authorize(Roles = "User")]
    public class AvailableRoomsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<Users> _userManager;

        public AvailableRoomsController(AppDbContext context, UserManager<Users> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.Hotels = await _context.Hotels.OrderBy(h => h.Name).ToListAsync();

            var rooms = await _context.Rooms
                .Include(r => r.Hotel)
                .OrderBy(r => r.Hotel.Name)
                .ThenBy(r => r.PricePerNight)
                .ToListAsync();

            return View(rooms);
        }

        public async Task<IActionResult> RoomDetails(int id)
        {
            var room = await _context.Rooms
                .Include(r => r.Hotel)
                .FirstOrDefaultAsync(r => r.RoomId == id);

            if (room == null) return NotFound();

            ViewBag.BookedDates = await _context.Bookings
                .Where(b => b.RoomId == id)
                .Select(b => new
                {
                    b.CheckInDate,
                    b.CheckOutDate
                })
                .ToListAsync();

            return View(room);
        }



        
        public class AvailabilityRequest
        {
            public int RoomId { get; set; }
            public DateTime CheckIn { get; set; }
            public DateTime CheckOut { get; set; }
        }

        public class BookingRequest
        {
            public int RoomId { get; set; }
            public DateTime CheckIn { get; set; }
            public DateTime CheckOut { get; set; }

            public string FirstName { get; set; }
            public string LastName { get; set; }
            public string Email { get; set; }
            public string PhoneNumber { get; set; }
        }

        // CHECK AVAILABILITY
        [HttpPost]
        public async Task<IActionResult> CheckAvailability([FromBody] AvailabilityRequest request)
        {
            var checkIn = request.CheckIn.Date;
            var checkOut = request.CheckOut.Date;

            if (checkIn < DateTime.Today)
                return Json(new { available = false, message = "Check-in cannot be in the past." });

            if (checkOut <= checkIn)
                return Json(new { available = false, message = "Check-out must be after check-in." });

            bool isBooked = await _context.Bookings.AnyAsync(b =>
                b.RoomId == request.RoomId &&
                b.CheckInDate < checkOut &&
                b.CheckOutDate > checkIn);

            if (isBooked)
                return Json(new { available = false, message = "Room is not available." });

            var room = await _context.Rooms.FindAsync(request.RoomId);

            int nights = (checkOut - checkIn).Days;

            return Json(new
            {
                available = true,
                nights,
                totalPrice = nights * room.PricePerNight,
                message = "Room is available!"
            });
        }

        // BOOK ROOM
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BookRoom([FromForm] BookingRequest request)
        {
            try
            {
                var checkIn = request.CheckIn.Date;
                var checkOut = request.CheckOut.Date;

                if (checkIn < DateTime.Today)
                    return Json(new { success = false, message = "Invalid check-in date." });

                if (checkOut <= checkIn)
                    return Json(new { success = false, message = "Invalid check-out date." });

                var room = await _context.Rooms
                    .Include(r => r.Hotel)
                    .FirstOrDefaultAsync(r => r.RoomId == request.RoomId);

                if (room == null)
                    return Json(new { success = false, message = "Room not found." });

                bool isBooked = await _context.Bookings.AnyAsync(b =>
                    b.RoomId == request.RoomId &&
                    b.CheckInDate < checkOut &&
                    b.CheckOutDate > checkIn);

                if (isBooked)
                    return Json(new { success = false, message = "Room already booked." });

                var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Email == request.Email);

                if (customer == null)
                {
                    customer = new Customer
                    {
                        FirstName = request.FirstName,
                        LastName = request.LastName,
                        Email = request.Email,
                        PhoneNumber = request.PhoneNumber
                    };

                    _context.Customers.Add(customer);
                    await _context.SaveChangesAsync();
                }

                int nights = (checkOut - checkIn).Days;

                var booking = new Booking
                {
                    RoomId = request.RoomId,
                    CustomerId = customer.CustomerId,
                    CheckInDate = checkIn,
                    CheckOutDate = checkOut,
                    TotalAmount = nights * room.PricePerNight
                };

                _context.Bookings.Add(booking);
                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = "Booking confirmed successfully!",
                    bookingId = booking.BookingId
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}