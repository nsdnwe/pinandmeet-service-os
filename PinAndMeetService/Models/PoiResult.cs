using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace PinAndMeetService.Models {

    // This used when returning a list of other check-ins for the map
    public class PoiResult {
        // VenueUrl = 'https://foursquare.com/v/' + venueId

        public string venueId { get; set; }
        public decimal lng { get; set; }
        public decimal lat { get; set; }
        public string venueName { get; set; }
        public string category { get; set; }
        public string venueImageUrl { get; set; }
        public string state { get; set; } // CHECKED_IN or MATCH
        public bool myVenue { get; set; } // True if user checked in here

        // FB event related
        public string street { get; set; }
        public string city { get; set; }
        public string eventId { get; set; }
        public string eventName { get; set; }
        public string eventDescription { get; set; }
        public DateTime eventStarttimeUtc { get; set; }
        public string eventStarttimeLocal { get; set; }

    }
}