using System;
using System.ComponentModel.DataAnnotations;

namespace HotelManager.ViewModels;
public class BookingViewModel
{
    // Room information
    public int RoomId { get; set; }

    public string? HotelName { get; set; }

    public string? RoomDescription { get; set; }

    public decimal PricePerNight { get; set; }

    public int Quantity { get; set; }

    // Customer information
    [Required]
    public string FirstName { get; set; } = "";

    [Required]
    public string LastName { get; set; } = "";

    [Required]
    [EmailAddress]
    public string Email { get; set; } = "";

    [Required]
    [Phone]
    public string PhoneNumber { get; set; } = "";

    // Dates
    [Required]
    public DateTime CheckInDate { get; set; }

    [Required]
    public DateTime CheckOutDate { get; set; }

    // Calculated values
    public int Nights { get; set; }

    public decimal TotalAmount { get; set; }

    public bool IsAvailable { get; set; }
}
