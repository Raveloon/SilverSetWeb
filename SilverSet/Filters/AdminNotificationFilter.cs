using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using SilverSet.Data;

namespace SilverSet.Filters;

public class AdminNotificationFilter : IAsyncActionFilter
{
    private readonly SilverSetContext _context;

    public AdminNotificationFilter(SilverSetContext context)
    {
        _context = context;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var controller = context.Controller as Controller;

        if (controller != null && context.RouteData.Values["area"]?.ToString() == "Admin")
        {
            controller.ViewBag.ActiveTicketCount = await _context.Tickets
                .CountAsync(t => t.IsOpen && !t.IsSpam);

            controller.ViewBag.SpamTicketCount = await _context.Tickets
                .CountAsync(t => t.IsSpam);
        }

        await next();
    }
}