using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace phpMVC.Models
{
    public class ProviderServiceViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Service title is required")]
        [Display(Name = "Service Title")]
        public string ServiceTitle { get; set; }

        [Display(Name = "Service Type")]
        public string? ServiceType { get; set; }

        [Required(ErrorMessage = "Description is required")]
        [Display(Name = "Description")]
        public string JobDescription { get; set; }

        [Required(ErrorMessage = "Location is required")]
        [Display(Name = "Location")]
        public string Location { get; set; }

        [Required(ErrorMessage = "Duration is required")]
        [Display(Name = "Duration")]
        public string Duration { get; set; }

        [Display(Name = "Availability")]
        public string? Availability { get; set; }

        [Required(ErrorMessage = "Price is required")]
        [Range(0.01, 999999.99, ErrorMessage = "Price must be between $0.01 and $999,999.99")]
        [Display(Name = "Price")]
        public decimal Price { get; set; }

        [Display(Name = "Price Type")]
        public string PriceType { get; set; } = "fixed";

        public double Rating { get; set; }
        public int ReviewCount { get; set; }

        [Display(Name = "Service Image")]
        public IFormFile? ServiceImage { get; set; }

        public string? ServiceImagePath { get; set; }

        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}