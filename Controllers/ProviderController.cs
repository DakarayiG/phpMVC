using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;
using phpMVC.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace phpMVC.Controllers
{
    public class ProviderController : Controller
    {
        private readonly IConfiguration _configuration;

        public ProviderController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private bool IsUserLoggedIn()
        {
            return !string.IsNullOrEmpty(HttpContext.Session.GetString("UserId"));
        }

        private bool IsProvider()
        {
            string userType = HttpContext.Session.GetString("UserType");
            return userType == "provider";
        }
    
      
        // GET: /Provider/Dashboard
        [HttpGet]
        public IActionResult Dashboard()
        {
            if (!IsUserLoggedIn())
            {
                TempData["ErrorMessage"] = "You must be logged in.";
                return RedirectToAction("Login", "Account");
            }

            if (!IsProvider())
            {
                TempData["ErrorMessage"] = "Only service providers can access the dashboard.";
                return RedirectToAction("Index", "Home");
            }

            string providerId = HttpContext.Session.GetString("UserId");
            string providerName = HttpContext.Session.GetString("UserName");

            var dashboardData = GetDashboardData(providerId);

            ViewBag.ProviderName = providerName;

            return View(dashboardData);
        }

        private ProviderDashboardViewModel GetDashboardData(string providerId)
        {
            var model = new ProviderDashboardViewModel();
            var connectionString = _configuration.GetConnectionString("MySqlConnection");

            using (var connection = new MySqlConnection(connectionString))
            {
                try
                {
                    connection.Open();

                    // Get booking counts by status
                    string countQuery = @"
                        SELECT 
                            SUM(CASE WHEN Status = 'pending' THEN 1 ELSE 0 END) AS PendingCount,
                            SUM(CASE WHEN Status = 'accepted' THEN 1 ELSE 0 END) AS AcceptedCount,
                            SUM(CASE WHEN Status = 'completed' THEN 1 ELSE 0 END) AS CompletedCount,
                            SUM(CASE WHEN Status = 'completed' THEN COALESCE(AgreedPrice, ProposedPrice) ELSE 0 END) AS TotalEarnings
                        FROM bookings
                        WHERE ProviderId = @providerId";

                    using (var cmd = new MySqlCommand(countQuery, connection))
                    {
                        cmd.Parameters.AddWithValue("@providerId", providerId);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                model.PendingOrders = reader["PendingCount"] != DBNull.Value ? Convert.ToInt32(reader["PendingCount"]) : 0;
                                model.AcceptedJobs = reader["AcceptedCount"] != DBNull.Value ? Convert.ToInt32(reader["AcceptedCount"]) : 0;
                                model.CompletedJobs = reader["CompletedCount"] != DBNull.Value ? Convert.ToInt32(reader["CompletedCount"]) : 0;
                                model.TotalCompleted = model.CompletedJobs;
                                model.TotalEarnings = reader["TotalEarnings"] != DBNull.Value ? Convert.ToDecimal(reader["TotalEarnings"]) : 0;
                            }
                        }
                    }

                    // Get average rating and review count
                    string ratingQuery = @"
                        SELECT 
                            AVG(b.Rating) AS AvgRating,
                            COUNT(CASE WHEN b.Rating IS NOT NULL THEN 1 END) AS ReviewCount
                        FROM bookings b
                        WHERE b.ProviderId = @providerId AND b.Status = 'completed'";

                    using (var cmd = new MySqlCommand(ratingQuery, connection))
                    {
                        cmd.Parameters.AddWithValue("@providerId", providerId);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                model.AverageRating = reader["AvgRating"] != DBNull.Value ? Convert.ToDecimal(reader["AvgRating"]) : 0;
                                model.TotalReviews = reader["ReviewCount"] != DBNull.Value ? Convert.ToInt32(reader["ReviewCount"]) : 0;
                            }
                        }
                    }

                    // Get rating breakdown (1-5 stars)
                    string breakdownQuery = @"
                        SELECT 
                            FLOOR(Rating) AS StarRating,
                            COUNT(*) AS Count
                        FROM bookings
                        WHERE ProviderId = @providerId 
                        AND Rating IS NOT NULL
                        GROUP BY FLOOR(Rating)";

                    using (var cmd = new MySqlCommand(breakdownQuery, connection))
                    {
                        cmd.Parameters.AddWithValue("@providerId", providerId);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                int starRating = Convert.ToInt32(reader["StarRating"]);
                                int count = Convert.ToInt32(reader["Count"]);
                                model.RatingBreakdown[starRating] = count;
                            }
                        }
                    }

                    // Initialize all star levels (1-5) if not present
                    for (int i = 1; i <= 5; i++)
                    {
                        if (!model.RatingBreakdown.ContainsKey(i))
                        {
                            model.RatingBreakdown[i] = 0;
                        }
                    }

                    // Get recent bookings
                    string recentQuery = @"
                        SELECT b.Id, b.ServiceId, b.CustomerId, b.ProviderId, b.BookingDate, 
                               b.CustomerNotes, b.ProposedPrice, b.AgreedPrice, b.Status, b.CreatedAt,
                               s.Name AS ServiceName,
                               c.FirstName AS CustomerFirstName, c.LastName AS CustomerLastName
                        FROM bookings b
                        INNER JOIN service s ON b.ServiceId = s.Id
                        INNER JOIN h_users c ON b.CustomerId = c.Id
                        WHERE b.ProviderId = @providerId
                        ORDER BY b.CreatedAt DESC
                        LIMIT 10";

                    using (var cmd = new MySqlCommand(recentQuery, connection))
                    {
                        cmd.Parameters.AddWithValue("@providerId", providerId);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                model.RecentBookings.Add(new Booking
                                {
                                    Id = Convert.ToInt32(reader["Id"]),
                                    ServiceId = Convert.ToInt32(reader["ServiceId"]),
                                    CustomerId = Convert.ToInt32(reader["CustomerId"]),
                                    ProviderId = Convert.ToInt32(reader["ProviderId"]),
                                    BookingDate = Convert.ToDateTime(reader["BookingDate"]),
                                    CustomerNotes = reader["CustomerNotes"].ToString(),
                                    ProposedPrice = Convert.ToDecimal(reader["ProposedPrice"]),
                                    AgreedPrice = reader["AgreedPrice"] != DBNull.Value ? Convert.ToDecimal(reader["AgreedPrice"]) : (decimal?)null,
                                    Status = reader["Status"].ToString(),
                                    CreatedAt = Convert.ToDateTime(reader["CreatedAt"]),
                                    ServiceName = reader["ServiceName"].ToString(),
                                    CustomerName = $"{reader["CustomerFirstName"]} {reader["CustomerLastName"]}"
                                });
                            }
                        }
                    }

                    // Get chart data (last 7 days)
                    var chartData = GetChartData(providerId, connection);
                    model.ChartLabels = chartData.Labels;
                    model.PendingData = chartData.PendingData;
                    model.AcceptedData = chartData.AcceptedData;
                    model.CompletedData = chartData.CompletedData;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading dashboard data: {ex.Message}");
                }
            }

            return model;
        }

        private (List<string> Labels, List<int> PendingData, List<int> AcceptedData, List<int> CompletedData) GetChartData(string providerId, MySqlConnection connection)
        {
            var labels = new List<string>();
            var pendingData = new List<int>();
            var acceptedData = new List<int>();
            var completedData = new List<int>();

            // Get last 7 days
            for (int i = 6; i >= 0; i--)
            {
                var date = DateTime.Now.Date.AddDays(-i);
                labels.Add(date.ToString("ddd"));

                string query = @"
                    SELECT 
                        SUM(CASE WHEN Status = 'pending' THEN 1 ELSE 0 END) AS Pending,
                        SUM(CASE WHEN Status = 'accepted' THEN 1 ELSE 0 END) AS Accepted,
                        SUM(CASE WHEN Status = 'completed' THEN 1 ELSE 0 END) AS Completed
                    FROM bookings
                    WHERE ProviderId = @providerId
                    AND DATE(CreatedAt) = @date";

                using (var cmd = new MySqlCommand(query, connection))
                {
                    cmd.Parameters.AddWithValue("@providerId", providerId);
                    cmd.Parameters.AddWithValue("@date", date);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            pendingData.Add(reader["Pending"] != DBNull.Value ? Convert.ToInt32(reader["Pending"]) : 0);
                            acceptedData.Add(reader["Accepted"] != DBNull.Value ? Convert.ToInt32(reader["Accepted"]) : 0);
                            completedData.Add(reader["Completed"] != DBNull.Value ? Convert.ToInt32(reader["Completed"]) : 0);
                        }
                        else
                        {
                            pendingData.Add(0);
                            acceptedData.Add(0);
                            completedData.Add(0);
                        }
                    }
                }
            }

            return (labels, pendingData, acceptedData, completedData);
        }
    }
}