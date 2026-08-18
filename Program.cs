using HUIOTClinicMS.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HUIOTClinicMS
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            var connectionString = builder.Configuration.GetConnectionString("ClinicConnection") ?? throw new InvalidOperationException("Connection string 'ClinicConnection' not found.");
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(connectionString));
            builder.Services.AddDatabaseDeveloperPageExceptionFilter();

            builder.Services.AddDefaultIdentity<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = false)
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>();

            builder.Services.AddScoped<HUIOTClinicMS.Services.INotificationService, HUIOTClinicMS.Services.NotificationService>();
            builder.Services.AddTransient<Microsoft.AspNetCore.Identity.UI.Services.IEmailSender, HUIOTClinicMS.Services.EmailSender>();

            builder.Services.AddControllersWithViews();

            var app = builder.Build();

            using (var scope = app.Services.CreateScope())
            {
                await SeedData.InitializeAsync(scope.ServiceProvider);

            }

            if (app.Environment.IsDevelopment())
            {
                app.UseMigrationsEndPoint();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapRazorPages();

            app.MapGet("/", (HttpContext context) =>
            {
                if (context.User.Identity?.IsAuthenticated != true)
                {
                    return Results.Redirect("/Identity/Account/Login");
                }

                if (context.User.IsInRole("HealthOfficer")) return Results.Redirect("/Home/Index");
                if (context.User.IsInRole("Nurse")) return Results.Redirect("/Nurse/Index");
                if (context.User.IsInRole("LabTechnician")) return Results.Redirect("/LabTechnician/Index");
                if (context.User.IsInRole("Pharmacist")) return Results.Redirect("/Pharmacist/Index");
                if (context.User.IsInRole("Student")) return Results.Redirect("/Student/Dashboard");

                // Authenticated but no recognized role — send back to Login rather than 403 loop
                return Results.Redirect("/Identity/Account/Login");
            });

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            await app.RunAsync();
        }
    }
}