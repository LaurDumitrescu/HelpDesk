using System.ComponentModel.DataAnnotations;

namespace HelpdeskApp.Models
{
    public class FirmaPunctLucru
    {
        public int Id { get; set; }

        [Required]
        public string Firma { get; set; }

        [Required]
        public string PctLucru { get; set; }

        public int? Priority { get; set; } 
    }
}
