using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace PinAndMeetService.Models {
    public class Block {
        [Key]
        public int BlockId { get; set; }
        public string Id { get; set; }
        public string BlockedUserId { get; set; }
        public DateTime Created { get; set; }
    }
}