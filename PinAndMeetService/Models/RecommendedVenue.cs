using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace PinAndMeetService.Models {
    public class RecommendedVenue {
        // VenueUrl = 'https://foursquare.com/v/' + venueId

        [Key]
        public string venueId { get; set; }
        public decimal lng { get; set; }
        public decimal lat { get; set; }
        public string venueName { get; set; }
        public string street { get; set; }
        public string category { get; set; }
        public string facebookId { get; set; }
        public string venueImageUrl { get; set; }
    }
}