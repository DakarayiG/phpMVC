using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;
using phpMVC.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using System.Linq;

namespace phpMVC.Controllers
{
    public class ProviderServicesController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;

        public ProviderServicesController(IConfiguration configuration, IWebHostEnvironment environment)
        {
            _configuration = configuration;
            _environment = environment;
        }

        private bool IsProviderLoggedIn()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("UserId")))
                return false;
            return HttpContext.Session.GetString("UserType") == "provider";
        }

        private string GetCurrentUserId()
        {
            return HttpContext.Session.GetString("UserId");
        }

        // GET: /ProviderServices
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            if (!IsProviderLoggedIn())
            {
                TempData["ErrorMessage"] = "You must be logged in as a provider.";
                return RedirectToAction("Login", "Account");
            }

            string providerId = GetCurrentUserId();
            var services = await LoadProviderServices(providerId);
            return View(services);
        }

        // GET: /ProviderServices/Edit/5
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            if (!IsProviderLoggedIn())
            {
                return RedirectToAction("Login", "Account");
            }

            string providerId = GetCurrentUserId();
            var service = await LoadServiceById(id, providerId);

            if (service == null)
            {
                TempData["ErrorMessage"] = "Service not found or you don't have permission to edit it.";
                return RedirectToAction("Index");
            }

            return View(service);
        }

        // POST: /ProviderServices/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ProviderServiceViewModel model)
        {
            if (!IsProviderLoggedIn())
            {
                return RedirectToAction("Login", "Account");
            }

            string providerId = GetCurrentUserId();

            // Manual validation
            if (string.IsNullOrWhiteSpace(model.ServiceTitle))
                ModelState.AddModelError("ServiceTitle", "Service title is required");

            if (string.IsNullOrWhiteSpace(model.JobDescription))
                ModelState.AddModelError("JobDescription", "Description is required");

            if (string.IsNullOrWhiteSpace(model.Location))
                ModelState.AddModelError("Location", "Location is required");

            if (model.Price <= 0)
                ModelState.AddModelError("Price", "Price must be greater than 0");

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                // Handle image upload if new image provided
                string imagePath = null;
                if (model.ServiceImage != null && model.ServiceImage.Length > 0)
                {
                    imagePath = await HandleImageUpload(model.ServiceImage, providerId);
                }

                await UpdateService(id, providerId, model, imagePath);

                TempData["SuccessMessage"] = "Service updated successfully!";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating service: {ex.Message}");
                TempData["ErrorMessage"] = $"Error updating service: {ex.Message}";
                return View(model);
            }
        }

        // POST: /ProviderServices/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            if (!IsProviderLoggedIn())
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            string providerId = GetCurrentUserId();

            try
            {
                await DeleteService(id, providerId);
                return Json(new { success = true, message = "Service deleted successfully" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting service: {ex.Message}");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // POST: /ProviderServices/ToggleStatus/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            if (!IsProviderLoggedIn())
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            string providerId = GetCurrentUserId();

            try
            {
                bool newStatus = await ToggleServiceStatus(id, providerId);
                string statusMessage = newStatus ? "activated" : "deactivated";
                return Json(new { success = true, isActive = newStatus, message = $"Service {statusMessage} successfully" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error toggling status: {ex.Message}");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // HELPER METHODS

        private async Task<List<ProviderServiceViewModel>> LoadProviderServices(string providerId)
        {
            var services = new List<ProviderServiceViewModel>();
            var connectionString = _configuration.GetConnectionString("MySqlConnection");

            using (var connection = new MySqlConnection(connectionString))
            {
                await connection.OpenAsync();

                string query = @"
                    SELECT Id, Name, Description, location, duration, availability, 
                           rating, reviewcount, price, serviceImages, IsActive, created_at
                    FROM service 
                    WHERE ProviderId = @providerId
                    ORDER BY created_at DESC";

                using (var cmd = new MySqlCommand(query, connection))
                {
                    cmd.Parameters.AddWithValue("@providerId", providerId);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            services.Add(new ProviderServiceViewModel
                            {
                                Id = Convert.ToInt32(reader["Id"]),
                                ServiceTitle = reader["Name"].ToString(),
                                JobDescription = reader["Description"].ToString(),
                                Location = reader["location"].ToString(),
                                Duration = reader["duration"].ToString(),
                                Availability = reader["availability"]?.ToString() ?? "",
                                Price = Convert.ToDecimal(reader["price"]),
                                PriceType = "fixed",
                                Rating = Convert.ToDouble(reader["rating"]),
                                ReviewCount = Convert.ToInt32(reader["reviewcount"]),
                                ServiceImagePath = reader["serviceImages"]?.ToString(),
                                IsActive = Convert.ToBoolean(reader["IsActive"]),
                                CreatedAt = Convert.ToDateTime(reader["created_at"])
                            });
                        }
                    }
                }
            }

            return services;
        }

        private async Task<ProviderServiceViewModel> LoadServiceById(int serviceId, string providerId)
        {
            var connectionString = _configuration.GetConnectionString("MySqlConnection");

            using (var connection = new MySqlConnection(connectionString))
            {
                await connection.OpenAsync();

                string query = @"
                    SELECT Id, Name, Description, location, duration, availability, 
                           price, serviceImages, IsActive, rating, reviewcount, created_at
                    FROM service 
                    WHERE Id = @serviceId AND ProviderId = @providerId";

                using (var cmd = new MySqlCommand(query, connection))
                {
                    cmd.Parameters.AddWithValue("@serviceId", serviceId);
                    cmd.Parameters.AddWithValue("@providerId", providerId);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return new ProviderServiceViewModel
                            {
                                Id = Convert.ToInt32(reader["Id"]),
                                ServiceTitle = reader["Name"].ToString(),
                                JobDescription = reader["Description"].ToString(),
                                Location = reader["location"].ToString(),
                                Duration = reader["duration"].ToString(),
                                Availability = reader["availability"]?.ToString() ?? "",
                                Price = Convert.ToDecimal(reader["price"]),
                                ServiceImagePath = reader["serviceImages"]?.ToString(),
                                IsActive = Convert.ToBoolean(reader["IsActive"]),
                                Rating = Convert.ToDouble(reader["rating"]),
                                ReviewCount = Convert.ToInt32(reader["reviewcount"]),
                                CreatedAt = Convert.ToDateTime(reader["created_at"])
                            };
                        }
                    }
                }
            }

            return null;
        }

        private async Task<string> HandleImageUpload(IFormFile imageFile, string providerId)
        {
            if (imageFile == null || imageFile.Length == 0)
                return null;

            // Validate file
            if (imageFile.Length > 5 * 1024 * 1024)
                throw new Exception("File size exceeds 5MB limit.");

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var extension = Path.GetExtension(imageFile.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(extension))
                throw new Exception("Invalid file type. Allowed: JPG, PNG, GIF, WEBP.");

            // Save file
            var uploadsFolder = Path.Combine(_environment.WebRootPath, "UploadedImages", "services");
            Directory.CreateDirectory(uploadsFolder);

            var uniqueFileName = $"service_{providerId}_{Guid.NewGuid():N}{extension}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await imageFile.CopyToAsync(stream);
            }

            return $"/UploadedImages/services/{uniqueFileName}";
        }

        private async Task UpdateService(int serviceId, string providerId, ProviderServiceViewModel model, string newImagePath)
        {
            var connectionString = _configuration.GetConnectionString("MySqlConnection");

            using (var connection = new MySqlConnection(connectionString))
            {
                await connection.OpenAsync();

                string query;
                if (!string.IsNullOrEmpty(newImagePath))
                {
                    query = @"
                        UPDATE service SET 
                            Name = @name,
                            Description = @description,
                            location = @location,
                            duration = @duration,
                            availability = @availability,
                            price = @price,
                            serviceImages = @image,
                            updated_at = NOW()
                        WHERE Id = @serviceId AND ProviderId = @providerId";
                }
                else
                {
                    query = @"
                        UPDATE service SET 
                            Name = @name,
                            Description = @description,
                            location = @location,
                            duration = @duration,
                            availability = @availability,
                            price = @price,
                            updated_at = NOW()
                        WHERE Id = @serviceId AND ProviderId = @providerId";
                }

                using (var cmd = new MySqlCommand(query, connection))
                {
                    cmd.Parameters.AddWithValue("@serviceId", serviceId);
                    cmd.Parameters.AddWithValue("@providerId", providerId);
                    cmd.Parameters.AddWithValue("@name", model.ServiceTitle ?? "");
                    cmd.Parameters.AddWithValue("@description", model.JobDescription ?? "");
                    cmd.Parameters.AddWithValue("@location", model.Location ?? "");
                    cmd.Parameters.AddWithValue("@duration", model.Duration ?? "");
                    cmd.Parameters.AddWithValue("@availability", model.Availability ?? "");
                    cmd.Parameters.AddWithValue("@price", model.Price);

                    if (!string.IsNullOrEmpty(newImagePath))
                    {
                        cmd.Parameters.AddWithValue("@image", newImagePath);
                    }

                    int rowsAffected = await cmd.ExecuteNonQueryAsync();
                    if (rowsAffected == 0)
                        throw new Exception("No changes were made to the service.");
                }
            }
        }

        private async Task DeleteService(int serviceId, string providerId)
        {
            var connectionString = _configuration.GetConnectionString("MySqlConnection");

            using (var connection = new MySqlConnection(connectionString))
            {
                await connection.OpenAsync();
                using (var transaction = await connection.BeginTransactionAsync())
                {
                    try
                    {
                        // First, get the image path to delete the file
                        string selectQuery = "SELECT serviceImages FROM service WHERE Id = @serviceId AND ProviderId = @providerId";
                        string imagePath = null;

                        using (var selectCmd = new MySqlCommand(selectQuery, connection, transaction))
                        {
                            selectCmd.Parameters.AddWithValue("@serviceId", serviceId);
                            selectCmd.Parameters.AddWithValue("@providerId", providerId);
                            var result = await selectCmd.ExecuteScalarAsync();
                            imagePath = result?.ToString();
                        }

                        // Check if service has any bookings - prevent deletion if it does
                        string checkBookingsQuery = "SELECT COUNT(*) FROM bookings WHERE ServiceId = @serviceId";
                        using (var checkCmd = new MySqlCommand(checkBookingsQuery, connection, transaction))
                        {
                            checkCmd.Parameters.AddWithValue("@serviceId", serviceId);
                            int bookingCount = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());

                            if (bookingCount > 0)
                            {
                                throw new Exception("Cannot delete service with existing bookings. You can deactivate it instead.");
                            }
                        }

                        // Delete from database
                        string deleteQuery = "DELETE FROM service WHERE Id = @serviceId AND ProviderId = @providerId";
                        using (var deleteCmd = new MySqlCommand(deleteQuery, connection, transaction))
                        {
                            deleteCmd.Parameters.AddWithValue("@serviceId", serviceId);
                            deleteCmd.Parameters.AddWithValue("@providerId", providerId);

                            int rowsAffected = await deleteCmd.ExecuteNonQueryAsync();
                            if (rowsAffected == 0)
                                throw new Exception("Service not found or you don't have permission to delete it.");
                        }

                        // Commit transaction
                        await transaction.CommitAsync();

                        // Delete image file if it exists (after successful DB delete)
                        if (!string.IsNullOrEmpty(imagePath))
                        {
                            try
                            {
                                string fullPath = Path.Combine(_environment.WebRootPath, imagePath.TrimStart('/'));
                                if (System.IO.File.Exists(fullPath))
                                {
                                    System.IO.File.Delete(fullPath);
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error deleting image file: {ex.Message}");
                            }
                        }
                    }
                    catch
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                }
            }
        }

        private async Task<bool> ToggleServiceStatus(int serviceId, string providerId)
        {
            var connectionString = _configuration.GetConnectionString("MySqlConnection");

            using (var connection = new MySqlConnection(connectionString))
            {
                await connection.OpenAsync();

                // Get current status
                string selectQuery = "SELECT IsActive FROM service WHERE Id = @serviceId AND ProviderId = @providerId";
                bool currentStatus = false;

                using (var selectCmd = new MySqlCommand(selectQuery, connection))
                {
                    selectCmd.Parameters.AddWithValue("@serviceId", serviceId);
                    selectCmd.Parameters.AddWithValue("@providerId", providerId);
                    var result = await selectCmd.ExecuteScalarAsync();
                    if (result == null)
                        throw new Exception("Service not found");
                    currentStatus = Convert.ToBoolean(result);
                }

                // Toggle status
                bool newStatus = !currentStatus;
                string updateQuery = "UPDATE service SET IsActive = @status, updated_at = NOW() WHERE Id = @serviceId AND ProviderId = @providerId";

                using (var updateCmd = new MySqlCommand(updateQuery, connection))
                {
                    updateCmd.Parameters.AddWithValue("@serviceId", serviceId);
                    updateCmd.Parameters.AddWithValue("@providerId", providerId);
                    updateCmd.Parameters.AddWithValue("@status", newStatus);

                    await updateCmd.ExecuteNonQueryAsync();
                }

                return newStatus;
            }
        }
    }
}