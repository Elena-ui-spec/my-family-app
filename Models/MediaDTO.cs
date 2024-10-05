namespace FamilyApp.API.Models
{
    public class MediaDTO
    {
        public string Id { get; set; }
        public string Description { get; set; }
        public List<string> Persons { get; set; }
        public string FilePath { get; set; }
        public string FileType { get; set; }
        public string FileUrl { get; set; }
        public string Story { get; set; } 
    }
}
