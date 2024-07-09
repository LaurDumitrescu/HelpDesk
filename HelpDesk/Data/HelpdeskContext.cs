using Microsoft.EntityFrameworkCore;
using HelpdeskApp.Models;

namespace HelpdeskApp.Data
{
    public class HelpdeskContext : DbContext
    {
        public HelpdeskContext(DbContextOptions<HelpdeskContext> options) : base(options)
        {
        }

        public DbSet<HelpdeskEntry> HelpdeskEntries { get; set; }
        public DbSet<FirmaPunctLucru> FirmaPunctLucruEntries { get; set; }
        public DbSet<FirmaNrTelefon> FirmaNrTelefonEntries { get; set; }
        public DbSet<User> Users { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<HelpdeskEntry>(entity =>
            {
                entity.ToTable("entry", "dbo");

                entity.Property(e => e.ID_nr_telefon).HasColumnName("ID_nr_telefon");
                entity.Property(e => e.Data).HasColumnName("data");
                entity.Property(e => e.Zi).HasColumnName("zi");
                entity.Property(e => e.OraApel).HasColumnName("ora_apel");
                entity.Property(e => e.DurataApel).HasColumnName("durata_apel");
                entity.Property(e => e.Problema).HasColumnName("problema");
                entity.Property(e => e.Rezolvare).HasColumnName("rezolvare");

                entity.Property(e => e.InsTime).HasColumnName("ins_time").IsRequired();
                entity.Property(e => e.ModTime).HasColumnName("mod_time");
                entity.Property(e => e.InsUserId).HasColumnName("ins_user_id");
                entity.Property(e => e.ModUserId).HasColumnName("mod_user_id");

                entity.HasOne(e => e.FirmaNrTelefon)
                      .WithMany()
                      .HasForeignKey(e => e.ID_nr_telefon);
            });

            modelBuilder.Entity<FirmaPunctLucru>(entity =>
            {
                entity.ToTable("firma_punct_lucru", "dbo");

                entity.Property(e => e.Firma).HasColumnName("firma");
                entity.Property(e => e.PctLucru).HasColumnName("pct_lucru");
                entity.Property(e => e.Priority).HasColumnName("priority"); // New property
            });

            modelBuilder.Entity<FirmaNrTelefon>(entity =>
            {
                entity.ToTable("firma_nr_telefon", "dbo");

                entity.Property(e => e.ID_firma_punct_lucru).HasColumnName("ID_firma_punct_lucru");
                entity.Property(e => e.NrTelefon).HasColumnName("nr_telefon");

                entity.HasOne(e => e.FirmaPunctLucru)
                      .WithMany()
                      .HasForeignKey(e => e.ID_firma_punct_lucru);
            });

            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("users", "dbo");

                entity.Property(e => e.Nume).HasColumnName("nume").IsRequired();
                entity.Property(e => e.Parola).HasColumnName("parola").IsRequired();
                entity.Property(e => e.Name).HasColumnName("name").IsRequired();
                entity.Property(e => e.LastLoginTimestamp).HasColumnName("last_login_timestamp");
                entity.Property(e => e.LastFailedLogin).HasColumnName("last_failed_login");
                entity.Property(e => e.FailedLoginsCount).HasColumnName("failed_logins_count");
                entity.Property(e => e.InsTime).HasColumnName("ins_time").IsRequired();
                entity.Property(e => e.ModTime).HasColumnName("mod_time");
                entity.Property(e => e.InsUserId).HasColumnName("ins_user_id");
                entity.Property(e => e.ModUserId).HasColumnName("mod_user_id");
                entity.Property(e => e.IsDeleted).HasColumnName("is_deleted").IsRequired();
            });
        }
    }
}
