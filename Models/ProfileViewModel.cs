using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace phpMVC.Models
{
    public class ProfileViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "First name is required")]
        [StringLength(50, ErrorMessage = "First name cannot exceed 50 characters")]
        [Display(Name = "First Name")]
        public string FirstName { get; set; }

        [Required(ErrorMessage = "Last name is required")]
        [StringLength(50, ErrorMessage = "Last name cannot exceed 50 characters")]
        [Display(Name = "Last Name")]
        public string LastName { get; set; }

        [Required(ErrorMessage = "Email address is required")]
        [EmailAddress(ErrorMessage = "Invalid email address")]
        [Display(Name = "Email Address")]
        [StringLength(255, ErrorMessage = "Email cannot exceed 255 characters")]
        public string Email { get; set; }

        // UserType kept but NOT required - just for display purposes
        public string UserType { get; set; }

        [Required(ErrorMessage = "Phone number is required")]
        [RegularExpression(@"^[\d\s\-\+\(\)]{10,}$",
            ErrorMessage = "Phone number must be at least 10 characters and contain only numbers, spaces, +, -, ( )")]
        [Display(Name = "Phone Number")]
        public string Phone { get; set; }

        [RegularExpression(@"^[\d\s\-\+\(\)]{10,}$",
            ErrorMessage = "WhatsApp number must be at least 10 characters and contain only numbers, spaces, +, -, ( )")]
        [Display(Name = "WhatsApp Number")]
        public string WhatsApp { get; set; }

        [Display(Name = "Address")]
        [StringLength(500, ErrorMessage = "Address cannot exceed 500 characters")]
        public string Address { get; set; }

        [Display(Name = "Profile Picture")]
        public IFormFile? ProfileImage { get; set; }

        public string? ProfileImagePath { get; set; }

        // Statistics - for display only, not editable
        public int TotalServices { get; set; }
        public int TotalBookings { get; set; }
        public double AverageRating { get; set; }
        public int MemberSince { get; set; }
        public DateTime JoinedDate { get; set; }
    }

    public class ChangePasswordViewModel
    {
        [Required(ErrorMessage = "Current password is required")]
        [DataType(DataType.Password)]
        [Display(Name = "Current Password")]
        public string CurrentPassword { get; set; }

        [Required(ErrorMessage = "New password is required")]
        [DataType(DataType.Password)]
        [StringLength(100, ErrorMessage = "Password must be at least 8 characters", MinimumLength = 8)]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$",
            ErrorMessage = "Password must contain at least one uppercase letter, one lowercase letter, and one number")]
        [Display(Name = "New Password")]
        public string NewPassword { get; set; }

        [Required(ErrorMessage = "Please confirm your new password")]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm New Password")]
        [Compare("NewPassword", ErrorMessage = "Passwords do not match")]
        public string ConfirmNewPassword { get; set; }
    }
}