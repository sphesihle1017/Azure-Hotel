using HotelManager.Data;
using HotelManager.Models;
using HotelManager.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelManager.Controllers
{
    [Authorize(Roles = "User")]
    public class BookingsController : Controller
    {
        private readonly AppDbContext _context;

        public BookingsController(AppDbContext context)
        {
            _context = context;
        }



        public async Task<IActionResult> Create(int roomId)
        {
            var room = await _context.Rooms
                .Include(r => r.Hotel)
                .FirstOrDefaultAsync(r => r.RoomId == roomId);

            if (room == null)
                return NotFound();

            BookingViewModel model = new BookingViewModel
            {
                RoomId = room.RoomId,
                HotelName = room.Hotel.Name,
                RoomDescription = room.RoomDescription,
                PricePerNight = room.PricePerNight,
                Quantity = room.Quantity,
                CheckInDate = DateTime.Today,
                CheckOutDate = DateTime.Today.AddDays(1)
            };

            return View(model);
        }

        //====================================================
        // POST: Bookings/Create
        //====================================================
        // POST: Bookings/Create
        //====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(BookingViewModel model)
        {
            try
            {
                Console.WriteLine("====================================");
                Console.WriteLine("BOOKING REQUEST RECEIVED");
                Console.WriteLine($"First Name : {model.FirstName}");
                Console.WriteLine($"Last Name  : {model.LastName}");
                Console.WriteLine($"Email      : {model.Email}");
                Console.WriteLine($"Phone      : {model.PhoneNumber}");
                Console.WriteLine("====================================");

                //-------------------------------------------------
                // Get Room
                //-------------------------------------------------

                var room = await _context.Rooms
                    .Include(r => r.Hotel)
                    .FirstOrDefaultAsync(r => r.RoomId == model.RoomId);

                if (room == null)
                {
                    Console.WriteLine("Room not found.");
                    return NotFound();
                }

                model.HotelName = room.Hotel.Name;
                model.RoomDescription = room.RoomDescription;
                model.PricePerNight = room.PricePerNight;
                model.Quantity = room.Quantity;

                //-------------------------------------------------
                // Validate Model
                //-------------------------------------------------

                if (!ModelState.IsValid)
                {
                    Console.WriteLine("ModelState Invalid");

                    foreach (var error in ModelState)
                    {
                        foreach (var e in error.Value.Errors)
                        {
                            Console.WriteLine($"{error.Key} : {e.ErrorMessage}");
                        }
                    }

                    return View(model);
                }

                //-------------------------------------------------
                // Validate Dates
                //-------------------------------------------------

                if (model.CheckInDate < DateTime.Today)
                {
                    ModelState.AddModelError("", "Check-in date cannot be before today.");
                    return View(model);
                }

                if (model.CheckOutDate <= model.CheckInDate)
                {
                    ModelState.AddModelError("", "Check-out date must be after check-in.");
                    return View(model);
                }

                //-------------------------------------------------
                // Check Availability
                //-------------------------------------------------

                Console.WriteLine("Checking availability...");

                int bookedRooms = await _context.Bookings.CountAsync(b =>
                    b.RoomId == room.RoomId &&
                    model.CheckInDate < b.CheckOutDate &&
                    model.CheckOutDate > b.CheckInDate);

                if (bookedRooms >= room.Quantity)
                {
                    Console.WriteLine("Room unavailable.");

                    ModelState.AddModelError("", "No rooms available.");

                    return View(model);
                }

                Console.WriteLine("Room Available");

                //-------------------------------------------------
                // Create Customer
                //-------------------------------------------------

                Console.WriteLine("Creating Customer...");

                Customer customer = new Customer
                {
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    Email = model.Email,
                    PhoneNumber = model.PhoneNumber
                };

                _context.Customers.Add(customer);

                Console.WriteLine("Saving Customer...");

                await _context.SaveChangesAsync();

                Console.WriteLine($"Customer Saved Successfully.");
                Console.WriteLine($"CustomerId = {customer.CustomerId}");

                //-------------------------------------------------
                // Create Booking
                //-------------------------------------------------

                int nights = (model.CheckOutDate - model.CheckInDate).Days;

                decimal total = nights * room.PricePerNight;

                Booking booking = new Booking
                {
                    CustomerId = customer.CustomerId,
                    RoomId = room.RoomId,
                    CheckInDate = model.CheckInDate,
                    CheckOutDate = model.CheckOutDate,
                    TotalAmount = total
                };

                Console.WriteLine("Creating Booking...");

                _context.Bookings.Add(booking);

                Console.WriteLine("Saving Booking...");

                await _context.SaveChangesAsync();

                Console.WriteLine("Booking Saved Successfully");

                TempData["Success"] = "Booking created successfully.";

                return RedirectToAction(nameof(Receipt), new
                {
                    id = booking.BookingId
                });
            }
            catch (DbUpdateException ex)
            {
                Console.WriteLine("========== DbUpdateException ==========");
                Console.WriteLine(ex.Message);

                if (ex.InnerException != null)
                {
                    Console.WriteLine("INNER EXCEPTION:");
                    Console.WriteLine(ex.InnerException.Message);
                }

                ModelState.AddModelError("", ex.InnerException?.Message ?? ex.Message);

                return View(model);
            }
            catch (Exception ex)
            {
                Console.WriteLine("========== GENERAL EXCEPTION ==========");
                Console.WriteLine(ex.ToString());

                ModelState.AddModelError("", ex.Message);

                return View(model);
            }
        }
        public async Task<IActionResult> Receipt(int id)
        {
            var booking = await _context.Bookings
                .Include(b => b.Customer)
                .Include(b => b.Room)
                    .ThenInclude(r => r.Hotel)
                .FirstOrDefaultAsync(b => b.BookingId == id);

            if (booking == null)
                return NotFound();

            return View(booking);
        }

        //====================================================
        // AJAX: Check Availability
        //====================================================

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
                    message = "Check-out date must be after check-in."
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
                message = $"{availableRooms} room(s) available.",
                availableRooms,
                nights,
                total
            });
        }

        //====================================================
        // View My Bookings
        //====================================================

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
        //====================================================
        // Booking Details
        //====================================================

        public async Task<IActionResult> Details(int id)
        {
            var booking = await _context.Bookings
                .Include(b => b.Customer)
                .Include(b => b.Room)
                    .ThenInclude(r => r.Hotel)
                .FirstOrDefaultAsync(b => b.BookingId == id);

            if (booking == null)
            {
                return NotFound();
            }

            return View(booking);
        }
    }
}