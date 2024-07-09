using System;
using System.ComponentModel.DataAnnotations;

namespace HelpdeskApp.Models
{
    public class EntryModel
    {
        public long Id { get; set; }

        [Required]
        public string Firma { get; set; }

        [Required]
        public string PctLucru { get; set; }

        [Required]
        public string NrTelefon { get; set; }

        [Required]
        [DataType(DataType.Date)]
        public DateTime Data { get; set; }

        public string OraApel { get; set; }

        public string DurataApel { get; set; }

        public string Problema { get; set; }

        public string Rezolvare { get; set; }
    }
}
