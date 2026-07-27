using System;
using System.ComponentModel.DataAnnotations;

namespace HotelManager.ViewModels
{
    public class BookingViewModel
    {
        // Room Information
        public int RoomId { get; set; }

        public string HotelName { get; set; }

        public string RoomDescription { get; set; }

        public decimal PricePerNight { get; set; }

        public int Quantity { get; set; }

        // Guest Information
        [Required(ErrorMessage = "First name is required.")]
        public string FirstName { get; set; }

        [Required(ErrorMessage = "Last name is required.")]
        public string LastName { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        [Phone]
        public string PhoneNumber { get; set; }

        // Booking Dates
        [Required]
        [DataType(DataType.Date)]
        public DateTime CheckInDate { get; set; }

        [Required]
        [DataType(DataType.Date)]
        public DateTime CheckOutDate { get; set; }

        // Calculated Values
        public int Nights { get; set; }

        public decimal TotalAmount { get; set; }
      
        public bool IsAvailable { get; set; }
    }
}