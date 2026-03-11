namespace SilverSet.Models.Entities
{
    public class FeaturedGame
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string ImagePath{ get; set; }
        public string Genre{ get; set; }
        public string Status { get; set; }
        public int DisplayOrder { get; set; }
        public bool IsVisible { get; set; }
    }
}
