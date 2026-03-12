using System;
using System.Collections.Generic;

namespace phpMVC.Models
{
    public class ProviderDashboardViewModel
    {
        // Summary Statistics
        public int PendingOrders { get; set; }
        public int AcceptedJobs { get; set; }
        public int CompletedJobs { get; set; }
        public int TotalCompleted { get; set; }
        public decimal TotalEarnings { get; set; }

        // Rating Information
        public decimal AverageRating { get; set; }
        public int TotalReviews { get; set; }
        public Dictionary<int, int> RatingBreakdown { get; set; } = new Dictionary<int, int>();

        // Recent Activity
        public List<Booking> RecentBookings { get; set; } = new List<Booking>();

        // Chart Data
        public List<string> ChartLabels { get; set; } = new List<string>();
        public List<int> PendingData { get; set; } = new List<int>();
        public List<int> AcceptedData { get; set; } = new List<int>();
        public List<int> CompletedData { get; set; } = new List<int>();
    }
}