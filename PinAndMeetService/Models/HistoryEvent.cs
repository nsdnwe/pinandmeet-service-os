using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace PinAndMeetService.Models {
    public class HistoryEvent {
        [Key]
        public int HistoryEventId { get; set; }          
        public string Id { get; set; }          // FB user id
        public string Description { get; set; }
        public string PartnerId { get; set; }
        public string PartnerName { get; set; }
        public string PartnerImageUrl { get; set; }          
        public string VenueId { get; set; }
        public string VenueName { get; set; }
        public string VenueImageUrl { get; set; }
        public DateTime LocalTimeStamp { get; set; }
        public DateTime Created { get; set; }
    }
}