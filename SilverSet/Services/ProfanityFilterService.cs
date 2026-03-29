using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
using SilverSet.Data;
using SilverSet.Models.Entities;

namespace SilverSet.Services;

public class ProfanityFilterService
{
    private readonly SilverSetContext _context;
    private readonly IMemoryCache _cache;
    private const string CacheKey = "BadWordsList";

    public ProfanityFilterService(SilverSetContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    private async Task<HashSet<string>> GetBlacklistAsync()
    {
        if (!_cache.TryGetValue(CacheKey, out HashSet<string>? blacklist))
        {
            var words = await _context.BadWords
                .Select(b => b.Word.ToLower().Trim())
                .ToListAsync();

            blacklist = new HashSet<string>(words, StringComparer.OrdinalIgnoreCase);
            _cache.Set(CacheKey, blacklist, TimeSpan.FromHours(24));
        }
        return blacklist ?? new HashSet<string>();
    }

    public void ClearCache()
    {
        _cache.Remove(CacheKey);
    }

    public async Task<string> FilterText(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return input;

        var blacklist = await GetBlacklistAsync();
        var words = input.Split(' ');

        for (int i = 0; i < words.Length; i++)
        {
            var cleanWord = words[i].Trim('.', ',', '!', '?', '(', ')', '\n', '\r');
            if (blacklist.Contains(cleanWord))
            {
                words[i] = words[i][0] + new string('*', words[i].Length - 1);
            }
        }

        return string.Join(" ", words);
    }

    public async Task<(bool IsSpam, string Reason)> DetectSpam(string title, string content)
    {
        var blacklist = await GetBlacklistAsync();

        var titleCheck = CheckWords(title, blacklist);
        if (titleCheck.IsFound) return (true, $"Başlık: {titleCheck.Word}");

        var contentCheck = CheckWords(content, blacklist);
        if (contentCheck.IsFound) return (true, $"İçerik: {contentCheck.Word}");

        return (false, string.Empty);
    }

    private (bool IsFound, string Word) CheckWords(string input, HashSet<string> blacklist)
    {
        if (string.IsNullOrWhiteSpace(input)) return (false, string.Empty);

        var words = input.Split(' ');
        foreach (var word in words)
        {
            var cleanWord = word.Trim('.', ',', '!', '?', '(', ')', '\n', '\r');
            if (blacklist.Contains(cleanWord)) return (true, cleanWord);
        }
        return (false, string.Empty);
    }
}