using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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

        public bool? Has_eFactura { get; set; } = false;
        public bool? Has_OPT { get; set; } = false;
        public bool? Has_CMS { get; set; } = false;
        public bool? Has_Loyalty { get; set; } = false;

        // Adjust the column names to match your database
        [Column("ins_time")]
        public DateTime? InsTime { get; set; }

        [Column("mod_time")]
        public DateTime? ModTime { get; set; }

        [Column("ins_user_id")]
        public int? InsUserId { get; set; }

        [Column("mod_user_id")]
        public int? ModUserId { get; set; }
    }

}
