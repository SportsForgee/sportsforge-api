namespace Api.Models
{
    public class ClubSquadPlayer
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Position { get; set; } = string.Empty;

        public int Age { get; set; }

        public int Form { get; set; }

        public string Value { get; set; } = string.Empty;

        public string Availability { get; set; } = string.Empty;
    }
}