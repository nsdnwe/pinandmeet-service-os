using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace PinAndMeetService.Models {
    public class Message {
        [Key]
        public int MessageId { get; set; }
        public string FromId { get; set; }
        public string FromSessionId { get; set; }
        public string ToId { get; set; }
        public string ToSessionId { get; set; }
        public string Text { get; set; }
        public DateTime Created { get; set; }
        public int Type { get; set; } // 0=Matching, 1=Other...
        public bool Delivered { get; set; }
    }
}