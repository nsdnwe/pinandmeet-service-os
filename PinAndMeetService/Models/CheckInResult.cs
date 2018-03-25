using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace PinAndMeetService.Models {
    public class CheckInResult {
        public string result { get; set; }
        public string justMetPartnerFirstName { get; set; }
        public string error { get; set; }
        public string errorCode { get; set; }
    }
}