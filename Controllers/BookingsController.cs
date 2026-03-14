using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;
using phpMVC.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace phpMVC.Controllers
{
    public class BookingsController : Controller
    {
        private readonly IConfiguration _configuration;

        public BookingsController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // CHECK IF USER IS LOGGED IN
        private bool IsUserLoggedIn()
        {
            return !string.IsNullOrEmpty(HttpContext.Session.GetString("UserId"));
        }

        // CHECK IF USER IS A CUSTOMER
        private bool IsCustomer()
        {
            string userType = HttpContext.Session.GetString("UserType");
            return userType == "customer";
        }

        // CHECK IF USER IS A PROVIDER
        private bool IsProvider()
        {
            string userType = HttpContext.Session.GetString("UserType");
            return userType == "provider";
        }

        // GET: /Bookings/Book/{serviceId}
        [HttpGet]
        public IActionResult Book(int serviceId)
        {
            if (!IsUserLoggedIn())
            {
                TempData["ErrorMessage"] = "You must be logged in to book a service.";
                return RedirectToAction("Login", "Account", new { returnUrl = $"/Bookings/Book/{serviceId}" });
            }

            if (!IsCustomer())
            {
                TempData["ErrorMessage"] = "Only customers can book services.";
                return RedirectToAction("Index", "Services");
            }

            var model = GetServiceDetailsForBooking(serviceId);

            if (model == null)
            {
                TempData["ErrorMessage"] = "Service not found.";
                return RedirectToAction("Index", "Services");
            }

            // Set default proposed price to the service's listed price
            model.ProposedPrice = model.ServicePrice;

            ViewBag.MinDate = DateTime.Now.AddDays(1).ToString("yyyy-MM-dd");

            return View(model);
        }

        // POST: /Bookings/Book
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Book(CreateBookingViewModel model)
        {
            Console.WriteLine("========== BOOKING SUBMISSION ==========");
            Console.WriteLine($"ServiceId: {model.ServiceId}");
            Console.WriteLine($"BookingDate: {model.BookingDate}");
            Console.WriteLine($"ProposedPrice: {model.ProposedPrice}");
            Console.WriteLine($"CustomerNotes: {model.CustomerNotes}");
            Console.WriteLine($"ModelState.IsValid (before clear): {ModelState.IsValid}");

            if (!IsUserLoggedIn())
            {
                TempData["ErrorMessage"] = "You must be logged in to book a service.";
                return RedirectToAction("Login", "Account");
            }

            if (!IsCustomer())
            {
                TempData["ErrorMessage"] = "Only customers can book services.";
                return RedirectToAction("Index", "Services");
            }

            // ✅ FIX: Clear ALL model validation errors
            ModelState.Clear();

            // REPOPULATE SERVICE DETAILS
            var serviceDetails = GetServiceDetailsForBooking(model.ServiceId);
            if (serviceDetails != null)
            {
                model.ServiceName = serviceDetails.ServiceName;
                model.ServiceDescription = serviceDetails.ServiceDescription;
                model.ServicePrice = serviceDetails.ServicePrice;
                model.ServiceImage = serviceDetails.ServiceImage;
                model.ProviderName = serviceDetails.ProviderName;
                model.Location = serviceDetails.Location;
            }

            ViewBag.MinDate = DateTime.Now.AddDays(1).ToString("yyyy-MM-dd");

            // Manual validation
            if (model.BookingDate == DateTime.MinValue || model.BookingDate.Date < DateTime.Now.Date)
            {
                Console.WriteLine("❌ Booking date is in the past");
                ModelState.AddModelError("BookingDate", "Booking date cannot be in the past.");
                return View(model);
            }

            if (string.IsNullOrWhiteSpace(model.CustomerNotes))
            {
                Console.WriteLine("❌ Customer notes are empty");
                ModelState.AddModelError("CustomerNotes", "Please describe what you need.");
                return View(model);
            }

            if (model.ProposedPrice <= 0)
            {
                Console.WriteLine("❌ Proposed price is invalid");
                ModelState.AddModelError("ProposedPrice", "Please enter a valid price.");
                return View(model);
            }

            try
            {
                string customerId = HttpContext.Session.GetString("UserId");
                Console.WriteLine($"Customer ID: {customerId}");

                await CreateBooking(model, customerId);

                Console.WriteLine("✅ Booking created successfully!");
                TempData["SuccessMessage"] = "Booking request sent successfully! The provider will review your request and proposed price.";
                return RedirectToAction("BookingSuccess", new { serviceId = model.ServiceId });
            }
            catch (MySqlException sqlEx)
            {
                Console.WriteLine($"❌ MySQL ERROR: {sqlEx.Message}");
                Console.WriteLine($"Error Code: {sqlEx.Number}");

                // Check if it's the "column not found" error
                if (sqlEx.Message.Contains("Unknown column") || sqlEx.Number == 1054)
                {
                    ModelState.AddModelError("", "Database schema error: The 'ProposedPrice' column doesn't exist in the bookings table. Please run the ALTER TABLE script.");
                }
                else
                {
                    ModelState.AddModelError("", $"Database error: {sqlEx.Message}");
                }
                return View(model);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ GENERAL ERROR: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                ModelState.AddModelError("", "An error occurred while creating your booking. Please try again.");
                return View(model);
            }
        }

        public IActionResult BookingSuccess(int serviceId)
        {
            if (!IsUserLoggedIn())
            {
                return RedirectToAction("Login", "Account");
            }

            ViewBag.ServiceId = serviceId;
            return View();
        }

        [HttpGet]
        public IActionResult Orders()
        {
            if (!IsUserLoggedIn())
            {
                TempData["ErrorMessage"] = "You must be logged in to view orders.";
                return RedirectToAction("Login", "Account");
            }

            if (!IsProvider())
            {
                TempData["ErrorMessage"] = "Only service providers can view orders.";
                return RedirectToAction("Index", "Home");
            }

            string providerId = HttpContext.Session.GetString("UserId");
            string providerName = HttpContext.Session.GetString("UserName");

            // DEBUG: Log the provider info
            Console.WriteLine("========== DEBUG ORDERS ==========");
            Console.WriteLine($"Logged in Provider ID: '{providerId}'");
            Console.WriteLine($"Logged in Provider Name: '{providerName}'");

            // Get all orders for this provider to see what's there
            var allOrders = GetAllProviderOrders(providerId);

            Console.WriteLine($"Total orders found for provider {providerId}: {allOrders.Count}");
            foreach (var order in allOrders)
            {
                Console.WriteLine($"Order ID: {order.Id}, Status: '{order.Status}', Customer: {order.CustomerName}");
            }

            var pendingOrders = allOrders.Where(o => o.Status?.ToLower() == "pending").ToList();
            Console.WriteLine($"Pending orders found: {pendingOrders.Count}");
            Console.WriteLine("==================================");

            // Pass the count to ViewBag for debugging
            ViewBag.DebugProviderId = providerId;
            ViewBag.DebugTotalOrders = allOrders.Count;
            ViewBag.DebugPendingOrders = pendingOrders.Count;

            return View(pendingOrders);
        }

        // GET: /Bookings/Orders (Provider view - pending orders) - Original method kept for reference
        //[HttpGet]
        //public IActionResult Orders()
        //{
        //    if (!IsUserLoggedIn())
        //    {
        //        TempData["ErrorMessage"] = "You must be logged in to view orders.";
        //        return RedirectToAction("Login", "Account");
        //    }
        //
        //    if (!IsProvider())
        //    {
        //        TempData["ErrorMessage"] = "Only service providers can view orders.";
        //        return RedirectToAction("Index", "Home");
        //    }
        //
        //    string providerId = HttpContext.Session.GetString("UserId");
        //    var orders = GetProviderOrders(providerId, "pending");
        //
        //    return View(orders);
        //}

        // POST: /Bookings/AcceptBooking
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AcceptBooking(int bookingId)
        {
            if (!IsProvider())
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            try
            {
                string providerId = HttpContext.Session.GetString("UserId");

                // NEW: When accepting, set the AgreedPrice to the ProposedPrice
                await AcceptBookingWithPrice(bookingId, providerId);

                TempData["SuccessMessage"] = "Booking accepted successfully!";
                return Json(new { success = true, message = "Booking accepted" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error accepting booking: {ex.Message}");
                return Json(new { success = false, message = "Error accepting booking" });
            }
        }

        // POST: /Bookings/RejectBooking
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectBooking(int bookingId)
        {
            if (!IsProvider())
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            try
            {
                string providerId = HttpContext.Session.GetString("UserId");
                await UpdateBookingStatus(bookingId, providerId, "rejected");

                TempData["SuccessMessage"] = "Booking rejected.";
                return Json(new { success = true, message = "Booking rejected" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error rejecting booking: {ex.Message}");
                return Json(new { success = false, message = "Error rejecting booking" });
            }
        }
        [HttpGet]
        public IActionResult AcceptedBookings()
        {
            if (!IsUserLoggedIn())
            {
                TempData["ErrorMessage"] = "You must be logged in to view accepted bookings.";
                return RedirectToAction("Login", "Account");
            }

            if (!IsProvider())
            {
                TempData["ErrorMessage"] = "Only service providers can view accepted bookings.";
                return RedirectToAction("Index", "Home");
            }

            string providerId = HttpContext.Session.GetString("UserId");
            string providerName = HttpContext.Session.GetString("UserName");

            // DEBUG: Log the provider info
            Console.WriteLine("========== DEBUG ACCEPTED BOOKINGS ==========");
            Console.WriteLine($"Logged in Provider ID: '{providerId}'");
            Console.WriteLine($"Logged in Provider Name: '{providerName}'");

            // Get all orders for this provider to see what's there
            var allOrders = GetAllProviderOrders(providerId);

            Console.WriteLine($"Total orders found for provider {providerId}: {allOrders.Count}");
            foreach (var order in allOrders)
            {
                Console.WriteLine($"Order ID: {order.Id}, Status: '{order.Status}', Customer: {order.CustomerName}");
            }

            var acceptedOrders = allOrders.Where(o => o.Status?.ToLower() == "accepted").ToList();
            Console.WriteLine($"Accepted orders found: {acceptedOrders.Count}");
            Console.WriteLine("==============================================");

            // Pass the count to ViewBag for debugging
            ViewBag.DebugProviderId = providerId;
            ViewBag.DebugTotalOrders = allOrders.Count;
            ViewBag.DebugAcceptedOrders = acceptedOrders.Count;

            return View(acceptedOrders);
        }
        //[HttpGet]
        //public IActionResult AcceptedBookings()
        //{
        //    if (!IsUserLoggedIn())
        //    {
        //        TempData["ErrorMessage"] = "You must be logged in to view accepted bookings.";
        //        return RedirectToAction("Login", "Account");
        //    }

        //    if (!IsProvider())
        //    {
        //        TempData["ErrorMessage"] = "Only service providers can view accepted bookings.";
        //        return RedirectToAction("Index", "Home");
        //    }

        //    string providerId = HttpContext.Session.GetString("UserId");
        //    var acceptedBookings = GetProviderOrders(providerId, "accepted");

        //    return View(acceptedBookings);
        //}

        [HttpGet]
        public IActionResult MyBookings()
        {
            if (!IsUserLoggedIn())
            {
                TempData["ErrorMessage"] = "You must be logged in to view your bookings.";
                return RedirectToAction("Login", "Account");
            }

            if (!IsCustomer())
            {
                TempData["ErrorMessage"] = "Only customers can view their bookings.";
                return RedirectToAction("Index", "Home");
            }

            string customerId = HttpContext.Session.GetString("UserId");
            var bookings = GetCustomerBookings(customerId);

            return View(bookings);
        }
        [HttpGet]
        public IActionResult CompletedBookings()
        {
            if (!IsUserLoggedIn())
            {
                TempData["ErrorMessage"] = "You must be logged in to view completed jobs.";
                return RedirectToAction("Login", "Account");
            }

            if (!IsProvider())
            {
                TempData["ErrorMessage"] = "Only service providers can view completed jobs.";
                return RedirectToAction("Index", "Home");
            }

            string providerId = HttpContext.Session.GetString("UserId");

             //Get all orders for this provider
            var allOrders = GetAllProviderOrders(providerId);

            var completedOrders = allOrders.Where(o => o.Status?.ToLower() == "completed").ToList();

            return View(completedOrders);
        }










        //// GET: /Bookings/CompletedBookings (Provider view - completed jobs)
        //[HttpGet]
        //public IActionResult CompletedBookings()
        //{
        //    if (!IsUserLoggedIn())
        //    {
        //        TempData["ErrorMessage"] = "You must be logged in to view completed jobs.";
        //        return RedirectToAction("Login", "Account");
        //    }

        //    if (!IsProvider())
        //    {
        //        TempData["ErrorMessage"] = "Only service providers can view completed jobs.";
        //        return RedirectToAction("Index", "Home");
        //    }

        //    string providerId = HttpContext.Session.GetString("UserId");
        //    var completedBookings = GetProviderOrders(providerId, "completed");

        //    return View(completedBookings);
        //}


        // POST: /Bookings/CompleteBooking
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompleteBooking(int bookingId)
        {
            if (!IsProvider())
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            try
            {
                string providerId = HttpContext.Session.GetString("UserId");
                await UpdateBookingStatus(bookingId, providerId, "completed");

                TempData["SuccessMessage"] = "Job marked as completed! Customer will be notified to rate the service.";
                return Json(new { success = true, message = "Job completed" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error completing booking: {ex.Message}");
                return Json(new { success = false, message = "Error completing booking" });
            }
        }

        // GET: /Bookings/CompletedBookings
        //[HttpGet]
        //public IActionResult CompletedBookings()
        //{
        //    if (!IsUserLoggedIn())
        //    {
        //        TempData["ErrorMessage"] = "You must be logged in.";
        //        return RedirectToAction("Login", "Account");
        //    }

        //    if (!IsProvider())
        //    {
        //        TempData["ErrorMessage"] = "Only service providers can view completed bookings.";
        //        return RedirectToAction("Index", "Home");
        //    }

        //    string providerId = HttpContext.Session.GetString("UserId");
        //    var completedBookings = GetProviderOrders(providerId, "completed");

        //    return View(completedBookings);
        //}

        // GET: /Bookings/PendingRatings
        [HttpGet]
        public IActionResult PendingRatings()
        {
            if (!IsUserLoggedIn())
            {
                return RedirectToAction("Login", "Account");
            }

            if (!IsCustomer())
            {
                return RedirectToAction("Index", "Home");
            }

            string customerId = HttpContext.Session.GetString("UserId");
            var pendingRatings = GetPendingRatingsForCustomer(customerId);

            return View(pendingRatings);
        }

        // POST: /Bookings/SubmitRating
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitRating(int bookingId, decimal rating, string reviewText)
        {
            if (!IsCustomer())
            {
                return Json(new { success = false, message = "Only customers can rate services" });
            }

            try
            {
                string customerId = HttpContext.Session.GetString("UserId");
                await SaveRating(bookingId, customerId, rating, reviewText);

                TempData["SuccessMessage"] = "Thank you for your rating!";
                return Json(new { success = true, message = "Rating submitted" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error submitting rating: {ex.Message}");
                return Json(new { success = false, message = "Error submitting rating" });
            }
        }

        // HELPER: Get pending ratings
        private List<Booking> GetPendingRatingsForCustomer(string customerId)
        {
            var bookings = new List<Booking>();
            var connectionString = _configuration.GetConnectionString("MySqlConnection");

            using (var connection = new MySqlConnection(connectionString))
            {
                try
                {
                    connection.Open();

                    string query = @"
                SELECT b.Id, b.ServiceId, b.BookingDate, b.ProposedPrice, b.AgreedPrice, 
                       b.Status, b.CreatedAt, b.UpdatedAt,
                       s.Name AS ServiceName, s.serviceImages AS ServiceImage,
                       p.FirstName AS ProviderFirstName, p.LastName AS ProviderLastName
                FROM bookings b
                INNER JOIN service s ON b.ServiceId = s.Id
                INNER JOIN h_users p ON b.ProviderId = p.Id
                WHERE b.CustomerId = @customerId 
                AND b.Status = 'completed' 
                AND b.Rating IS NULL
                ORDER BY b.UpdatedAt DESC";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@customerId", customerId);

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                bookings.Add(new Booking
                                {
                                    Id = Convert.ToInt32(reader["Id"]),
                                    ServiceId = Convert.ToInt32(reader["ServiceId"]),
                                    BookingDate = Convert.ToDateTime(reader["BookingDate"]),
                                    ProposedPrice = Convert.ToDecimal(reader["ProposedPrice"]),
                                    AgreedPrice = reader["AgreedPrice"] != DBNull.Value ? Convert.ToDecimal(reader["AgreedPrice"]) : (decimal?)null,
                                    Status = reader["Status"].ToString(),
                                    CreatedAt = Convert.ToDateTime(reader["CreatedAt"]),
                                    UpdatedAt = reader["UpdatedAt"] != DBNull.Value ? Convert.ToDateTime(reader["UpdatedAt"]) : (DateTime?)null,
                                    ServiceName = reader["ServiceName"].ToString(),
                                    ServiceImage = NormalizeImageUrl(reader["ServiceImage"]?.ToString() ?? ""),  // ✅ FIXED
                                    ProviderName = $"{reader["ProviderFirstName"]} {reader["ProviderLastName"]}"
                                });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading pending ratings: {ex.Message}");
                }
            }

            return bookings;
        }

        // HELPER: Save rating
        private async Task SaveRating(int bookingId, string customerId, decimal rating, string reviewText)
        {
            var connectionString = _configuration.GetConnectionString("MySqlConnection");

            using (var connection = new MySqlConnection(connectionString))
            {
                await connection.OpenAsync();

                string query = @"
            UPDATE bookings 
            SET Rating = @rating, 
                ReviewText = @reviewText, 
                RatedAt = NOW(),
                UpdatedAt = NOW()
            WHERE Id = @bookingId 
            AND CustomerId = @customerId 
            AND Status = 'completed'
            AND Rating IS NULL";

                using (var cmd = new MySqlCommand(query, connection))
                {
                    cmd.Parameters.AddWithValue("@rating", rating);
                    cmd.Parameters.AddWithValue("@reviewText", reviewText ?? "");
                    cmd.Parameters.AddWithValue("@bookingId", bookingId);
                    cmd.Parameters.AddWithValue("@customerId", customerId);

                    int rowsAffected = await cmd.ExecuteNonQueryAsync();

                    if (rowsAffected == 0)
                    {
                        throw new Exception("Unable to submit rating.");
                    }
                }

                // Update provider's average rating
                await UpdateProviderAverageRating(bookingId, connection);
            }
        }

        // HELPER: Update provider average rating
        private async Task UpdateProviderAverageRating(int bookingId, MySqlConnection connection)
        {
            // Get provider ID and service ID
            string getProviderQuery = "SELECT ProviderId, ServiceId FROM bookings WHERE Id = @bookingId";
            int providerId, serviceId;

            using (var cmd = new MySqlCommand(getProviderQuery, connection))
            {
                cmd.Parameters.AddWithValue("@bookingId", bookingId);
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (!reader.Read())
                        return;

                    providerId = Convert.ToInt32(reader["ProviderId"]);
                    serviceId = Convert.ToInt32(reader["ServiceId"]);
                }
            }

            // Calculate average rating
            string avgQuery = @"
        SELECT AVG(Rating) as AvgRating, COUNT(*) as ReviewCount
        FROM bookings 
        WHERE ServiceId = @serviceId 
        AND Rating IS NOT NULL";

            decimal avgRating = 0;
            int reviewCount = 0;

            using (var cmd = new MySqlCommand(avgQuery, connection))
            {
                cmd.Parameters.AddWithValue("@serviceId", serviceId);
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (reader.Read() && reader["AvgRating"] != DBNull.Value)
                    {
                        avgRating = Convert.ToDecimal(reader["AvgRating"]);
                        reviewCount = Convert.ToInt32(reader["ReviewCount"]);
                    }
                }
            }

            // Update service table
            string updateServiceQuery = @"
        UPDATE service 
        SET rating = @avgRating, 
            reviewcount = @reviewCount,
            updated_at = NOW()
        WHERE Id = @serviceId";

            using (var cmd = new MySqlCommand(updateServiceQuery, connection))
            {
                cmd.Parameters.AddWithValue("@avgRating", avgRating);
                cmd.Parameters.AddWithValue("@reviewCount", reviewCount);
                cmd.Parameters.AddWithValue("@serviceId", serviceId);
                await cmd.ExecuteNonQueryAsync();
            }
        }
        // POST: /Bookings/CompleteBooking



        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> CompleteBooking(int bookingId)
        //{
        //    if (!IsProvider())
        //    {
        //        return Json(new { success = false, message = "Unauthorized" });
        //    }

        //    try
        //    {
        //        string providerId = HttpContext.Session.GetString("UserId");

        //        var connectionString = _configuration.GetConnectionString("MySqlConnection");

        //        using (var connection = new MySqlConnection(connectionString))
        //        {
        //            await connection.OpenAsync();

        //            string query = @"
        //        UPDATE bookings 
        //        SET Status = 'completed', 
        //            UpdatedAt = NOW() 
        //        WHERE Id = @bookingId 
        //        AND ProviderId = @providerId 
        //        AND Status = 'accepted'";

        //            using (var cmd = new MySqlCommand(query, connection))
        //            {
        //                cmd.Parameters.AddWithValue("@bookingId", bookingId);
        //                cmd.Parameters.AddWithValue("@providerId", providerId);

        //                int rowsAffected = await cmd.ExecuteNonQueryAsync();

        //                if (rowsAffected > 0)
        //                {
        //                    TempData["SuccessMessage"] = "Job marked as completed!";
        //                    return Json(new { success = true, message = "Job completed successfully" });
        //                }
        //                else
        //                {
        //                    return Json(new { success = false, message = "Booking not found or cannot be completed" });
        //                }
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"Error completing booking: {ex.Message}");
        //        return Json(new { success = false, message = "Error completing job" });
        //    }
        //}

        // POST: /Bookings/MarkComplete
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkComplete(int bookingId, int rating, string reviewText)
        {
            if (!IsCustomer())
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            try
            {
                string customerId = HttpContext.Session.GetString("UserId");

                var connectionString = _configuration.GetConnectionString("MySqlConnection");

                using (var connection = new MySqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    // First check if the booking exists and belongs to this customer
                    string checkQuery = "SELECT Id FROM bookings WHERE Id = @bookingId AND CustomerId = @customerId AND Status = 'accepted'";
                    using (var checkCmd = new MySqlCommand(checkQuery, connection))
                    {
                        checkCmd.Parameters.AddWithValue("@bookingId", bookingId);
                        checkCmd.Parameters.AddWithValue("@customerId", customerId);

                        var exists = await checkCmd.ExecuteScalarAsync();
                        if (exists == null)
                        {
                            return Json(new { success = false, message = "Booking not found or cannot be completed" });
                        }
                    }

                    // Update the booking with rating, review, and completed status
                    string updateQuery = @"
                UPDATE bookings 
                SET Status = 'completed', 
                    Rating = @rating,
                    ReviewText = @reviewText,
                    UpdatedAt = NOW() 
                WHERE Id = @bookingId 
                AND CustomerId = @customerId";

                    using (var cmd = new MySqlCommand(updateQuery, connection))
                    {
                        cmd.Parameters.AddWithValue("@bookingId", bookingId);
                        cmd.Parameters.AddWithValue("@customerId", customerId);
                        cmd.Parameters.AddWithValue("@rating", rating);
                        cmd.Parameters.AddWithValue("@reviewText", reviewText ?? "");

                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                TempData["SuccessMessage"] = "Thank you for your feedback! Job marked as completed.";
                return Json(new { success = true, message = "Job completed and rated successfully" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error completing booking: {ex.Message}");
                return Json(new { success = false, message = "Error completing job" });
            }
        }

        // HELPER METHODS

        private CreateBookingViewModel GetServiceDetailsForBooking(int serviceId)
        {
            var connectionString = _configuration.GetConnectionString("MySqlConnection");

            using (var connection = new MySqlConnection(connectionString))
            {
                try
                {
                    connection.Open();

                    string query = @"
                        SELECT s.Id, s.Name, s.Description, s.price, s.location, s.serviceImages,
                               u.FirstName, u.LastName
                        FROM service s
                        INNER JOIN h_users u ON s.ProviderId = u.Id
                        WHERE s.Id = @serviceId AND s.IsActive = 1";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@serviceId", serviceId);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return new CreateBookingViewModel
                                {
                                    ServiceId = Convert.ToInt32(reader["Id"]),
                                    ServiceName = reader["Name"].ToString(),
                                    ServiceDescription = reader["Description"].ToString(),
                                    ServicePrice = Convert.ToDecimal(reader["price"]),
                                    Location = reader["location"].ToString(),
                                    ServiceImage = NormalizeImageUrl(reader["serviceImages"]?.ToString() ?? ""),
                                    ProviderName = $"{reader["FirstName"]} {reader["LastName"]}"
                                };
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error loading service details: {ex.Message}");
                }
            }

            return null;
        }

        private async Task CreateBooking(CreateBookingViewModel model, string customerId)
        {
            var connectionString = _configuration.GetConnectionString("MySqlConnection");

            using (var connection = new MySqlConnection(connectionString))
            {
                await connection.OpenAsync();
                Console.WriteLine("✅ Database connection opened");

                // Get provider ID from service
                string getProviderQuery = "SELECT ProviderId FROM service WHERE Id = @serviceId";
                int providerId;

                using (var cmd = new MySqlCommand(getProviderQuery, connection))
                {
                    cmd.Parameters.AddWithValue("@serviceId", model.ServiceId);
                    var result = await cmd.ExecuteScalarAsync();

                    if (result == null)
                    {
                        throw new Exception($"Service with ID {model.ServiceId} not found");
                    }

                    providerId = Convert.ToInt32(result);
                    Console.WriteLine($"Provider ID: {providerId}");
                }

                // NEW: Insert booking with ProposedPrice
                string insertQuery = @"
                    INSERT INTO bookings 
                    (ServiceId, CustomerId, ProviderId, BookingDate, CustomerNotes, ProposedPrice, Status, CreatedAt)
                    VALUES 
                    (@serviceId, @customerId, @providerId, @bookingDate, @customerNotes, @proposedPrice, 'pending', NOW())";

                using (var cmd = new MySqlCommand(insertQuery, connection))
                {
                    cmd.Parameters.AddWithValue("@serviceId", model.ServiceId);
                    cmd.Parameters.AddWithValue("@customerId", customerId);
                    cmd.Parameters.AddWithValue("@providerId", providerId);
                    cmd.Parameters.AddWithValue("@bookingDate", model.BookingDate);
                    cmd.Parameters.AddWithValue("@customerNotes", model.CustomerNotes ?? "");
                    cmd.Parameters.AddWithValue("@proposedPrice", model.ProposedPrice);

                    Console.WriteLine($"Executing INSERT with ProposedPrice: ${model.ProposedPrice}...");
                    int rowsAffected = await cmd.ExecuteNonQueryAsync();
                    Console.WriteLine($"✅ Rows affected: {rowsAffected}");

                    if (rowsAffected == 0)
                    {
                        throw new Exception("Failed to insert booking - 0 rows affected");
                    }
                }
            }
        }

        private List<Booking> GetAllProviderOrders(string providerId)
        {
            var bookings = new List<Booking>();
            var connectionString = _configuration.GetConnectionString("MySqlConnection");

            using (var connection = new MySqlConnection(connectionString))
            {
                try
                {
                    connection.Open();

                    string query = @"
                SELECT b.Id, b.ServiceId, b.CustomerId, b.ProviderId, b.BookingDate, 
                       b.CustomerNotes, b.ProposedPrice, b.AgreedPrice, b.Status, b.CreatedAt,
                       s.Name AS ServiceName, s.Description AS ServiceDescription, 
                       s.price AS ServicePrice, s.serviceImages AS ServiceImage,
                       c.FirstName AS CustomerFirstName, c.LastName AS CustomerLastName,
                       c.Email AS CustomerEmail, c.Phone AS CustomerPhone
                FROM bookings b
                INNER JOIN service s ON b.ServiceId = s.Id
                INNER JOIN h_users c ON b.CustomerId = c.Id
                WHERE b.ProviderId = @providerId
                ORDER BY b.CreatedAt DESC";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@providerId", providerId);

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                bookings.Add(new Booking
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
                                    ServiceDescription = reader["ServiceDescription"].ToString(),
                                    ServicePrice = Convert.ToDecimal(reader["ServicePrice"]),
                                    ServiceImage = NormalizeImageUrl(reader["ServiceImage"]?.ToString() ?? ""),
                                    CustomerName = $"{reader["CustomerFirstName"]} {reader["CustomerLastName"]}",
                                    CustomerEmail = reader["CustomerEmail"].ToString(),
                                    CustomerPhone = reader["CustomerPhone"]?.ToString() ?? ""
                                });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading provider orders: {ex.Message}");
                }
            }

            return bookings;
        }

        private List<Booking> GetProviderOrders(string providerId, string status)
        {
            var bookings = new List<Booking>();
            var connectionString = _configuration.GetConnectionString("MySqlConnection");

            using (var connection = new MySqlConnection(connectionString))
            {
                try
                {
                    connection.Open();

                    // Updated query to include Rating and ReviewText
                    string query = @"
                SELECT b.Id, b.ServiceId, b.CustomerId, b.ProviderId, b.BookingDate, 
                       b.CustomerNotes, b.ProposedPrice, b.AgreedPrice, b.Status, 
                       b.CreatedAt, b.UpdatedAt, b.Rating, b.ReviewText,
                       s.Name AS ServiceName, s.Description AS ServiceDescription, 
                       s.price AS ServicePrice, s.serviceImages AS ServiceImage,
                       c.FirstName AS CustomerFirstName, c.LastName AS CustomerLastName,
                       c.Email AS CustomerEmail, c.Phone AS CustomerPhone
                FROM bookings b
                INNER JOIN service s ON b.ServiceId = s.Id
                INNER JOIN h_users c ON b.CustomerId = c.Id
                WHERE b.ProviderId = @providerId AND b.Status = @status
                ORDER BY b.CreatedAt DESC";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@providerId", providerId);
                        cmd.Parameters.AddWithValue("@status", status);

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                bookings.Add(new Booking
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
                                    UpdatedAt = reader["UpdatedAt"] != DBNull.Value ? Convert.ToDateTime(reader["UpdatedAt"]) : (DateTime?)null,

                                    // NEW: Add Rating and ReviewText
                                    Rating = reader["Rating"] != DBNull.Value ? Convert.ToInt32(reader["Rating"]) : (int?)null,
                                    ReviewText = reader["ReviewText"]?.ToString(),

                                    ServiceName = reader["ServiceName"].ToString(),
                                    ServiceDescription = reader["ServiceDescription"].ToString(),
                                    ServicePrice = Convert.ToDecimal(reader["ServicePrice"]),
                                    ServiceImage = NormalizeImageUrl(reader["ServiceImage"]?.ToString() ?? ""),
                                    CustomerName = $"{reader["CustomerFirstName"]} {reader["CustomerLastName"]}",
                                    CustomerEmail = reader["CustomerEmail"].ToString(),
                                    CustomerPhone = reader["CustomerPhone"]?.ToString() ?? ""
                                });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading provider orders: {ex.Message}");
                }
            }

            return bookings;
        }

        private List<Booking> GetCustomerBookings(string customerId)
        {
            var bookings = new List<Booking>();
            var connectionString = _configuration.GetConnectionString("MySqlConnection");

            using (var connection = new MySqlConnection(connectionString))
            {
                try
                {
                    connection.Open();

                    // NEW: Include ProposedPrice and AgreedPrice in query
                    string query = @"
                        SELECT b.Id, b.ServiceId, b.CustomerId, b.ProviderId, b.BookingDate, 
                               b.CustomerNotes, b.ProposedPrice, b.AgreedPrice, b.Status, b.CreatedAt,
                               s.Name AS ServiceName, s.Description AS ServiceDescription, 
                               s.price AS ServicePrice, s.serviceImages AS ServiceImage,
                               p.FirstName AS ProviderFirstName, p.LastName AS ProviderLastName,
                               p.Email AS ProviderEmail, p.Phone AS ProviderPhone
                        FROM bookings b
                        INNER JOIN service s ON b.ServiceId = s.Id
                        INNER JOIN h_users p ON b.ProviderId = p.Id
                        WHERE b.CustomerId = @customerId
                        ORDER BY b.CreatedAt DESC";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@customerId", customerId);

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                bookings.Add(new Booking
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
                                    ServiceDescription = reader["ServiceDescription"].ToString(),
                                    ServicePrice = Convert.ToDecimal(reader["ServicePrice"]),
                                    ServiceImage = NormalizeImageUrl(reader["ServiceImage"]?.ToString() ?? ""),
                                    ProviderName = $"{reader["ProviderFirstName"]} {reader["ProviderLastName"]}",
                                    ProviderEmail = reader["ProviderEmail"].ToString(),
                                    ProviderPhone = reader["ProviderPhone"]?.ToString() ?? ""
                                });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading customer bookings: {ex.Message}");
                }
            }

            return bookings;
        }

        // NEW: Accept booking and set agreed price
        private async Task AcceptBookingWithPrice(int bookingId, string providerId)
        {
            var connectionString = _configuration.GetConnectionString("MySqlConnection");

            using (var connection = new MySqlConnection(connectionString))
            {
                await connection.OpenAsync();

                // Update status to 'accepted' and set AgreedPrice to ProposedPrice
                string query = @"
                    UPDATE bookings 
                    SET Status = 'accepted', 
                        AgreedPrice = ProposedPrice, 
                        UpdatedAt = NOW() 
                    WHERE Id = @bookingId AND ProviderId = @providerId";

                using (var cmd = new MySqlCommand(query, connection))
                {
                    cmd.Parameters.AddWithValue("@bookingId", bookingId);
                    cmd.Parameters.AddWithValue("@providerId", providerId);

                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        private async Task UpdateBookingStatus(int bookingId, string providerId, string status)
        {
            var connectionString = _configuration.GetConnectionString("MySqlConnection");

            using (var connection = new MySqlConnection(connectionString))
            {
                await connection.OpenAsync();

                string query = @"
                    UPDATE bookings 
                    SET Status = @status, UpdatedAt = NOW() 
                    WHERE Id = @bookingId AND ProviderId = @providerId";

                using (var cmd = new MySqlCommand(query, connection))
                {
                    cmd.Parameters.AddWithValue("@status", status);
                    cmd.Parameters.AddWithValue("@bookingId", bookingId);
                    cmd.Parameters.AddWithValue("@providerId", providerId);

                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        // IMAGE NORMALIZATION METHODS
        private string NormalizeImageUrl(string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
                return GetDefaultServiceImage();

            imagePath = imagePath.Trim();

            // Already an absolute external URL — use as-is
            if (imagePath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                imagePath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return imagePath;
            }

            // Strip any leading ~, / or combination
            imagePath = imagePath.TrimStart('~').TrimStart('/');

            // If path already starts with UploadedImages/, just prepend /
            if (imagePath.StartsWith("UploadedImages/", StringComparison.OrdinalIgnoreCase))
            {
                return "/" + imagePath;
            }

            // If it looks like a full path with subfolders but no UploadedImages prefix
            // e.g. "Services/filename.jpg"
            if (imagePath.Contains("/"))
            {
                return "/UploadedImages/" + imagePath;
            }

            // Plain filename only — e.g. "abc123.jpg"
            return "/UploadedImages/" + imagePath;
        }

        private string GetDefaultServiceImage()
        {
            return "https://images.unsplash.com/photo-1558618666-fcd25c85cd64?ixlib=rb-4.0.3&auto=format&fit=crop&w=500&q=80";
        }
    }
}