using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace PinAndMeetService.Models {
    public class LogEvent {
        [Key]
        public int LogEventId { get; set; }
        public string Id { get; set; }          // FB id
        public string SessionId { get; set; }
        public string Stage { get; set; }
        public string Module { get; set; }
        public string Method { get; set; }
        public string Type { get; set; }
        public int TypeId { get; set; }     // 0 = Event, 10=Warning, 20=Error, 30=Unhandled error
        public string EventName { get; set; }
        public string Parameters { get; set; }
        public DateTime Created { get; set; }
        public DateTime LocalTimeStamp { get; set; }
    }
}