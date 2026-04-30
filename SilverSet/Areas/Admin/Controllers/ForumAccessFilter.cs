using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SilverSet.Services;

namespace SilverSet.Filters;

public class ForumAccessFilter : IAsyncActionFilter
{
    private readonly ForumSettingsService _settingsService;

    public ForumAccessFilter(ForumSettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var isActive = await _settingsService.IsForumActive();

        if (!isActive)
        {
            var descriptor = context.ActionDescriptor as Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor;
            var actionName = descriptor?.ActionName;


            if (actionName != "Maintenance")
            {
                context.Result = new RedirectToActionResult("Maintenance", "Forum", new { area = "" });
                return;
            }
        }

        await next();
    }
}