using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;


// -------------------------------------------------------------
// LoggingLevel
// -------------------------------------------------------------
// 0 = No logging (except errors)
// 10 = Log all but not map related. Send immideately
// 20 = Log all but not map related. Send as a batch
// 110 = Log all. Send immideately
// 120 = Log all. Send as a batch

namespace PinAndMeetService.Models {
    public class User {
        [Key]
        public int UserId { get; set; }
        public string SessionId { get; set; }
        public string State { get; set; } // // "" = Idle on map, CHECKED_IN, MATCHING = Match (suprice) but not found each other, MATCHED = Found each other, PAST_MATCH = Giving review
        public bool Deleted { get; set; }
        public bool TestUser { get; set; }
        public DateTime Created { get; set; }
        public int LoggingLevel { get; set; } // 0
        public int TimeZoneBias { get; set; } // Minutes from UTC
        public bool LogEvents { get; set; } // Not in use

        // Device info
        public string Platform { get; set; }
        public string Model { get; set; }
        public string Version { get; set; }
        public string UuId { get; set; }

        // Push message
        public string GcmRegistrationId { get; set; }
        public string ApmsRegistrationId { get; set; }

        // FB user data
        public string Id { get; set; }          // FB user id
        public string FbAccessToken { get; set; }
        public string Birthday { get; set; }
        public string First_name { get; set; }
        public string Gender { get; set; }
        public string Last_name { get; set; }
        public string Link { get; set; }        // FB user home page
        public string Locale { get; set; }
        public string Name { get; set; }
        public int Timezone { get; set; }
        public string Updated_time { get; set; }
        public bool Verified { get; set; }
        public string ImageUrl { get; set; }
        public bool NonFbImage { get; set; }

        // PnM accont data
        public bool IsPnmAccount { get; set; }
        public string PasswordHash { get; set; }
        public string Email { get; set; }
        public string OriginalEmail { get; set; }
        public string EmailVerificationCode { get; set; }
        public bool EmailVerified { get; set; }

        // Foursquar data
        public string VenueId { get; set; }
        public string VenueName { get; set; }
        public string VenueCity { get; set; }
        public decimal Lng { get; set; } // NOTE. See DB.OnModelCreating !!!
        public decimal Lat { get; set; }
        public string Category { get; set; }
        public string VenueImageUrl { get; set; }
        public DateTime CheckinDatetime { get; set; } // UTC
        public bool VenueOutOfRange { get; set; } // If over 200m from the venue but not clicked found/notfound or given feedback

        // POI data
        public bool NewPoisExist { get; set; }  // Inside map view area
        public decimal SwLat { get; set; }       // Map view are
        public decimal SwLng { get; set; }
        public decimal NeLat { get; set; }       // Map view are
        public decimal NeLng { get; set; }

        // Matching info
        public string PartnerId { get; set; }
        public bool PartnerContacted { get; set; } // Physicly found the partner

        // Banning
        public bool Banned { get; set; } 
        public string BannedReason { get; set; }

        // Checkin alerts
        public DateTime GeolocationUpdatedDatetime { get; set; } // UTC
        public bool SendCheckinAlerts { get; set; }  // Set as false when sign out
        public int CheckinAlertCount { get; set; }  // Not too many sent per time window

    }
}