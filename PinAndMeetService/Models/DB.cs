using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.ModelConfiguration.Conventions;
using System.Linq;
using System.Web;

namespace PinAndMeetService.Models {
    public class DB : DbContext {
        public DbSet<User> Users { get; set; }
        public DbSet<LogEvent> LogEvents { get; set; }
        public DbSet<Review> Reviews { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<HistoryEvent> HistoryEvents { get; set; }
        public DbSet<Contact> Contacts { get; set; }
        public DbSet<ViolationReport> ViolationReports { get; set; }
        public DbSet<Block> Blocks { get; set; }
        public DbSet<RecommendedVenue> RecommendedVenues { get; set; }
        public DbSet<FbEvent> FbEvents { get; set; }
        public DbSet<FbCity> FbCities { get; set; }
        public DbSet<FbCityCircle> FbCityCircles { get; set; }

        public DB(): base("DB"){} // Azure model-first requirement

        protected override void OnModelCreating(DbModelBuilder modelBuilder) {
            modelBuilder.Conventions.Remove<DecimalPropertyConvention>();
            modelBuilder.Conventions.Add(new DecimalPropertyConvention(20, 17));
        }
    }
}