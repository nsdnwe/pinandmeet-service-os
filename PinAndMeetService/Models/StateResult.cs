using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace PinAndMeetService.Models {
    public class StateResult {
        public string state { get; set; }
        public string error { get; set; }
        public string errorCode { get; set; }

        public int loggingLevel { get; set; }
    }
}