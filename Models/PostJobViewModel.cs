using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace phpMVC.Models
{
    public class PostJobViewModel
    {
        //    [Display(Name = "Service Title")]
        //    public string ServiceTitle { get; set; }

        //    [Display(Name = "Service Type")]
        //    public string ServiceType { get; set; }

        //    [Display(Name = "Price")]
        //    public decimal Price { get; set; }

        //    [Display(Name = "Price Type")]
        //    public string PriceType { get; set; } = "hourly";

        //    [Display(Name = "Location")]
        //    public string Location { get; set; }

        //    [Display(Name = "Duration")]
        //    public string Duration { get; set; }

        //    [Display(Name = "Description")]
        //    public string JobDescription { get; set; }

        //    [Display(Name = "Availability")]
        //    public string Availability { get; set; }

        //    [Display(Name = "Rating")]
        //    public decimal Rating { get; set; } = 0;

        //    [Display(Name = "Review Count")]
        //    public int ReviewCount { get; set; } = 0;

        //    [Display(Name = "Service Image")]
        //    [DataType(DataType.Upload)]
        //    public IFormFile ServiceImage { get; set; }

        //    // For displaying the uploaded image path
        //    public string ServiceImagePath { get; set; }
        //}
       
            // ✅ REQUIRED USER INPUTS - These must be validated
            [Required(ErrorMessage = "Service title is required")]
            [StringLength(200, ErrorMessage = "Service title cannot exceed 200 characters")]
            [Display(Name = "Service Title")]
            public string ServiceTitle { get; set; }

            [StringLength(100, ErrorMessage = "Service type cannot exceed 100 characters")]
            [Display(Name = "Service Type")]
            public string ServiceType { get; set; }

            [Required(ErrorMessage = "Price is required")]
            [Range(0.01, 999999.99, ErrorMessage = "Price must be between $0.01 and $999,999.99")]
            [Display(Name = "Price")]
            public decimal Price { get; set; }

            [Required(ErrorMessage = "Price type is required")]
            [Display(Name = "Price Type")]
            public string PriceType { get; set; } = "hourly";

            [Required(ErrorMessage = "Location is required")]
            [StringLength(200, ErrorMessage = "Location cannot exceed 200 characters")]
            [Display(Name = "Location")]
            public string Location { get; set; }

            [Required(ErrorMessage = "Duration is required")]
            [StringLength(100, ErrorMessage = "Duration cannot exceed 100 characters")]
            [Display(Name = "Duration")]
            public string Duration { get; set; }

            [Required(ErrorMessage = "Description is required")]
            [StringLength(2000, ErrorMessage = "Description must be between 20 - 2000 characters", MinimumLength = 20)]
            [Display(Name = "Description")]
            public string JobDescription { get; set; }

            [Required(ErrorMessage = "Availability is required")]
            [StringLength(200, ErrorMessage = "Availability cannot exceed 200 characters")]
            [Display(Name = "Availability")]
            public string Availability { get; set; }

            // ❌ NEVER ADD [Required] TO THESE - They are set by the system, not the user!
            [Display(Name = "Rating")]
            public decimal Rating { get; set; } = 0;

            [Display(Name = "Review Count")]
            public int ReviewCount { get; set; } = 0;

          
            public IFormFile ServiceImage { get; set; }

            public string? ServiceImagePath { get; set; }
        }
    }
