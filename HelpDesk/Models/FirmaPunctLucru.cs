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

        public bool? Has_eFactura { get; set; } = false; // BIT default 0

        public bool? Has_OPT { get; set; } = false; // BIT default 0

        public bool? Has_CMS { get; set; } = false; // BIT default 0

        public bool? Has_Loyalty { get; set; } = false; // BIT default 0
    }
}
