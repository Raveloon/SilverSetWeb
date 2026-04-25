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

            var unhandledSpamTopicIds = _context.ForumSpamLogs
                .Where(l => !l.IsHandled && l.TopicId.HasValue && l.PostId == null)
                .Select(l => l.TopicId!.Value);

            var unhandledSpamPostIds = _context.ForumSpamLogs
                .Where(l => !l.IsHandled && l.PostId.HasValue)
                .Select(l => l.PostId!.Value);

            int pendingTopics = await _context.ForumTopics
                .CountAsync(t => !t.IsApproved && !unhandledSpamTopicIds.Contains(t.Id));

            int pendingPosts = await _context.ForumPosts
                .CountAsync(p => !p.IsApproved && !unhandledSpamPostIds.Contains(p.Id));

            controller.ViewBag.ForumApprovalCount = pendingTopics + pendingPosts;

            controller.ViewBag.ForumSpamCount = await _context.ForumSpamLogs
                .CountAsync(l => !l.IsHandled);
        }

        await next();
    }
}