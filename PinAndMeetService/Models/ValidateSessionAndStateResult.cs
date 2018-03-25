using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace PinAndMeetService.Models {
    public class ValidateSessionAndStateResult {
        public string id { get; set; }
        public string sessionId { get; set; }

        public string first_name { get; set; }
        public string last_name { get; set; }
        public string email { get; set; }
        public string birthday { get; set; }
        
        public string imageUrl { get; set; }
        public string state { get; set; }
        public string error { get; set; }
        public string errorCode { get; set; }

        public int loggingLevel { get; set; }
    }
}