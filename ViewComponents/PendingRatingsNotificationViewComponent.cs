using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using MySql.Data.MySqlClient;
using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.Blazor;
using NuGet.Protocol.Plugins;

namespace phpMVC.ViewComponents
{
    public class PendingRatingsNotificationViewComponent : ViewComponent
    {
        private readonly IConfiguration _configuration;

        public PendingRatingsNotificationViewComponent(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var customerId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(customerId))
                return Content("");

            var count = GetPendingRatingsCount(customerId);

            if (count > 0)
            {
                ViewBag.Count = count;
                return View("Default");
            }

            return Content("");
        }

        private int GetPendingRatingsCount(string customerId)
        {
            var connectionString = _configuration.GetConnectionString("MySqlConnection");

            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();
                string query = @"
                    SELECT COUNT(*) 
                    FROM bookings 
                    WHERE CustomerId = @customerId 
                    AND Status = 'completed' 
                    AND Rating IS NULL";

                using (var cmd = new MySqlCommand(query, connection))
                {
                    cmd.Parameters.AddWithValue("@customerId", customerId);
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }
    }
}