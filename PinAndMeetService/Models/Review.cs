using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace PinAndMeetService.Models {
    public class Review {
        [Key]
        public int ReviewId { get; set; }
        public string Id { get; set; } // FB user id
        public int Stars { get; set; }
        public string Evaluation { get; set; }
        public string FromId { get; set; } // FB user id
        public DateTime Created { get; set; }
        public string CreatedDate { get; set; }
        public string FromName { get; set; }    // Not saved
        public string FromImageUrl { get; set; } // Not saved
    }
}