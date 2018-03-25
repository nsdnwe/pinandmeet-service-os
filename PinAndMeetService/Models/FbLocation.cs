using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace PinAndMeetServiceR1.Models {
    public class FbLocation {
        [Key]
        public string id { get; set; }
        public int fbCityCircleId { get; set; }
        public string name { get; set; }
        public string city { get; set; }
        public string country { get; set; }
        public decimal latitude { get; set; }   // NOTE. See DB.OnModelCreating !!!
        public decimal longitude { get; set; }
        public string state { get; set; }
        public string street { get; set; }
        public string zip { get; set; }
        public string category { get; set; }
        public bool ownEvents { get; set; }
    }
}