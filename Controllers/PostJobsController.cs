using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;
using phpMVC.Models;
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System.Linq;

namespace phpMVC.Controllers
{
    public class PostJobsController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;

        public PostJobsController(IConfiguration configuration, IWebHostEnvironment environment)
        {
            _configuration = configuration;
            _environment = environment;
        }

        // CHECK IF USER IS LOGGED IN AND IS A PROVIDER
        private bool IsProviderLoggedIn()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("UserId")))
            {
                return false;
            }

            string userType = HttpContext.Session.GetString("UserType");
            return userType == "provider";
        }

        // GET: /PostJobs/Create
        [HttpGet]
        public IActionResult Create()
        {
            if (!IsProviderLoggedIn())
            {
                TempData["ErrorMessage"] = "You must be logged in as a service provider to post a service.";
                return RedirectToAction("Login", "Account", new { returnUrl = "/PostJobs/Create" });
            }

            return View(new PostJobViewModel());
        }

        // POST: /PostJobs/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PostJobViewModel model)
        {
            // CHECK AGAIN ON POST (security)
            if (!IsProviderLoggedIn())
            {
                TempData["ErrorMessage"] = "You must be logged in as a service provider to post a service.";
                return RedirectToAction("Login", "Account", new { returnUrl = "/PostJobs/Create" });
            }

            // 🔴 DEBUG: Log all form data
            Console.WriteLine("========== POST JOB SUBMISSION ==========");
            Console.WriteLine($"Request Form Keys: {string.Join(", ", Request.Form.Keys)}");
            Console.WriteLine($"Request Files Count: {Request.Form.Files.Count}");

            if (Request.Form.Files.Count > 0)
            {
                var file = Request.Form.Files[0];
                Console.WriteLine($"File Name: {file.FileName}");
                Console.WriteLine($"File Size: {file.Length} bytes");
                Console.WriteLine($"File Content Type: {file.ContentType}");
                Console.WriteLine($"Model.ServiceImage is null: {model.ServiceImage == null}");
            }
            else
            {
                Console.WriteLine("⚠️ No files received in request!");
            }

            // Check ModelState
            Console.WriteLine($"ModelState.IsValid: {ModelState.IsValid}");
            if (!ModelState.IsValid)
            {
                foreach (var state in ModelState)
                {
                    foreach (var error in state.Value.Errors)
                    {
                        Console.WriteLine($"Error - {state.Key}: {error.ErrorMessage}");
                    }
                }
            }

            try
            {
                // Get the current provider's ID from session
                string providerId = HttpContext.Session.GetString("UserId");

                Console.WriteLine($"Provider ID: {providerId}");
                Console.WriteLine($"Service Title: {model.ServiceTitle ?? "NULL"}");
                Console.WriteLine($"Service Type: {model.ServiceType ?? "NULL"}");
                Console.WriteLine($"Price: {model.Price}");
                Console.WriteLine($"Location: {model.Location ?? "NULL"}");
                Console.WriteLine($"Duration: {model.Duration ?? "NULL"}");
                Console.WriteLine($"Description Length: {model.JobDescription?.Length ?? 0}");
                Console.WriteLine($"Has Image: {model.ServiceImage != null}");

                // Handle image upload
                string imagePath = null;
                if (model.ServiceImage != null && model.ServiceImage.Length > 0)
                {
                    imagePath = await HandleImageUpload(model.ServiceImage);
                    Console.WriteLine($"✅ Image uploaded successfully: {imagePath}");
                }
                else
                {
                    Console.WriteLine("ℹ️ No image uploaded - using NULL");
                }

                // Save to database with provider ID
                await SaveServiceToDatabase(model, imagePath, providerId);

                Console.WriteLine("✅ Service posted successfully!");

                // SET SUCCESS MESSAGE BUT STAY ON SAME PAGE
                TempData["ShowSuccessMessage"] = true;
                TempData["SuccessMessage"] = "Service posted successfully!";

                // Return the same view with a fresh model
                return View(new PostJobViewModel());
            }
            catch (MySqlException sqlEx)
            {
                Console.WriteLine($"❌ MySQL ERROR: {sqlEx.Message}");
                Console.WriteLine($"Error Code: {sqlEx.Number}");
                Console.WriteLine($"SQL State: {sqlEx.SqlState}");

                TempData["ErrorMessage"] = $"Database error: {sqlEx.Message}";
                return View(model);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ GENERAL ERROR: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");

                TempData["ErrorMessage"] = $"Error posting service: {ex.Message}";
                return View(model);
            }
        }

        private async Task<string> HandleImageUpload(IFormFile imageFile)
        {
            if (imageFile == null || imageFile.Length == 0)
            {
                Console.WriteLine("⚠️ No image uploaded or file is empty");
                return null;
            }

            try
            {
                Console.WriteLine($"📸 Processing image: {imageFile.FileName}, Size: {imageFile.Length} bytes");

                // Validate file size (5MB)
                if (imageFile.Length > 5 * 1024 * 1024)
                {
                    throw new Exception($"File size exceeds 5MB limit. Current size: {imageFile.Length / 1024 / 1024}MB");
                }

                // Validate file type
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                var extension = Path.GetExtension(imageFile.FileName).ToLowerInvariant();

                if (string.IsNullOrEmpty(extension) || !allowedExtensions.Contains(extension))
                {
                    throw new Exception($"Invalid file type: {extension}. Allowed: JPG, PNG, GIF, WEBP.");
                }

                // Define the correct path: wwwroot/UploadedImages/services/
                var uploadsFolder = Path.Combine(_environment.WebRootPath, "UploadedImages", "services");
                Console.WriteLine($"📁 Upload folder absolute path: {uploadsFolder}");

                // Create directory if it doesn't exist
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                    Console.WriteLine($"✅ Created directory: {uploadsFolder}");
                }

                // Generate unique filename
                var uniqueFileName = $"{Guid.NewGuid():N}{extension}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                Console.WriteLine($"💾 Saving to: {filePath}");

                // Save the file
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await imageFile.CopyToAsync(stream);
                    Console.WriteLine($"✅ File written, size: {stream.Length} bytes");
                }

                // Verify file was saved
                if (System.IO.File.Exists(filePath))
                {
                    var fileInfo = new FileInfo(filePath);
                    Console.WriteLine($"✅ File verified: {fileInfo.Name}, Size: {fileInfo.Length} bytes");
                }
                else
                {
                    throw new Exception("File was not saved successfully");
                }

                // Return the URL path
                var relativePath = $"/UploadedImages/services/{uniqueFileName}";
                Console.WriteLine($"🔗 Relative URL path: {relativePath}");

                return relativePath;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error in HandleImageUpload: {ex.Message}");
                throw;
            }
        }

        private async Task SaveServiceToDatabase(PostJobViewModel model, string imagePath, string providerId)
        {
            var connectionString = _configuration.GetConnectionString("MySqlConnection");

            using (var connection = new MySqlConnection(connectionString))
            {
                await connection.OpenAsync();
                Console.WriteLine("✅ Database connection opened");

                // Insert the service - REMOVED ProviderImages column
                string query = @"
                    INSERT INTO service (
                        Name, 
                        Description, 
                        location, 
                        duration, 
                        availability,
                        rating, 
                        reviewcount, 
                        price, 
                        serviceImages, 
                        ProviderId, 
                        IsActive, 
                        created_at, 
                        updated_at
                    ) VALUES (
                        @Name, 
                        @Description, 
                        @location, 
                        @duration, 
                        @availability,
                        @rating, 
                        @reviewcount, 
                        @price, 
                        @serviceImages, 
                        @ProviderId, 
                        @IsActive, 
                        NOW(), 
                        NOW()
                    )";

                using (var cmd = new MySqlCommand(query, connection))
                {
                    cmd.Parameters.AddWithValue("@Name", model.ServiceTitle ?? "");
                    cmd.Parameters.AddWithValue("@Description", model.JobDescription ?? "");
                    cmd.Parameters.AddWithValue("@location", model.Location ?? "");
                    cmd.Parameters.AddWithValue("@duration", model.Duration ?? "");
                    cmd.Parameters.AddWithValue("@availability", model.Availability ?? "");
                    cmd.Parameters.AddWithValue("@rating", 0); // New service starts with 0 rating
                    cmd.Parameters.AddWithValue("@reviewcount", 0); // New service starts with 0 reviews
                    cmd.Parameters.AddWithValue("@price", model.Price);

                    // Handle image path
                    if (!string.IsNullOrEmpty(imagePath))
                    {
                        cmd.Parameters.AddWithValue("@serviceImages", imagePath);
                        Console.WriteLine($"✅ Setting serviceImages to: {imagePath}");
                    }
                    else
                    {
                        cmd.Parameters.AddWithValue("@serviceImages", DBNull.Value);
                        Console.WriteLine("ℹ️ Setting serviceImages to NULL");
                    }

                    cmd.Parameters.AddWithValue("@ProviderId", providerId);
                    cmd.Parameters.AddWithValue("@IsActive", 1);

                    Console.WriteLine("Executing SQL INSERT...");
                    int rowsAffected = await cmd.ExecuteNonQueryAsync();

                    if (rowsAffected == 0)
                    {
                        throw new Exception("Failed to insert service into database - 0 rows affected");
                    }

                    Console.WriteLine($"✅ Service inserted successfully. Rows affected: {rowsAffected}");
                }
            }
        }

        // GET: /PostJobs/Success
        public IActionResult Success()
        {
            if (!IsProviderLoggedIn())
            {
                return RedirectToAction("Login", "Account");
            }

            if (TempData["SuccessMessage"] == null)
            {
                return RedirectToAction("Create");
            }
            return View();
        }
    }
}