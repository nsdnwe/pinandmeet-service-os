using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace PinAndMeetService.Models {
    public class FbEvent {
        [Key]
        public string eventId { get; set; }
        public int fbCityCircleId { get; set; }
        public string cityName { get; set; }
        public string fbLocationId { get; set; }
        public string eventName { get; set; }
        public string eventDescription { get; set; }
        public DateTime eventStarttimeUtc { get; set; }
        public string eventStarttimeLocal { get; set; }
        public bool Deleted { get; set; }

        // Location
        public string locationName { get; set; }
        public string locationCity { get; set; }
        public decimal latitude { get; set; }   // NOTE. See DB.OnModelCreating !!!
        public decimal longitude { get; set; }
        public string locationStreet { get; set; }
        public string locationCategory { get; set; }
    }
}