using Newtonsoft.Json;
using PinAndMeetService.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

// https://developers.google.com/maps/documentation/timezone/intro#Responses

namespace PinAndMeetService.Helpers {
    public static class PoiHelpers {

        // Update also in UserHelpers.cs
        const int MAX_CHECK_IN_TIME = 120; // 2h
        const int MAX_MATCHING_TIME = 6 * 60; // 6h
        const bool ENABLE_12H_RULE = false; // false is just for testing

        public static IEnumerable<PoiResult> GetPois(dynamic userData) {
            string id = userData.id;
            string sessionId = userData.sessionId;
            int loggingLevel = userData.loggingLevel;
            if (loggingLevel >= 110) GeneralHelpers.AddLogEvent(id, sessionId, "PoiHelpers", "GetPois", "Start", JsonConvert.SerializeObject(userData), loggingLevel);

            // ERROR: userData.swLng cuts last 3 digits off (why?) -> Hack
            string dynamicString = Convert.ToString(userData);

            decimal swLat = GeneralHelpers.GetDecimalValueFromDynamic(dynamicString, "swLat");
            decimal swLng = GeneralHelpers.GetDecimalValueFromDynamic(dynamicString, "swLng");
            decimal neLat = GeneralHelpers.GetDecimalValueFromDynamic(dynamicString, "neLat");
            decimal neLng = GeneralHelpers.GetDecimalValueFromDynamic(dynamicString, "neLng");

            var poiResult = new List<PoiResult>();
            using (DB db = new DB()) {
                User thisUser = db.Users.SingleOrDefault(z => z.Id == id && z.SessionId == sessionId);
                if (thisUser == null) {
                    poiResult.Add(new PoiResult() { venueId = "ERROR1" });
                    return poiResult;
                }
                thisUser.NewPoisExist = false;

                // Set map area
                thisUser.SwLat = swLat;
                thisUser.SwLng = swLng;
                thisUser.NeLat = neLat;
                thisUser.NeLng = neLng;

                // Checkin alert related
                thisUser.GeolocationUpdatedDatetime = DateTime.UtcNow;
                thisUser.CheckinAlertCount = 0;

                db.SaveChanges();

                // Get Recommended venues
                string sql = @"
                    SELECT * FROM RecommendedVenues
                    WHERE Enabled = 1
                ";
                sql += string.Format(" AND Lat < {0} AND Lat > {1} AND Lng > {2} AND Lng < {3}", neLat.ToString().Replace(',', '.'), swLat.ToString().Replace(',', '.'), swLng.ToString().Replace(',', '.'), neLng.ToString().Replace(',', '.'));
                var recommendedVenues = db.RecommendedVenues.SqlQuery(sql).ToList();

                //// Get FB events
                sql = @"
                    SELECT * FROM FbEvents
                    WHERE 1=1
                ";

                sql += string.Format(" AND latitude < {0} AND latitude > {1} AND longitude > {2} AND longitude < {3}", neLat.ToString().Replace(',', '.'), swLat.ToString().Replace(',', '.'), swLng.ToString().Replace(',', '.'), neLng.ToString().Replace(',', '.'));
                var fbEvents = db.FbEvents.SqlQuery(sql).ToList();

                // Get Checked in users
                sql = @"
                    SELECT * FROM Users 
                    WHERE Deleted = 0 
                    AND ([State] = 'CHECKED_IN' OR [State] = 'MATCHING' OR [State] = 'REVIEW') 
                    AND VenueOutOfRange = 0
                    AND PartnerId <> '{0}'
                    AND [Id] NOT IN (SELECT [Id] FROM Blocks WHERE [BlockedUserId] = '{0}') 
                    AND [Id] NOT IN (SELECT [BlockedUserId] FROM Blocks WHERE [Id] = '{0}') 
                    AND [Id] NOT IN (SELECT [PartnerId] FROM Contacts WHERE [Id] = '{0}' AND Created > '{1}') 
                    AND CheckinDatetime > '{2}'";


                // 12h limit. Not show who you just met 
                //  2h limit: Not show who has been checked in more than 2h 

                string back12h = DateTime.UtcNow.AddHours(-12).ToString("yyyy-MM-dd HH:mm:ss");
                if (!ENABLE_12H_RULE) back12h = DateTime.UtcNow.AddHours(123).ToString("yyyy-MM-dd HH:mm:ss");

                string back2h = DateTime.UtcNow.AddMinutes(-MAX_CHECK_IN_TIME).ToString("yyyy-MM-dd HH:mm:ss");

                sql = string.Format(sql, id, back12h, back2h);

                sql += string.Format(" AND Lat < {0} AND Lat > {1} AND Lng > {2} AND Lng < {3}", neLat.ToString().Replace(',', '.'), swLat.ToString().Replace(',', '.'), swLng.ToString().Replace(',', '.'), neLng.ToString().Replace(',', '.'));

                var checkedInUsers = db.Users.SqlQuery(sql).ToList();

                // Make poiResult
                // FB events
                foreach (var item in fbEvents) {
                    var cr = new PoiResult() {
                        venueId = item.fbLocationId,
                        venueName = item.locationName,
                        lat = item.latitude,
                        lng = item.longitude,
                        state = "FB_EVENT",
                        street = item.locationStreet,
                        city = item.cityName,
                        eventId = item.eventId,
                        eventName = item.eventName,
                        eventDescription = item.eventDescription,
                        eventStarttimeUtc = item.eventStarttimeUtc,
                        eventStarttimeLocal = item.eventStarttimeLocal
                    };
                    if (cr.eventDescription == null) cr.eventDescription = "";
                    poiResult.Add(cr);
                }

                // Checked in users
                foreach (var item in checkedInUsers) {
                    var cr = new PoiResult() {
                        venueId = item.VenueId,
                        venueName = item.VenueName,
                        lat = item.Lat,
                        lng = item.Lng,
                        state = item.State,
                        category = item.Category,
                        venueImageUrl = item.VenueImageUrl
                    };

                    if (item.Id == id) cr.myVenue = true;

                    // Check is this venue already on the list
                    var resVenue = poiResult.SingleOrDefault(z => z.venueId == item.VenueId);

                    if (resVenue == null) {
                        poiResult.Add(cr);
                    } else {
                        // There is one (or more) checkins in this venue
                        if (resVenue.state == "MATCHING" || resVenue.state == "REVIEW") {
                            // Always put CHECKED_IN on the list in stead of MATCHING / REVIEW if possible
                            poiResult.Remove(resVenue);
                            poiResult.Add(cr);
                        } else {
                            // This venue is alrady marked as CHECKED_IN so do nothing
                        }
                    }
                }

                // Recommended venues
                foreach (var item in recommendedVenues) {
                    var cr = new PoiResult() {
                        venueId = item.venueId,
                        venueName = item.venueName,
                        street = item.street,
                        lat = item.lat,
                        lng = item.lng,
                        state = "RECOMMENDED_VENUE",
                        category = item.category,
                        eventId = item.facebookId,
                        venueImageUrl = item.venueImageUrl
                    };
                    poiResult.Add(cr);
                }
            }
            return poiResult;
        }
    }
}