using SilverSet.Data;
using Microsoft.EntityFrameworkCore;
using SilverSet.Models.Entities;

namespace SilverSet.Services
{
    public class ForumSettingsService
    {
        private readonly SilverSetContext _context;

        public ForumSettingsService(SilverSetContext context)
        {
            _context = context;
        }

        public async Task<bool> IsForumActive()
        {
            var setting = await _context.GlobalSettings.FirstOrDefaultAsync(s => s.Key == "ForumStatus");
            return setting == null || setting.Value.ToLower() == "true";
        }

        public async Task<TimeSpan> GetPostRestrictionTime()
        {
            var setting = await _context.GlobalSettings.FirstOrDefaultAsync(s => s.Key == "ForumPostDelay");
            if (setting == null || setting.Value == "0") return TimeSpan.Zero;

            var val = setting.Value.ToLower();
            if (val.EndsWith("h"))
            {
                int.TryParse(val.Replace("h", ""), out int hours);
                return TimeSpan.FromHours(hours);
            }
            if (val.EndsWith("m"))
            {
                int.TryParse(val.Replace("m", ""), out int minutes);
                return TimeSpan.FromMinutes(minutes);
            }

            int.TryParse(val, out int defaultHours);
            return TimeSpan.FromHours(defaultHours);
        }

        public async Task<int> GetShadowPostLimit()
        {
            var setting = await _context.GlobalSettings.FirstOrDefaultAsync(s => s.Key == "ForumApprovalThreshold");
            if (setting == null) return 5;
            int.TryParse(setting.Value, out int limit);
            return limit;
        }

        public async Task UpdateSettingAsync(string key, string value)
        {
            var setting = await _context.GlobalSettings.FirstOrDefaultAsync(s => s.Key == key);
            if (setting != null)
            {
                setting.Value = value;
                await _context.SaveChangesAsync();
            }
        }

        public async Task<List<GlobalSetting>> GetAllForumSettingsAsync()
        {
            return await _context.GlobalSettings
                .Where(s => s.Key.StartsWith("Forum"))
                .ToListAsync();
        }
    }
}