using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
using SilverSet.Data;
using SilverSet.Models.Entities.Forum;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;

namespace SilverSet.Services;

public class ProfanityFilterService
{
    private readonly SilverSetContext _context;
    private readonly IMemoryCache _cache;
    private readonly IWebHostEnvironment _env;
    private const string CacheKey = "UnifiedBadWordsList";

    public ProfanityFilterService(SilverSetContext context, IMemoryCache cache, IWebHostEnvironment env)
    {
        _context = context;
        _cache = cache;
        _env = env;
    }

    private async Task<List<string>> GetBlacklistAsync()
    {
        if (!_cache.TryGetValue(CacheKey, out List<string>? blacklist))
        {
            var dbWords = await _context.BadWords
                .Select(b => b.Word.ToLowerInvariant().Trim())
                .ToListAsync();

            var fileWords = new List<string>();
            string filePath = Path.Combine(_env.ContentRootPath, "Data", "blacklist.txt");

            if (File.Exists(filePath))
            {
                var lines = await File.ReadAllLinesAsync(filePath);
                fileWords = lines
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .Select(l => l.ToLowerInvariant().Trim())
                    .ToList();
            }

            blacklist = dbWords.Union(fileWords).Distinct().ToList();
            _cache.Set(CacheKey, blacklist, TimeSpan.FromHours(24));
        }
        return blacklist ?? new List<string>();
    }

    public void ClearCache()
    {
        _cache.Remove(CacheKey);
    }

    public async Task<string> FilterText(string input)
    {
        return input;
    }

    public async Task<(bool IsSpam, string Reason)> DetectSpam(string title, string content)
    {
        var blacklist = await GetBlacklistAsync();
        string fullText = (title + " " + content).ToLowerInvariant();

        foreach (var badWord in blacklist)
        {
            if (fullText.Contains(badWord))
            {
                return (true, badWord);
            }
        }

        return (false, string.Empty);
    }

    public async Task<int?> GetActiveSpamIdForUser(int userId, int? topicId)
    {
        return await _context.ForumSpamLogs
            .Where(l => l.UserId == userId && l.TopicId == topicId && !l.IsHandled && l.RelatedSpamId == null)
            .OrderByDescending(l => l.CreatedAt)
            .Select(l => (int?)l.Id)
            .FirstOrDefaultAsync();
    }

    public async Task LogSpam(int userId, string type, string content, string word, int? topicId, int? postId, int? relatedId = null)
    {
        var log = new ForumSpamLog
        {
            UserId = userId,
            Type = type,
            Content = content,
            CaughtWord = word,
            TopicId = topicId,
            PostId = postId,
            CreatedAt = DateTime.UtcNow,
            IsHandled = false,
            RelatedSpamId = relatedId
        };

        _context.ForumSpamLogs.Add(log);
        await _context.SaveChangesAsync();
    }
}