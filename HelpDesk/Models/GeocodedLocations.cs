using System.ComponentModel.DataAnnotations;

namespace HelpdeskApp.Models
{
    public class GeocodedLocation
    {
        public int Id { get; set; }

        [Required]
        public string PctLucru { get; set; }

        [Required]
        public string Latitude { get; set; }

        [Required]
        public string Longitude { get; set; }
    }
}
