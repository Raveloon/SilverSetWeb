namespace SilverSet.Areas.Admin.Models;

public class ForumSettingsViewModel
{
    public bool IsForumActive { get; set; }
    public string PostRestrictionTime { get; set; } = "24h";
    public int ShadowPostLimit { get; set; }
}