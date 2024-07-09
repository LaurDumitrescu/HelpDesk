using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HelpdeskApp.Models
{
    public class FirmaNrTelefon
    {
        public Int64 Id { get; set; }

        [Required]
        public int ID_firma_punct_lucru { get; set; }

        [ForeignKey("ID_firma_punct_lucru")]
        public virtual FirmaPunctLucru FirmaPunctLucru { get; set; }

        [Required]
        public string NrTelefon { get; set; }
    }
}
