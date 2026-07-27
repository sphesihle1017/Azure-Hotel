using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
namespace HotelManager.Models;
public class Customer
{
    [Key]
    public int CustomerId { get; set; }

    [Required(ErrorMessage = "First name is required.")]
    [StringLength(50)]
    [RegularExpression(@"^[a-zA-Z]+$", ErrorMessage = "First name must contain letters only.")]
    public string FirstName { get; set; }

    [Required(ErrorMessage = "Last name is required.")]
    [StringLength(50)]
    [RegularExpression(@"^[a-zA-Z]+$", ErrorMessage = "Last name must contain letters only.")]
    public string LastName { get; set; }

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Invalid email address.")]
    public string Email { get; set; }

    [Required(ErrorMessage = "Phone number is required.")]
    [RegularExpression(@"^0[0-9]{9}$",
        ErrorMessage = "Phone number must be 10 digits and start with 0.")]
    public string PhoneNumber { get; set; }

    public virtual ICollection<Booking> Bookings { get; set; }
    
}
