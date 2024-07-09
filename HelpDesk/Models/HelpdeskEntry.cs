using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HelpdeskApp.Models
{
    public class HelpdeskEntry
    {
        public Int64 Id { get; set; }

        [Required]
        public Int64 ID_nr_telefon { get; set; }

        [ForeignKey("ID_nr_telefon")]
        public virtual FirmaNrTelefon FirmaNrTelefon { get; set; }

        [DataType(DataType.Date)]
        public DateTime Data { get; set; }

        public string Zi { get; set; }

        [DataType(DataType.Time)]
        public TimeSpan OraApel { get; set; }

        public string? DurataApel { get; set; }

        public string? Problema { get; set; }

        public string? Rezolvare { get; set; }

        public DateTime InsTime { get; set; }
        public DateTime? ModTime { get; set; }
        public int? InsUserId { get; set; }
        public int? ModUserId { get; set; }
    }
}
