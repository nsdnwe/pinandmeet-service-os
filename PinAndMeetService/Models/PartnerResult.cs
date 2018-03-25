using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace PinAndMeetService.Models {
    public class PartnerResult {
        public string id { get; set; }          // FB user id
        public string name { get; set; }
        public string imageUrl { get; set; }
        public string state { get; set; }       // State of this user (and partner)
        public string error { get; set; } 
    }
}