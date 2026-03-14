using System;
using System.ComponentModel.DataAnnotations;

namespace phpMVC.Models
{
    public class Booking
    {
        public int Id { get; set; }

        public int ServiceId { get; set; }

        public int CustomerId { get; set; }

        public int ProviderId { get; set; }

        public DateTime BookingDate { get; set; }

        public string CustomerNotes { get; set; }

        // NEW: Customer's proposed price
        public decimal ProposedPrice { get; set; }

        // NEW: Final agreed price (set when provider accepts)
        public decimal? AgreedPrice { get; set; }

        public string Status { get; set; } // "pending", "accepted", "rejected", "completed"

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }


        public int? Rating { get; set; }        // Customer rating (1-5)
        public string ReviewText { get; set; }  // Customer review text

        // Navigation properties
        public string ServiceName { get; set; }
        public string ServiceDescription { get; set; }
        public decimal ServicePrice { get; set; }
        public string ServiceImage { get; set; }

        public string CustomerName { get; set; }
        public string CustomerEmail { get; set; }
        public string CustomerPhone { get; set; }

        public string ProviderName { get; set; }
        public string ProviderEmail { get; set; }
        public string ProviderPhone { get; set; }
    }

    public class CreateBookingViewModel
    {
        public int ServiceId { get; set; }

        
        public DateTime BookingDate { get; set; }

        
        public string CustomerNotes { get; set; }

      
        public decimal ProposedPrice { get; set; }

        public string ServiceName { get; set; }
        public string ServiceDescription { get; set; }
        public decimal ServicePrice { get; set; }
        public string ServiceImage { get; set; }
        public string ProviderName { get; set; }
        public string Location { get; set; }
    }
}
