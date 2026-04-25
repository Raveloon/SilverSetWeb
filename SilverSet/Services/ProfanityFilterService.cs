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
    private const string RegexCacheKey = "BadWordsRegex";

    public ProfanityFilterService(SilverSetContext context, IMemoryCache cache, IWebHostEnvironment env)
    {
        _context = context;
        _cache = cache;
        _env = env;
    }

    private async Task<(List<string> words, Regex? pattern)> GetFilterDataAsync()
    {
        if (!_cache.TryGetValue(CacheKey, out List<string>? blacklist) || !_cache.TryGetValue(RegexCacheKey, out Regex? regex))
        {
            var dbWords = await _context.BadWords.Select(b => b.Word.Trim().ToLowerInvariant()).ToListAsync();
            var fileWords = new List<string>();

            var paths = new[]
            {
                Path.Combine(_env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot"), "Data", "blacklist.txt"),
                Path.Combine(_env.ContentRootPath, "Data", "blacklist.txt"),
                Path.Combine(AppContext.BaseDirectory, "Data", "blacklist.txt")
            };

            foreach (var path in paths)
            {
                if (File.Exists(path))
                {
                    var lines = await File.ReadAllLinesAsync(path);
                    fileWords = lines.Where(l => !string.IsNullOrWhiteSpace(l)).Select(l => l.Trim().ToLowerInvariant()).ToList();
                    break;
                }
            }

            blacklist = dbWords.Concat(fileWords).Distinct().OrderByDescending(w => w.Length).ToList();
            regex = null;

            if (blacklist.Any())
            {
                string joined = string.Join("|", blacklist.Select(Regex.Escape));
                string pattern = $@"(?<![\wçğıöşüİĞÜŞÖÇ])({joined})(?![\wçğıöşüİĞÜŞÖÇ])";
                regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
            }

            _cache.Set(CacheKey, blacklist, TimeSpan.FromHours(1));
            _cache.Set(RegexCacheKey, regex, TimeSpan.FromHours(1));
        }

        return (blacklist ?? new List<string>(), regex);
    }

    public void ClearCache()
    {
        _cache.Remove(CacheKey);
        _cache.Remove(RegexCacheKey);
    }

    public async Task<string> FilterText(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return input;
        var (_, regex) = await GetFilterDataAsync();
        if (regex == null) return input;

        return regex.Replace(input, m => new string('*', m.Length));
    }

    public async Task<(bool IsSpam, string CaughtWord, bool IsInTitle)> DetectSpam(string title, string content)
    {
        var (_, regex) = await GetFilterDataAsync();
        if (regex == null) return (false, string.Empty, false);

        if (!string.IsNullOrWhiteSpace(title))
        {
            var match = regex.Match(title);
            if (match.Success) return (true, match.Value, true);
        }

        if (!string.IsNullOrWhiteSpace(content))
        {
            var match = regex.Match(content);
            if (match.Success) return (true, match.Value, false);
        }

        return (false, string.Empty, false);
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