using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using HUIOTClinicMS.Data;
using HUIOTClinicMS.Services;

namespace HUIOTClinicMS.ViewComponents
{
    public class NotificationBellViewComponent : ViewComponent
    {
        private readonly INotificationService _notificationService;
        private readonly UserManager<ApplicationUser> _userManager;

        public NotificationBellViewComponent(INotificationService notificationService, UserManager<ApplicationUser> userManager)
        {
            _notificationService = notificationService;
            _userManager = userManager;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var user = await _userManager.GetUserAsync(HttpContext.User);
            if (user == null)
            {
                return View(new List<Models.Notification>());
            }

            var notifications = await _notificationService.GetUnreadNotificationsAsync(user.Id);
            return View(notifications);
        }
    }
}
