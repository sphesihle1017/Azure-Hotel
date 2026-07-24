using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
namespace HotelManager.Models;
public class Room
{
    [Key]
    public int RoomId { get; set; }

    [Required(ErrorMessage = "Room description is required.")]
    [StringLength(50)]
    [RegularExpression(@"^(Deluxe|Premium|Presidential)$",
        ErrorMessage = "Room must be Deluxe, Premium or Presidential.")]
    public string RoomDescription { get; set; }

    [Required(ErrorMessage = "Price per night is required.")]
    [Range(1, 100000, ErrorMessage = "Price must be greater than 0.")]
    public decimal PricePerNight { get; set; }


    /*[Required(ErrorMessage = "Please enter the quantity of rooms.")]*/
    /*public int Qty { get; set; } */

    /*[Required(ErrorMessage = "Please enter the Room Number.")]*/
    /*public int RoomNumber { get; set; } */

    [Required]
    public int HotelId { get; set; }

    public virtual Hotel Hotel { get; set; }

    public virtual ICollection<Booking> Bookings { get; set; }
}
