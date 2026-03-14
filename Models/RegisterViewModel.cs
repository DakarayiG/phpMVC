using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace phpMVC.Models
{
    public class RegisterViewModel
    {
       
        //public string FirstName { get; set; }

       
        //public string LastName { get; set; }

        ////[Required(ErrorMessage = "Email address is required")]
        ////[EmailAddress(ErrorMessage = "Invalid email address")]
        ////[Display(Name = "Email")]
        ////[StringLength(255, ErrorMessage = "Email cannot exceed 255 characters")]
        //public string Email { get; set; }

        ////[Required(ErrorMessage = "User type is required")]
        ////[Display(Name = "I am a")]
        //public string UserType { get; set; } // "customer" or "provider"

        ////[Display(Name = "Phone Number")]
        ////[Phone(ErrorMessage = "Invalid phone number")]
        ////[StringLength(20, ErrorMessage = "Phone number cannot exceed 20 characters")]
        //public string Phone { get; set; }

        ////[Display(Name = "WhatsApp Number")]
        ////[Phone(ErrorMessage = "Invalid WhatsApp number")]
        ////[StringLength(20, ErrorMessage = "WhatsApp number cannot exceed 20 characters")]
        //public string WhatsApp { get; set; }

        ////[Display(Name = "Address")]
        ////[StringLength(500, ErrorMessage = "Address cannot exceed 500 characters")]
        //public string Address { get; set; }

        ////[Display(Name = "Profile Picture")]
        //public IFormFile ProviderImage { get; set; }

        ////[Required(ErrorMessage = "Password is required")]
        ////[DataType(DataType.Password)]
        ////[StringLength(100, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 8)]
        ////[RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$",
        ////    ErrorMessage = "Password must contain at least one uppercase letter, one lowercase letter, and one number")]
        //public string Password { get; set; }

        ////[Required(ErrorMessage = "Please confirm your password")]
        ////[DataType(DataType.Password)]
        ////[Display(Name = "Confirm Password")]
        ////[Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        //public string ConfirmPassword { get; set; }




            // ✅ REQUIRED FIELDS - User must fill these out
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
            [Display(Name = "Email")]
            [StringLength(255, ErrorMessage = "Email cannot exceed 255 characters")]
            public string Email { get; set; }

           
            public string UserType { get; set; } // "customer" or "provider"

        [Required(ErrorMessage = "Phone number is required")]
        [RegularExpression(@"^[\d\s\-\+\(\)]{10,}$",
            ErrorMessage = "Phone number must be at least 10 characters and contain only numbers, spaces, +, -, ( )")]
        [Display(Name = "Phone Number")]
        public string Phone { get; set; }

        [Required(ErrorMessage = "Password is required")]
            [DataType(DataType.Password)]
            [StringLength(100, ErrorMessage = "Password must be at least 8 characters", MinimumLength = 8)]
            [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$",
                ErrorMessage = "Password must contain at least one uppercase letter, one lowercase letter, and one number")]
            [Display(Name = "Password")]
            public string Password { get; set; }

            [Required(ErrorMessage = "Please confirm your password")]
            [DataType(DataType.Password)]
            [Display(Name = "Confirm Password")]
            [Compare("Password", ErrorMessage = "Passwords do not match")]
            public string ConfirmPassword { get; set; }

        [RegularExpression(@"^[\d\s\-\+\(\)]{10,}$",
          ErrorMessage = "WhatsApp number must be at least 10 characters and contain only numbers, spaces, +, -, ( )")]
        [Display(Name = "WhatsApp Number")]
        public string WhatsApp { get; set; }

            [Display(Name = "Address")]
            [StringLength(500, ErrorMessage = "Address cannot exceed 500 characters")]
            public string Address { get; set; }

            
            public IFormFile? ProviderImage { get; set; }
        }
    }
