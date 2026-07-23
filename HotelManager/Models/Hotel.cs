using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
namespace HotelManager.Models;
public class Hotel
{
    [Key]
    public int HotelId { get; set; }

    [Required(ErrorMessage = "Hotel name is required.")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "Hotel name must be between 3 and 100 characters.")]
    [RegularExpression(@"^[a-zA-Z0-9\s]+$", ErrorMessage = "Hotel name can only contain letters, numbers and spaces.")]
    public string Name { get; set; }

    [Required(ErrorMessage = "Location is required.")]
    [StringLength(150)]
    public string Location { get; set; }

    public virtual ICollection<Room> Rooms { get; set; }
}
