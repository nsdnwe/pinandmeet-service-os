using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace PinAndMeetService.Models {
    public class ViolationReport {
        [Key]
        public int ViolationReportId { get; set; }
        public string Id { get; set; }
        public string TargetUserId { get; set; }
        public string Description { get; set; }
        public DateTime Created { get; set; }
    }
}