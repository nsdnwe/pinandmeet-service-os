using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace PinAndMeetService.Models {
    public class FbCity {
        public int Id { get; set; }
        public string city { get; set; }
        public int cityTimeZoneBias { get; set; }
        public int distance { get; set; }
    }
}