using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace PinAndMeetService.Models {
    public class ChatMessageResult {
        public int id { get; set; }
        public string from { get; set; }
        public string message { get; set; }
    }
}