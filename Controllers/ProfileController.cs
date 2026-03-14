using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;
using phpMVC.Models;
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System.Security.Cryptography;
using System.Text;
using System.Collections.Generic;

namespace phpMVC.Controllers
{
    public class ProfileController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;

        public ProfileController(IConfiguration configuration, IWebHostEnvironment environment)
        {
            _configuration = configuration;
            _environment = environment;
        }

        // CHECK IF USER IS LOGGED IN
        private bool IsUserLoggedIn()
        {
            return !string.IsNullOrEmpty(HttpContext.Session.GetString("UserId"));
        }

        private string GetCurrentUserId()
        {
            return HttpContext.Session.GetString("UserId");
        }

        private string GetCurrentUserType()
        {
            return HttpContext.Session.GetString("UserType");
        }

        // Hash password
        private string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
              return Convert.ToBase64String(hashedBytes); // 👈 Use Base64 to match AccountController

            }
        }

        // GET: /Profile
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            if (!IsUserLoggedIn())
            {
                TempData["ErrorMessage"] = "You must be logged in to view your profile.";
                return RedirectToAction("Login", "Account");
            }

            var profile = await LoadUserProfile(GetCurrentUserId());

            if (profile == null)
            {
                TempData["ErrorMessage"] = "Profile not found.";
                return RedirectToAction("Index", "Home");
            }

            return View(profile);
        }

        // GET: /Profile/Edit
        [HttpGet]
        public async Task<IActionResult> Edit()
        {
            if (!IsUserLoggedIn())
            {
                TempData["ErrorMessage"] = "You must be logged in to edit your profile.";
                return RedirectToAction("Login", "Account");
            }

            var profile = await LoadUserProfile(GetCurrentUserId());

            if (profile == null)
            {
                TempData["ErrorMessage"] = "Profile not found.";
                return RedirectToAction("Index", "Home");
            }

            return View(profile);
        }

        // POST: /Profile/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ProfileViewModel model)
        {
            ModelState.Remove("Email");
            ModelState.Remove("UserType");

            if (!IsUserLoggedIn())
            {
                TempData["ErrorMessage"] = "You must be logged in to edit your profile.";
                return RedirectToAction("Login", "Account");
            }

            string userId = GetCurrentUserId();

            // Manual validation
            if (string.IsNullOrWhiteSpace(model.FirstName))
                ModelState.AddModelError("FirstName", "First name is required");

            if (string.IsNullOrWhiteSpace(model.LastName))
                ModelState.AddModelError("LastName", "Last name is required");

            if (string.IsNullOrWhiteSpace(model.Phone))
                ModelState.AddModelError("Phone", "Phone number is required");

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                // Handle profile image upload
                string profileImagePath = null;
                if (model.ProfileImage != null && model.ProfileImage.Length > 0)
                {
                    profileImagePath = await HandleProfileImageUpload(model.ProfileImage, userId);
                    Console.WriteLine($"✅ New image uploaded: {profileImagePath}");
                }
                else
                {
                    Console.WriteLine("ℹ️ No new image uploaded");
                }

                // Update user profile in database
                await UpdateUserProfile(model, userId, profileImagePath);

                // Update session
                HttpContext.Session.SetString("UserEmail", model.Email);
                HttpContext.Session.SetString("UserName", $"{model.FirstName} {model.LastName}");

                TempData["SuccessMessage"] = "Profile updated successfully!";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
                TempData["ErrorMessage"] = $"Error updating profile: {ex.Message}";
                return View(model);
            }
        }

        // GET: /Profile/ChangePassword
        [HttpGet]
        public IActionResult ChangePassword()
        {
            if (!IsUserLoggedIn())
            {
                return RedirectToAction("Login", "Account");
            }

            return View(new ChangePasswordViewModel());
        }

        // POST: /Profile/ChangePassword
        // POST: /Profile/ChangePassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!IsUserLoggedIn())
            {
                return RedirectToAction("Login", "Account");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                string userId = GetCurrentUserId();

                // NOW using the SAME hashing method as AccountController
                string currentPasswordHash = HashPassword(model.CurrentPassword);
                string newPasswordHash = HashPassword(model.NewPassword);

                var connectionString = _configuration.GetConnectionString("MySqlConnection");

                using (var connection = new MySqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    // Verify current password
                    string verifyQuery = "SELECT COUNT(*) FROM h_users WHERE Id = @userId AND Password = @password";
                    using (var verifyCmd = new MySqlCommand(verifyQuery, connection))
                    {
                        verifyCmd.Parameters.AddWithValue("@userId", userId);
                        verifyCmd.Parameters.AddWithValue("@password", currentPasswordHash);

                        int count = Convert.ToInt32(await verifyCmd.ExecuteScalarAsync());
                        if (count == 0)
                        {
                            ModelState.AddModelError("CurrentPassword", "Current password is incorrect.");
                            return View(model);
                        }
                    }

                    // Update password
                    string updateQuery = "UPDATE h_users SET Password = @newPassword, UpdatedAt = NOW() WHERE Id = @userId";
                    using (var updateCmd = new MySqlCommand(updateQuery, connection))
                    {
                        updateCmd.Parameters.AddWithValue("@userId", userId);
                        updateCmd.Parameters.AddWithValue("@newPassword", newPasswordHash);

                        int rowsAffected = await updateCmd.ExecuteNonQueryAsync();
                        if (rowsAffected > 0)
                        {
                            TempData["SuccessMessage"] = "Password changed successfully!";
                            return RedirectToAction("Index");
                        }
                        else
                        {
                            throw new Exception("Failed to update password.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error changing password: {ex.Message}";
                return View(model);
            }
        }

        // HELPER METHODS

        private async Task<ProfileViewModel> LoadUserProfile(string userId)
        {
            var connectionString = _configuration.GetConnectionString("MySqlConnection");
            var profile = new ProfileViewModel();

            using (var connection = new MySqlConnection(connectionString))
            {
                try
                {
                    await connection.OpenAsync();

                    string query = @"
                        SELECT Id, FirstName, LastName, Email, UserType, Phone, WhatsApp, 
                               Address, ProviderImage, CreatedAt
                        FROM h_users 
                        WHERE Id = @userId";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@userId", userId);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                profile.Id = Convert.ToInt32(reader["Id"]);
                                profile.FirstName = reader["FirstName"]?.ToString() ?? "";
                                profile.LastName = reader["LastName"]?.ToString() ?? "";
                                profile.Email = reader["Email"]?.ToString() ?? "";
                                profile.UserType = reader["UserType"]?.ToString() ?? "";
                                profile.Phone = reader["Phone"]?.ToString() ?? "";
                                profile.WhatsApp = reader["WhatsApp"]?.ToString() ?? "";
                                profile.Address = reader["Address"]?.ToString() ?? "";
                                profile.ProfileImagePath = reader["ProviderImage"]?.ToString() ?? "/images/default-avatar.png";
                                profile.JoinedDate = Convert.ToDateTime(reader["CreatedAt"]);
                                profile.MemberSince = profile.JoinedDate.Year;
                            }
                            else
                            {
                                return null;
                            }
                        }
                    }

                    // Load statistics
                    if (profile.UserType == "provider")
                    {
                        await LoadProviderStatistics(connection, userId, profile);
                    }
                    else
                    {
                        await LoadCustomerStatistics(connection, userId, profile);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading profile: {ex.Message}");
                    return null;
                }
            }

            return profile;
        }

        private async Task LoadProviderStatistics(MySqlConnection connection, string userId, ProfileViewModel profile)
        {
            try
            {
                string servicesQuery = "SELECT COUNT(*) FROM service WHERE ProviderId = @userId AND IsActive = 1";
                using (var cmd = new MySqlCommand(servicesQuery, connection))
                {
                    cmd.Parameters.AddWithValue("@userId", userId);
                    profile.TotalServices = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                }

                string bookingsQuery = "SELECT COUNT(*) FROM bookings WHERE ProviderId = @userId";
                using (var cmd = new MySqlCommand(bookingsQuery, connection))
                {
                    cmd.Parameters.AddWithValue("@userId", userId);
                    profile.TotalBookings = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                }

                profile.AverageRating = 0;
            }
            catch
            {
                profile.TotalServices = 0;
                profile.TotalBookings = 0;
                profile.AverageRating = 0;
            }
        }

        private async Task LoadCustomerStatistics(MySqlConnection connection, string userId, ProfileViewModel profile)
        {
            try
            {
                string bookingsQuery = "SELECT COUNT(*) FROM bookings WHERE CustomerId = @userId";
                using (var cmd = new MySqlCommand(bookingsQuery, connection))
                {
                    cmd.Parameters.AddWithValue("@userId", userId);
                    profile.TotalBookings = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                }
            }
            catch
            {
                profile.TotalBookings = 0;
            }
        }

        private async Task<string> HandleProfileImageUpload(IFormFile imageFile, string userId)
        {
            if (imageFile == null || imageFile.Length == 0)
            {
                return null;
            }

            try
            {
                // Validate file size (5MB)
                if (imageFile.Length > 5 * 1024 * 1024)
                {
                    throw new Exception("File size exceeds 5MB limit.");
                }

                // Validate file type
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                var extension = Path.GetExtension(imageFile.FileName).ToLowerInvariant();

                if (string.IsNullOrEmpty(extension) || !allowedExtensions.Contains(extension))
                {
                    throw new Exception("Invalid file type. Allowed: JPG, PNG, GIF, WEBP.");
                }

                // Save to Providers folder
                var uploadsFolder = Path.Combine(_environment.WebRootPath, "UploadedImages", "Providers");
                Directory.CreateDirectory(uploadsFolder);

                // ✅ FIX: Use userId only since model is not available here
                // Generate filename using userId and timestamp
                var uniqueFileName = $"provider_{userId}_{DateTime.Now:yyyyMMddHHmmss}{extension}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                // Save the file
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await imageFile.CopyToAsync(stream);
                }

                // Return the path
                return $"/UploadedImages/Providers/{uniqueFileName}";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error uploading profile image: {ex.Message}");
                throw;
            }
        }
        private async Task UpdateUserProfile(ProfileViewModel model, string userId, string profileImagePath)
        {
            var connectionString = _configuration.GetConnectionString("MySqlConnection");

            using (var connection = new MySqlConnection(connectionString))
            {
                await connection.OpenAsync();

                try
                {
                    Console.WriteLine($"========== UPDATING PROFILE ==========");
                    Console.WriteLine($"UserId: {userId}");
                    Console.WriteLine($"FirstName: {model.FirstName}");
                    Console.WriteLine($"LastName: {model.LastName}");
                    Console.WriteLine($"Phone: {model.Phone}");
                    Console.WriteLine($"WhatsApp: {model.WhatsApp}");
                    Console.WriteLine($"Address: {model.Address}");
                    Console.WriteLine($"ProfileImagePath: {(string.IsNullOrEmpty(profileImagePath) ? "NO CHANGE" : profileImagePath)}");

                    // Update ONLY the h_users table - NO service table update
                    string userQuery;

                    if (!string.IsNullOrEmpty(profileImagePath))
                    {
                        userQuery = @"
                    UPDATE h_users SET 
                        FirstName = @firstName,
                        LastName = @lastName,
                        Phone = @phone,
                        WhatsApp = @whatsApp,
                        Address = @address,
                        ProviderImage = @profileImage,
                        UpdatedAt = NOW()
                    WHERE Id = @userId";

                        Console.WriteLine("✅ Updating WITH new profile image");
                    }
                    else
                    {
                        userQuery = @"
                    UPDATE h_users SET 
                        FirstName = @firstName,
                        LastName = @lastName,
                        Phone = @phone,
                        WhatsApp = @whatsApp,
                        Address = @address,
                        UpdatedAt = NOW()
                    WHERE Id = @userId";

                        Console.WriteLine("ℹ️ Updating WITHOUT changing profile image");
                    }

                    using (var userCmd = new MySqlCommand(userQuery, connection))
                    {
                        userCmd.Parameters.AddWithValue("@userId", userId);
                        userCmd.Parameters.AddWithValue("@firstName", model.FirstName ?? "");
                        userCmd.Parameters.AddWithValue("@lastName", model.LastName ?? "");
                        userCmd.Parameters.AddWithValue("@phone", model.Phone ?? "");
                        userCmd.Parameters.AddWithValue("@whatsApp", model.WhatsApp ?? (object)DBNull.Value);
                        userCmd.Parameters.AddWithValue("@address", model.Address ?? (object)DBNull.Value);

                        if (!string.IsNullOrEmpty(profileImagePath))
                        {
                            userCmd.Parameters.AddWithValue("@profileImage", profileImagePath);
                            Console.WriteLine($"Parameter @profileImage = {profileImagePath}");
                        }

                        int userRowsAffected = await userCmd.ExecuteNonQueryAsync();
                        Console.WriteLine($"Rows affected in h_users: {userRowsAffected}");

                        if (userRowsAffected == 0)
                        {
                            throw new Exception("User profile not found.");
                        }
                    }

                    Console.WriteLine("✅ Profile updated successfully in h_users table");
                    Console.WriteLine($"========================================");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error updating profile: {ex.Message}");
                    Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                    throw;
                }
            }
        }
    }
}