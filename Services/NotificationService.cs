using HUIOTClinicMS.Data;
using HUIOTClinicMS.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

namespace HUIOTClinicMS.Services
{
    public interface INotificationService
    {
        Task CreateNotificationAsync(string userId, string title, string message, string type, int? relatedId = null);
        Task CreateDirectNotificationAsync(string userId, string title, string message, string type, int? relatedId = null);
        Task CreateNotificationForUsersAsync(IEnumerable<string> userIds, string title, string message, string type, int? relatedId = null);
        Task<List<Notification>> GetUnreadNotificationsAsync(string userId);
        Task MarkAsReadAsync(int notificationId);
        Task MarkAllAsReadAsync(string userId);
    }

    public class NotificationService : INotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public NotificationService(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task CreateNotificationAsync(string userId, string title, string message, string type, int? relatedId = null)
        {
            await CreateNotificationForUsersAsync(new[] { userId }, title, message, type, relatedId);
        }

        public async Task CreateDirectNotificationAsync(string userId, string title, string message, string type, int? relatedId = null)
        {
            var notification = new Notification
            {
                UserID = userId,
                Title = title,
                Message = message,
                NotificationType = type,
                RelatedRecordID = relatedId,
                CreatedAt = DateTime.UtcNow
            };
            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();
        }

        public async Task CreateNotificationForUsersAsync(IEnumerable<string> userIds, string title, string message, string type, int? relatedId = null)
        {
            var usersToNotify = new HashSet<string>(userIds);

            // Also notify all Health Officers
            var healthOfficers = await _userManager.GetUsersInRoleAsync("HealthOfficer");
            foreach (var ho in healthOfficers)
            {
                usersToNotify.Add(ho.Id);
            }

            foreach (var uid in usersToNotify)
            {
                var notification = new Notification
                {
                    UserID = uid,
                    Title = title,
                    Message = message,
                    NotificationType = type,
                    RelatedRecordID = relatedId
                };
                _context.Notifications.Add(notification);
            }

            await _context.SaveChangesAsync();
        }

        public async Task<List<Notification>> GetUnreadNotificationsAsync(string userId)
        {
            return await _context.Notifications
                .Where(n => n.UserID == userId && !n.IsRead)
                .OrderByDescending(n => n.CreatedAt)
                .Take(10) // Limit to 10 for bell dropdown
                .ToListAsync();
        }

        public async Task MarkAsReadAsync(int notificationId)
        {
            var notification = await _context.Notifications.FindAsync(notificationId);
            if (notification != null)
            {
                notification.IsRead = true;
                await _context.SaveChangesAsync();
            }
        }

        public async Task MarkAllAsReadAsync(string userId)
        {
            var unread = await _context.Notifications
                .Where(n => n.UserID == userId && !n.IsRead)
                .ToListAsync();

            foreach (var n in unread)
            {
                n.IsRead = true;
            }

            if (unread.Any())
            {
                await _context.SaveChangesAsync();
            }
        }
    }
}
