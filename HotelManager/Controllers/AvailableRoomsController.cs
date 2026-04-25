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

            // Get all booked dates for this room
            ViewBag.BookedDates = await _context.Bookings
                .Where(b => b.RoomId == id)
                .Select(b => new { b.CheckInDate, b.CheckOutDate })
                .ToListAsync();

            return View(room);
        }

        [HttpPost]
        public async Task<IActionResult> CheckAvailability(int roomId, DateTime checkIn, DateTime checkOut)
        {
            // Use .Date to ignore any time component
            DateTime checkInDate = checkIn.Date;
            DateTime checkOutDate = checkOut.Date;

            if (checkInDate < DateTime.Today)
                return Json(new { available = false, message = "Check-in date cannot be in the past." });

            if (checkOutDate <= checkInDate)
                return Json(new { available = false, message = "Check-out must be after check-in." });

            // Check for overlapping bookings
            var isBooked = await _context.Bookings
                .AnyAsync(b => b.RoomId == roomId
                            && b.CheckInDate < checkOutDate
                            && b.CheckOutDate > checkInDate);

            if (isBooked)
                return Json(new { available = false, message = "Room is not available for these dates." });

            var room = await _context.Rooms.FindAsync(roomId);
            if (room == null)
                return Json(new { available = false, message = "Room not found." });

            int nights = (int)(checkOutDate - checkInDate).Days;
            decimal totalPrice = nights * room.PricePerNight;

            return Json(new
            {
                available = true,
                nights = nights,
                pricePerNight = room.PricePerNight,
                totalPrice = totalPrice,
                message = "Room is available!"
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BookRoom(
            int roomId,
            DateTime checkIn,
            DateTime checkOut,
            string firstName,
            string lastName,
            string email,
            string phoneNumber)
        {
            try
            {
                // Use .Date to ignore any time component
                DateTime checkInDate = checkIn.Date;
                DateTime checkOutDate = checkOut.Date;

                // Validate customer fields
                if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName) ||
                    string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(phoneNumber))
                    return Json(new { success = false, message = "All customer fields are required." });

                if (!email.Contains("@") || !email.Contains("."))
                    return Json(new { success = false, message = "Invalid email address." });

                if (!System.Text.RegularExpressions.Regex.IsMatch(phoneNumber, @"^0[0-9]{9}$"))
                    return Json(new { success = false, message = "Phone number must be 10 digits and start with 0." });

                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                    return Json(new { success = false, message = "User not authenticated." });

                if (checkInDate < DateTime.Today)
                    return Json(new { success = false, message = "Check-in date cannot be in the past." });

                if (checkOutDate <= checkInDate)
                    return Json(new { success = false, message = "Check-out must be after check-in." });

                var room = await _context.Rooms
                    .Include(r => r.Hotel)
                    .FirstOrDefaultAsync(r => r.RoomId == roomId);
                if (room == null)
                    return Json(new { success = false, message = "Room not found." });

                // Final availability check
                var isBooked = await _context.Bookings
                    .AnyAsync(b => b.RoomId == roomId
                                && b.CheckInDate < checkOutDate
                                && b.CheckOutDate > checkInDate);
                if (isBooked)
                    return Json(new { success = false, message = "Room is no longer available for these dates." });

                // Find or create customer
                var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Email == email);
                if (customer == null)
                {
                    customer = new Customer
                    {
                        FirstName = firstName.Trim(),
                        LastName = lastName.Trim(),
                        Email = email.Trim(),
                        PhoneNumber = phoneNumber.Trim()
                    };
                    _context.Customers.Add(customer);
                    await _context.SaveChangesAsync();
                }
                else
                {
                    customer.FirstName = firstName.Trim();
                    customer.LastName = lastName.Trim();
                    customer.PhoneNumber = phoneNumber.Trim();
                    _context.Customers.Update(customer);
                    await _context.SaveChangesAsync();
                }

                int nights = (int)(checkOutDate - checkInDate).Days;
                var booking = new Booking
                {
                    RoomId = roomId,
                    CustomerId = customer.CustomerId,
                    CheckInDate = checkInDate,
                    CheckOutDate = checkOutDate,
                    TotalAmount = nights * room.PricePerNight
                };

                _context.Bookings.Add(booking);
                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = $"Booking confirmed! {room.RoomDescription} Room at {room.Hotel.Name}",
                    bookingId = booking.BookingId
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "An error occurred: " + ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetBookedDates(int roomId)
        {
            var bookedDates = await _context.Bookings
                .Where(b => b.RoomId == roomId)
                .Select(b => new {
                    checkIn = b.CheckInDate.ToString("yyyy-MM-dd"),
                    checkOut = b.CheckOutDate.ToString("yyyy-MM-dd")
                })
                .ToListAsync();
            return Json(bookedDates);
        }
    }
}