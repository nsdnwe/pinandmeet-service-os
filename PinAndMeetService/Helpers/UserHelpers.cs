using Newtonsoft.Json;
using PinAndMeetService.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

//public enum LoggingLevels { 
//    NO = 0,                     // Only errors
//    ONLY_SERVICE_EVENTS = 1,    // Errors and service events (no events from client)
//    ALL_NO_MAP_INSTANT = 10,    // Errors and events (not map events)
//    ALL_NO_MAP_BATCH = 20,      // Errors and events (not map events).  Send as a batch
//    ALL_INSTANT = 110,          // Errors and events 
//    ALL_BATCH = 120             // Errors and events                    Send as a batch

namespace PinAndMeetService.Helpers {
    public static class UserHelpers {

        // Update also in PoiHelpers.cs
        const int MAX_CHECK_IN_TIME = 120; // 2h
        const int MAX_MATCHING_TIME = 6 * 60; // (MATCHING or REVIEW) 6h
        const bool ENABLE_12H_RULE = true; // false is just for testing
        const int MAX_CHECK_IN_ALERTS_SENT = 1;

        // fbData sample: {"id":"10153374187653948","birthday":"12/21/1969","first_name":"Niko","gender":"male","last_name":"Wessman","link":"https://www.facebook.com/app_scoped_user_id/10153374187653948/","locale":"en_GB","name":"Niko Wessman","timezone":3,"updated_time":"2013-12-20T16:11:48+0000","verified":true}
        public static string UpdateFbUserData(dynamic userData) {
            string id = userData.fbData.id;
            int loggingLevel = 110; // Because it's not yet defined, user has not logged in

            GeneralHelpers.AddLogEvent(id, "", "UserHelpers", "UpdateFbUserData", "Start", JsonConvert.SerializeObject(userData), loggingLevel);

            using (DB db = new DB()) {
                var user = db.Users.SingleOrDefault(z => z.Id == id);

                string uuid = userData.device.uuid;
                if (db.Users.Any(z => z.UuId == uuid && z.Banned)) {
                    GeneralHelpers.AddLogEvent("", "", "UserHelpers", "SignIn", "Banned uuid", uuid);
                    return "You are banned from this application due to inappropriate behaviour or content that is violating the Terms of use (UCLA)";
                }

                bool isNewUser = false;
                if (user == null) {
                    isNewUser = true;
                    user = new User() { // TODO: Muuta LoggingLevel = 20
                        Deleted = false, TestUser = false, IsPnmAccount = false, NonFbImage = false, SessionId = "", PartnerId = "", State = "", Lat = 0, Lng = 0, VenueId = "",
                        LoggingLevel = 1, LogEvents = true, CheckinDatetime = new DateTime(2000, 1, 1), Created = DateTime.UtcNow,
                        NewPoisExist = false, SwLat = 0, SwLng = 0, NeLat = 0, NeLng = 0, VenueOutOfRange = false, Banned = false, GeolocationUpdatedDatetime = new DateTime(2000, 1, 1), CheckinAlertCount = 0, SendCheckinAlerts = true
                    };
                    db.Users.Add(user);
                    GeneralHelpers.AddLogEvent(id, "", "UserHelpers", "UpdateFbUserData", "New user added", user.Id, loggingLevel);
                    string fnam = userData.fbData.name;
                    string imur = userData.fbData.imageUrl;
                    Task.Factory.StartNew(() => EmailHelpers.SendNewUserEmail(fnam, imur));
                }

                // FB data
                user.Id = userData.fbData.id;
                user.Birthday = userData.fbData.birthday;
                user.FbAccessToken = userData.fbAccessToken;
                user.First_name = userData.fbData.first_name;
                user.Gender = userData.fbData.gender;
                user.Last_name = userData.fbData.last_name;
                user.Link = userData.fbData.link;
                user.Locale = userData.fbData.locale;
                user.Name = userData.fbData.name;
                user.Timezone = userData.fbData.timezone;
                user.Updated_time = userData.fbData.updated_time;
                user.Verified = userData.fbData.verified;
                if(!user.NonFbImage) user.ImageUrl = userData.fbData.imageUrl;

                // Device data
                user.Platform = userData.device.platform;
                user.Model = userData.device.model;
                user.Version = userData.device.version;
                user.UuId = userData.device.uuid;

                // Other data
                if (userData.testUser != null) user.TestUser = userData.testUser;

                if (user.First_name == "Pin'n'Meet") user.First_name = "Pin'n'Meet?"; // So no-one can use this as first name. Messes unit tests

                string localTimeStamp = userData.fbData.localTimeStamp;
                user.TimeZoneBias = getBias(localTimeStamp);

                db.SaveChanges();
                if (isNewUser) {
                    AddHistoryEvent(HistoryEventEnum.ACCOUNT_CREATED, id);
                }
                return "";
            }
        }

        private static int getBias(string localTimeStamp) {
            DateTime now = DateTime.UtcNow;

            string[] dateTimeParts = localTimeStamp.Split(' ');
            string[] dateParts = dateTimeParts[0].Split('-');
            string[] timePartsWithoutMs = dateTimeParts[1].Split('.');
            string[] timeParts = timePartsWithoutMs[0].Split(':');

            DateTime localNow = new DateTime(int.Parse(dateParts[0]), int.Parse(dateParts[1]), int.Parse(dateParts[2]), int.Parse(timeParts[0]), int.Parse(timeParts[1]), int.Parse(timeParts[2]));

            double biasD = (now - localNow).TotalMinutes;
            return (int)(Math.Round(biasD / 10, 0) * 10);
        }

        public static ValidateSessionAndStateResult ValidateSessionAndState(dynamic userData) {
            GeneralHelpers.AddLogEvent(userData, "ValidateSessionAndState", "Start", JsonConvert.SerializeObject(userData));
            string id = userData.id;
            string sessionId = userData.sessionId;

            using (DB db = new DB()) {
                var user = db.Users.SingleOrDefault(z => z.Id == id && !z.Deleted); // id == FB user id

                // If no user found with this Id
                if (user == null) {
                    GeneralHelpers.AddLogError(userData, "ValidateSessionAndState", "User not found", "");
                    return new ValidateSessionAndStateResult() { sessionId = "", id = "", state = "", error = "User not found", errorCode = "1" };
                }

                if (user.Banned) {
                    GeneralHelpers.AddLogError(userData, "ValidateSessionAndState", "User banned", "");
                    return new ValidateSessionAndStateResult() { sessionId = "", id = "", state = "", error = "You are banned from this application due to inappropriate behaviour or content that is violating the Terms of use (UCLA)", errorCode = "2" };
                }

                // If sessionId is invalid, generate new sessionId
                if (user.SessionId != sessionId || sessionId == "") {

                    // Clear any possible partner
                    var partner = db.Users.FirstOrDefault(z => z.PartnerId == id && !z.Deleted); // id == FB user id
                    if (partner != null) {
                        // Clear this user values
                        clearCheckInValues(partner);
                        db.SaveChanges();
                        // Send message (partner.Id, partner.SessionId, 1, "The other user closed the application.");
                    }

                    // Clear this user values
                    clearCheckInValues(user);

                    // Generate new sessionId
                    string newSessionId = System.Guid.NewGuid().ToString();
                    GeneralHelpers.AddLogEvent(userData, "ValidateSessionAndState", "New sessionId generated", newSessionId);
                    user.SessionId = newSessionId;
                    user.ApmsRegistrationId = "";
                    user.GcmRegistrationId = "";
                    db.SaveChanges();
                } else {
                    
                    // Session is valid

                    // -----------------------------------------------------------------------
                    // State: Not on the map "" and time has passed a lot
                    // -----------------------------------------------------------------------

                    if (
                        (user.State == "CHECKED_IN" && user.CheckinDatetime < DateTime.UtcNow.AddMinutes(-MAX_CHECK_IN_TIME)) ||
                        (user.State == "MATCHING" && user.CheckinDatetime < DateTime.UtcNow.AddMinutes(-MAX_MATCHING_TIME))
                        ) {
                        GeneralHelpers.AddLogEvent(userData, "ValidateSessionAndState", "Max check in or matching time exceeded. State to map");
                        user.State = "";
                        db.SaveChanges();

                        if (user.PartnerId != "") {
                            user.PartnerId = "";
                            db.SaveChanges();

                            var partner = db.Users.FirstOrDefault(z => z.PartnerId == id && !z.Deleted); // id == FB user id
                            if (partner != null) {
                                // Clear this user values
                                GeneralHelpers.AddLogEvent(userData, "ValidateSessionAndState", "Matching time exceeded", partner.Id);
                                clearCheckInValues(partner);
                                db.SaveChanges();
                                sendStateChangeAlert(user.Id, "Maximum matching time exceeded.");
                            }
                        }
                    }

                    // -----------------------------------------------------------------------
                    // State: "" or CHECKED_IN 
                    // -----------------------------------------------------------------------
                    
                    else if (user.State == "" || user.State == "CHECKED_IN") {
                        if (user.PartnerId != "") {
                            GeneralHelpers.AddLogError(userData, "ValidateSessionAndState", "PartnerId shoud not exist", user.PartnerId);
                            user.PartnerId = "";
                            db.SaveChanges();
                        }

                        // Again this should not ever happend. State "" or Checkedin, but still have a partner. Let's clear the mess
                        var partner = db.Users.FirstOrDefault(z => z.PartnerId == id && !z.Deleted); // id == FB user id
                        if (partner != null) {
                            // Clear this user values
                            GeneralHelpers.AddLogError(userData, "ValidateSessionAndState", "PartnerId should not be found", partner.Id);
                            clearCheckInValues(partner);
                            db.SaveChanges();
                            sendStateChangeAlert(partner.Id, "The other user closed the application.");
                        }

                        // Checked in max 2 h
                        if (user.State == "CHECKED_IN" && user.CheckinDatetime < DateTime.UtcNow.AddMinutes(-MAX_CHECK_IN_TIME)) {
                            GeneralHelpers.AddLogEvent(userData, "ValidateSessionAndState", "Max check in time exceeded. State to map");
                            user.State = "";
                            db.SaveChanges();
                        }
                    }

                    // -----------------------------------------------------------------------
                    // State: MATCHING 
                    // -----------------------------------------------------------------------

                    if (user.State == "MATCHING") {
                        string myPartnerId = user.PartnerId;
                        
                        if(myPartnerId == "") {
                            // Should never happen
                            GeneralHelpers.AddLogError(userData, "ValidateSessionAndState", "In matching state but partnerId is empty", "");
                            clearCheckInValues(user);
                            db.SaveChanges();
                        } else {
                            var partner = db.Users.SingleOrDefault(z => z.Id == myPartnerId && !z.Deleted); // id == FB user id
                            if (partner != null) {
                                // Yes, the person was found
                                // Is his/hers PartnerId still my id
                                if (partner.PartnerId == id && partner.State == "MATCHING") {
                                    GeneralHelpers.AddLogEvent(userData, "ValidateSessionAndState", "We are still matching", partner.Id);
                                } else {
                                    // The partner was found but he/she is with someone else now
                                    // Back on map
                                    GeneralHelpers.AddLogEvent(userData, "ValidateSessionAndState", "Partner is now with someone else", myPartnerId + " " + partner.PartnerId + " " + partner.State);
                                    clearCheckInValues(user);
                                    db.SaveChanges();
                                }
                            } else {
                                // No partner found. Maybe deleted
                                GeneralHelpers.AddLogError(userData, "ValidateSessionAndState", "Maybe partner is deleted", myPartnerId);
                                clearCheckInValues(user);
                                db.SaveChanges();
                            }
                        }
                    }
                }

                // Activate alerts just in case being 
                user.SendCheckinAlerts = true;
                db.SaveChanges();

                // If on Review page, no need to check partner state

                return new ValidateSessionAndStateResult() {
                    id = user.Id,
                    sessionId = user.SessionId,
                    state = user.State,
                    imageUrl = user.ImageUrl,
                    first_name = user.First_name,
                    last_name = user.Last_name,
                    email = user.Email,
                    birthday = user.Birthday,
                    error = "",
                    errorCode = "",
                    loggingLevel = user.LoggingLevel
                };
            }
        }

        public static void SetStateForTesting(dynamic userData) {
            GeneralHelpers.AddLogEvent(userData, "SetStateForTesting", "Start", JsonConvert.SerializeObject(userData));
            string id = userData.id; // FB user id
            string sessionId = userData.sessionId;
            bool? partnerContacted = userData.partnerContacted;
            using (DB db = new DB()) {
                User u = db.Users.SingleOrDefault(z => z.Id == id && z.SessionId == sessionId);
                u.State = userData.state;
                u.PartnerId = userData.partnerId;
                u.PartnerContacted = userData.partnerContacted;
                u.VenueId = userData.venueId;
                db.SaveChanges();
            }
        }

        // Return pertner is "FOUND" or "NOT_FOUND". Or return "ERROR1" if user is not found
        public static CheckInResult CheckIn(dynamic userData) {
            GeneralHelpers.AddLogEvent(userData, "CheckIn", "Start", JsonConvert.SerializeObject(userData));
            string id = userData.id; // FB user id
            string sessionId = userData.sessionId;
            string venueId = userData.venueId;
            string venueCity = userData.city;
            string venueName = userData.venueName;

            using (DB db = new DB()) {
                User thisUser = db.Users.SingleOrDefault(z => z.Id == id && z.SessionId == sessionId);
                if (thisUser == null) {
                    GeneralHelpers.AddLogError(userData, "CheckIn", "User not found", "");
                    return new CheckInResult() { errorCode = "1", error = "User not found" };
                }

                // Something strange has happened. Maybe network error while running this method and client clicked retry
                if (thisUser.State != "") {
                    GeneralHelpers.AddLogError(userData, "CheckIn", "User was already checked in. Trying to do again", thisUser.State);
                    return new CheckInResult() { result = "FOUND", errorCode = "2", error = "Invalid state" };
                }

                // ERROR: userData.swLng cuts last 3 digits off (why?) -> Hack
                string dynamicString = Convert.ToString(userData);

                // Set my state
                thisUser.State = "CHECKED_IN";
                thisUser.VenueId = venueId;
                thisUser.VenueName = venueName;
                thisUser.VenueCity = venueCity;
                thisUser.Lat = GeneralHelpers.GetDecimalValueFromDynamic(dynamicString, "lat");
                thisUser.Lng = GeneralHelpers.GetDecimalValueFromDynamic(dynamicString, "lng");
                thisUser.Category = userData.category;
                thisUser.VenueImageUrl = userData.venueImageUrl;
                thisUser.CheckinDatetime = DateTime.UtcNow;
                thisUser.PartnerContacted = false; 
                thisUser.VenueOutOfRange = false;

                string venueImageUrl = userData.venueImageUrl;
                AddHistoryEvent(HistoryEventEnum.CHECK_IN, thisUser.Id, "", venueId, venueName + " in " + venueCity, venueImageUrl);


                // Try to find, is there someone else in the same venue?
                DateTime checkinTimelimit = DateTime.UtcNow.AddMinutes(-MAX_CHECK_IN_TIME);
                var partnersFound = db.Users.Where(z => z.VenueId == venueId && !z.Deleted && z.State == "CHECKED_IN" && z.Id != id && z.CheckinDatetime > checkinTimelimit).ToList();
                
                User partner = null;
                string justMetPartner = "";

                if (partnersFound != null) {
                    // Check 12h rule
                    DateTime contact12hRule = DateTime.UtcNow.AddHours(-12);
                    foreach (var p in partnersFound) {
                        Contact recentContact = db.Contacts.FirstOrDefault(z => z.Id == thisUser.Id && z.PartnerId == p.Id && z.Created > contact12hRule);
                        bool blocked = db.Blocks.Any(z => (z.BlockedUserId == thisUser.Id && z.Id == p.Id) || (z.BlockedUserId == p.Id && z.Id == thisUser.Id));
                        if(!blocked) {
                            if (recentContact != null && ENABLE_12H_RULE) {
                                // Found someone that was met within 12h
                                if (justMetPartner == "") justMetPartner = p.First_name; else justMetPartner = "*";
                            } else {
                                // Found someone that is not met within 12h
                                partner = p;
                                break;
                            }
                        }
                    }
                }

                if (partner != null) {
                    // Yes, partner was found

                    partner.PartnerId = id; // My id
                    partner.State = "MATCHING";
                    db.SaveChanges();

                    // Add message to partner's queue
                    sendStateChangeAlert(partner.Id, "You have a match");

                    thisUser.State = "MATCHING";
                    thisUser.PartnerId = partner.Id;
                    db.SaveChanges();

                    AddHistoryEvent(HistoryEventEnum.MATCH, id, partner.Id, venueId, venueName + " in " + venueCity, venueImageUrl);
                    AddHistoryEvent(HistoryEventEnum.MATCH, partner.Id, thisUser.Id, venueId, venueName + " in " + venueCity, venueImageUrl);

                    // Add contact information
                    var contact = new Contact() { Id = id, PartnerId = partner.Id, Created = DateTime.UtcNow };
                    db.Contacts.Add(contact);
                    contact = new Contact() { PartnerId = id, Id = partner.Id, Created = DateTime.UtcNow };
                    db.Contacts.Add(contact);
                    db.SaveChanges();

                    updateNewPoisExist(id);

                    string name1 = thisUser.Name;
                    string name2 = partner.Name;
                    string imageLink1 = thisUser.ImageUrl;
                    string imageLink2 = partner.ImageUrl;
                    Task.Factory.StartNew(() => EmailHelpers.SendNewMatchEmail(name1, imageLink1, name2, imageLink2, venueName, venueCity));

                    return new CheckInResult() { result = "FOUND" };
                } else {
                    // No partner found. 
                    // Just set state as checked in

                    thisUser.PartnerId = "";
                    db.SaveChanges();
                    updateNewPoisExist(id);

                    // Send check in alerts to nearby other users

                    Task.Factory.StartNew(() => sendCheckinAlertMessages(id));

                    return new CheckInResult() { result = "NOT_FOUND", justMetPartnerFirstName = justMetPartner }; // Lisää justMetPartner
                }
            }
        }

        // Put more time to refresh in check in 2h limit
        public static void ExtendCheckIn(dynamic userData) {
            GeneralHelpers.AddLogEvent(userData, "ExtendCheckIn", "Start", JsonConvert.SerializeObject(userData));
            string id = userData.id; // FB user id
            string sessionId = userData.sessionId;

            using (DB db = new DB()) {
                User thisUser = db.Users.SingleOrDefault(z => z.Id == id && z.SessionId == sessionId);
                if (thisUser == null) {
                    GeneralHelpers.AddLogError(userData, "ExtendCheckIn", "User not found", "");
                    return;
                }

                // Something strange has happened. Maybe network error while running this method and client clicked retry
                if (thisUser.State != "CHECKED_IN") {
                    GeneralHelpers.AddLogError(userData, "ExtendCheckIn", "Invalid state to resume check in", thisUser.State);
                    return;
                }

                thisUser.CheckinDatetime = DateTime.UtcNow;
                db.SaveChanges();
            }
        }

        // Find other not checked in users nearby and send a push message
        // id = thisUserId
        public static void sendCheckinAlertMessages(string id) {
            using (DB db = new DB()) {
                var thisUser = db.Users.Single(z => z.Id == id);

                // 12h limit. Not show who you just met 
                //  2h limit: Not show who has been checked in more than 2h 

                string back12h = DateTime.UtcNow.AddHours(-12).ToString("yyyy-MM-dd HH:mm:ss");
                if (!ENABLE_12H_RULE) back12h = DateTime.UtcNow.AddHours(123).ToString("yyyy-MM-dd HH:mm:ss");

                string back2h = DateTime.UtcNow.AddMinutes(-MAX_CHECK_IN_TIME).ToString("yyyy-MM-dd HH:mm:ss");

                string sql = @"
                    SELECT * FROM Users 
                    WHERE Deleted = 0 
                    AND [Id] <> '{0}' 
                    AND [State] = ''
                    AND VenueOutOfRange = 0
                    AND [Id] NOT IN (SELECT [Id] FROM Blocks WHERE [BlockedUserId] = '{0}') 
                    AND [Id] NOT IN (SELECT [BlockedUserId] FROM Blocks WHERE [Id] = '{0}') 
                    AND [Id] NOT IN (SELECT [PartnerId] FROM Contacts WHERE [Id] = '{0}' AND Created > '{1}') 
                    AND SendCheckinAlerts = 1
                    AND CheckinAlertCount < {2}
                    AND GeolocationUpdatedDatetime > '{3}'";

                sql = string.Format(sql, id, back12h, MAX_CHECK_IN_ALERTS_SENT, back2h);

                // Only those inside map zoom
                string mapLimit = " AND SwLat < {0} AND NeLat > {0} AND SwLng < {1} AND NeLng > {1}";
                sql += string.Format(mapLimit, thisUser.Lat.ToString().Replace(',', '.'), thisUser.Lng.ToString().Replace(',', '.'));

                var usersNearBy = db.Users.SqlQuery(sql).ToList();
                foreach (var user in usersNearBy) {
                    user.CheckinAlertCount++;
                    db.SaveChanges();

                    // ------------------------------------------------------
                    // HERE CALL PUSH MESSAGE MODULE TO SEND THE MESSAGE
                    // ------------------------------------------------------
                    var toGcm = user.GcmRegistrationId;
                    var toApgm = user.ApmsRegistrationId;
                    var toPlatform = user.Platform;

                    GeneralHelpers.AddLogEvent(user.Id, user.SessionId, "UserHelpers", "private sendCheckinAlertMessages", "Platform", toPlatform); // TODO: true -> parameter

                    if (toPlatform.ToLower() == "android") {
                        if (toGcm == "") {
                            GeneralHelpers.AddLogError(user.Id, user.SessionId, "UserHelpers", "private sendCheckinAlertMessages", "Message receiver has no GCM id", ""); // TODO: true -> parameter
                            return;
                        }
                        PushHelpers.SendGcmNotification(toGcm, "Pin'n'Meet", "Somebody checked in nearby");
                    } else {
                        if (toApgm == "") {
                            GeneralHelpers.AddLogError(user.Id, user.SessionId, "UserHelpers", "private sendCheckinAlertMessages", "Message receiver has no APNS id", ""); // TODO: true -> parameter
                            return;
                        }
                        // IOS
                        GeneralHelpers.AddLogEvent(user.Id, user.SessionId, "UserHelpers", "private sendCheckinAlertMessages", "APNS push", ""); // TODO: true -> parameter
                        PushHelpers.SendApnsNotification(toApgm, "Pin'n'Meet", "Somebody checked in nearby", 0);
                    }
                    GeneralHelpers.AddLogEvent(user.Id, user.SessionId, "UserHelpers", "private sendCheckinAlertMessages", "Push completed", ""); // TODO: true -> parameter
                }
            }
        }

        public static string CheckOut(dynamic userData) {
            GeneralHelpers.AddLogEvent(userData, "CheckOut", "Start", JsonConvert.SerializeObject(userData));

            using (DB db = new DB()) {
                User user = getUser(userData, db);
                if (user == null) return "ERROR";
                clearCheckInValues(user);
                db.SaveChanges();
            }
            return "CHECKED_OUT";
        }


        // Get partner data
        public static PartnerResult GetPartner(dynamic userData) {
            GeneralHelpers.AddLogEvent(userData, "GetPartner", "Start", JsonConvert.SerializeObject(userData));
            using (DB db = new DB()) {
                User user = getUser(userData, db);

                // Handle errors
                if (user == null) {
                    GeneralHelpers.AddLogError(userData, "GetPartner", "User not found", "");
                    return new PartnerResult() { error = "ERROR1" };
                }

                string partnerId = user.PartnerId;
                if (partnerId == "") {
                    GeneralHelpers.AddLogError(userData, "GetPartner", "PartnerId is blank", "");
                    return new PartnerResult() { error = "ERROR2" };
                }

                User partner = db.Users.SingleOrDefault(z => z.Id == partnerId);
                if (partner == null) {
                    GeneralHelpers.AddLogError(userData, "GetPartner", "Partner data not found", partnerId);
                    return new PartnerResult() { error = "ERROR3" };
                }

                // All OK, get data
                // Partner found
                var partnerData = new PartnerResult() {
                    error = "",
                    id = partnerId,
                    name = partner.Name,
                    imageUrl = partner.ImageUrl
                };
                return partnerData;
            }
        }

        // Get this user state from DB
        public static StateResult GetState(dynamic userData) {
            GeneralHelpers.AddLogEvent(userData, "GetState", "Start", JsonConvert.SerializeObject(userData));
            using (DB db = new DB()) {
                User user = getUser(userData, db);
                if (user == null) return new StateResult() { error = "User not found", errorCode = "1" };
                return new StateResult() { error = "", errorCode = "", state = user.State, loggingLevel = user.LoggingLevel }; // Always return logEvents in case it has changed
            }
        }

        private static void clearCheckInValues(User u) {
            u.State = "";
            u.VenueId = "";
            u.VenueName = "";   
            u.VenueCity = "";
            u.PartnerId = "";
            u.PartnerContacted = false;
            u.Lat = 0;
            u.Lng = 0;
            u.CheckinDatetime = DateTime.UtcNow;
            updateNewPoisExist(u.Id);
        }

        public static string ResetUser(dynamic userData) {
            GeneralHelpers.AddLogEvent(userData, "ResetUser", "Start", JsonConvert.SerializeObject(userData));
            using (DB db = new DB()) {
                string id = userData.id;
                User user = getUser(id, db);
                if (user == null) return "ERROR";
                clearCheckInValues(user);
                db.SaveChanges();
                return "";
            }
        }

        public static string ReportUser(dynamic userData) {
            GeneralHelpers.AddLogEvent(userData, "ReportUser", "Start", JsonConvert.SerializeObject(userData));
            string targetUserId = userData.targetUserId;
            string desc = userData.description;

            using (DB db = new DB()) {
                User user = getUser(userData, db);
                if (user == null) return "ERROR1";

                User targetUser = getUser(targetUserId, db);
                if (targetUser == null) return "ERROR2";

                var report = new ViolationReport() { Id = user.Id, TargetUserId = targetUser.Id, Created = DateTime.UtcNow, Description = userData.description };
                db.ViolationReports.Add(report);

                EmailHelpers.SendViolationReportEmail(user.Id, user.Name, targetUser.Id, targetUser.Name, desc);
                db.SaveChanges();

                AddHistoryEvent(HistoryEventEnum.REPORT, targetUser.Id, user.Id,"","","", desc);

                return "";
            }
        }

        public static string SendMessage(dynamic messageData) {
            GeneralHelpers.AddLogEvent(messageData, "SendMessage", "Start", JsonConvert.SerializeObject(messageData));
            string id = messageData.id; // FB user id
            string sessionId = messageData.sessionId;
            string toId = messageData.toId;
            string message = messageData.message;

            Task.Factory.StartNew(() => sendMessage(id, sessionId, toId, message));
            return "";
        }

        private static string sendStateChangeAlert(string toId, string text = "") {
            Task.Factory.StartNew(() => sendMessage("", "", toId, text, 1));
            return "";
        }

        // Note: 
        // toSessionId is get by toId
        // If an alert, (from) id and sessionId are ""
        // Type: 0:Chat message, 1:Alert
        private static void sendMessage(string id, string sessionId, string toId, string text, int type = 0) {
            GeneralHelpers.AddLogEvent(id, sessionId, "UserHelpers", "private sendMessage", "Start", ""); // TODO: true -> parameter
            using (DB db = new DB()) {

                string fromName = "Pin'n'Meet";  // <--------------------- NOTE this, If not a message from a user, it's from Pin'n'Meet. This is checked in when receiving in clients $("body").on("pushMessageEvent", function (event, title, message, dummyId) {

                // Check is FROM user valid. "" == From Pin'n'Meet
                if (id != "") {
                    User user = getUser(id, sessionId, db);
                    if (user == null) return;
                    fromName = user.Name;
                }

                // Get TO user sessionId
                User toUser = getUser(toId, db);
                if (toUser == null) return;
                var toSessionId = toUser.SessionId;

                // Make new message
                var m = new Message() {
                    FromId = id,
                    FromSessionId = sessionId,
                    ToId = toId,
                    ToSessionId = toSessionId,
                    Text = text,
                    Type = type,
                    Created = DateTime.UtcNow,
                    Delivered = false
                };
                db.Messages.Add(m);
                db.SaveChanges();

                // ------------------------------------------------------
                // HERE CALL PUSH MESSAGE MODULE TO SEND THE MESSAGE
                // ------------------------------------------------------
                var toGcm = toUser.GcmRegistrationId;
                var toApgm = toUser.ApmsRegistrationId;
                var toPlatform = toUser.Platform;

                GeneralHelpers.AddLogEvent(id, sessionId, "UserHelpers", "private sendMessage", "Platform", toPlatform); // TODO: true -> parameter

                if (toPlatform.ToLower() == "android") {
                    if (toGcm == "") {
                        GeneralHelpers.AddLogError(id, sessionId, "UserHelpers", "private sendMessage", "Message receiver has no GCM id", ""); // TODO: true -> parameter
                        return;
                    }
                    PushHelpers.SendGcmNotification(toGcm, fromName, text);
                } else {
                    if (toApgm == "") {
                        GeneralHelpers.AddLogError(id, sessionId, "UserHelpers", "private sendMessage", "Message receiver has no APNS id", ""); // TODO: true -> parameter
                        return;
                    }
                    // IOS
                    GeneralHelpers.AddLogEvent(id, sessionId, "UserHelpers", "private sendMessage", "APNS push", ""); // TODO: true -> parameter
                    PushHelpers.SendApnsNotification(toApgm, fromName, text, m.MessageId);
                }
                GeneralHelpers.AddLogEvent(id, sessionId, "UserHelpers", "private sendMessage", "Push completed", ""); // TODO: true -> parameter
            }
            return;
        }

        public static List<ChatMessageResult> GetChatMessages(dynamic messageData) {
            GeneralHelpers.AddLogEvent(messageData, "GetChatMessages", "Start", JsonConvert.SerializeObject(messageData));
            string id = messageData.id; // FB user id
            string fromId = messageData.fromId;
            var result = new List<ChatMessageResult>();
            using (DB db = new DB()) {
                // From x to me or from me to x
                var limit = DateTime.UtcNow.AddHours(-1); // Show only last hour messages
                var messages = db.Messages.Where(z => z.Type == 0 && ((z.FromId == fromId && z.ToId == id) || (z.FromId == id && z.ToId == fromId)) && z.Created > limit)
                    .Take(50).ToList();
                foreach (var item in messages) {
                    string from = "Me";
                    if (item.FromId != id) from = fromId;
                    result.Add(new ChatMessageResult() { id = item.MessageId, from = from,  message = item.Text});
                }
            }
            return result;
        }

        // Return one message based on messageId
        public static ChatMessageResult GetSingleChatMessages(dynamic messageData) {
            GeneralHelpers.AddLogEvent(messageData, "GetSingleChatMessages", "Start", JsonConvert.SerializeObject(messageData));
            string id = messageData.id; // FB user id
            string fromId = messageData.fromId;
            int messageId = messageData.messageId;
            using (DB db = new DB()) {
                // From x to me or from me to x
                var message = db.Messages.SingleOrDefault(z => z.Type == 0 && z.MessageId == messageId && ((z.FromId == fromId && z.ToId == id) || (z.FromId == id && z.ToId == fromId)));
                string from = "Me";
                if (message.FromId != id) from = fromId;
                return(new ChatMessageResult() { id = message.MessageId, from = from, message = message.Text });
            }
        }
        

        public static string PartnerFound(dynamic userData) {
            GeneralHelpers.AddLogEvent(userData, "PartnerFound", "Start", JsonConvert.SerializeObject(userData));
            string id = userData.id; // FB user id
            string sessionId = userData.sessionId;

            using (DB db = new DB()) {
                User thisUser = getUser(userData, db);
                if (thisUser == null) return "ERROR1";
                if (thisUser.State == "") {
                    GeneralHelpers.AddLogEvent(userData, "PartnerFound", "The other user already clicked not found");
                    return "ERROR3";
                }

                var partnerId = thisUser.PartnerId;

                User partner = getUser(partnerId, db);
                if (partner == null) return "ERROR2";

                thisUser.PartnerContacted = true; // Just out of interest, no use
                thisUser.State = "REVIEW";
                db.SaveChanges();

                AddHistoryEvent(HistoryEventEnum.CONTACT, id, partnerId, thisUser.VenueId, thisUser.VenueName, thisUser.VenueImageUrl);
                return "REVIEW";
            }
        }

        public static string PartnerNotFound(dynamic userData) {
            GeneralHelpers.AddLogEvent(userData, "PartnerNotFound", "Start", JsonConvert.SerializeObject(userData));
            string id = userData.id; // FB user id
            string sessionId = userData.sessionId;
            using (DB db = new DB()) {
                User thisUser = getUser(userData, db);
                if (thisUser == null) return "ERROR1";
                
                var partnerId = thisUser.PartnerId;

                User partner = getUser(partnerId, db);
                if (partner == null) {
                    // Can be null (partner not found) if the partner already clicked Not Found
                    GeneralHelpers.AddLogEvent(userData, "PartnerNotFound", "Partner data not found (already not found)");
                } else {
                    // Partner was found
                    if (partner.State != "REVIEW" && partner.PartnerId == id) {
                        clearCheckInValues(partner);
                        sendStateChangeAlert(partner.Id, "The other user didn't find you");
                    }
                }
                ResetUser(userData);
                db.SaveChanges();

                AddHistoryEvent(HistoryEventEnum.NO_CONTACT, id, partnerId, thisUser.VenueId, thisUser.VenueName, thisUser.VenueImageUrl);
                return "";
            }
        }

        public static string SetVenueOutOfRange(dynamic userData) {
            GeneralHelpers.AddLogEvent(userData, "SetVenueOutOfRange", "Start", JsonConvert.SerializeObject(userData));
            using (DB db = new DB()) {
                User thisUser = getUser(userData, db);
                if (thisUser == null) return "ERROR1";

                thisUser.VenueOutOfRange = true;
                db.SaveChanges();
                return "";
            }
        }

        public static string SaveReview(dynamic userData) {
            GeneralHelpers.AddLogEvent(userData, "SaveReview", "Start", JsonConvert.SerializeObject(userData));

            using (DB db = new DB()) {
                User user = getUser(userData, db);

                var review = new Review() {
                    Id = userData.partnerId,  // Note that id = partnerId
                    FromId = userData.id,
                    Stars = userData.stars,
                    Evaluation = userData.evaluation,
                    Created = DateTime.UtcNow,
                    CreatedDate = DateTime.UtcNow.ToString("yyyy-MM-dd")
                };
                db.Reviews.Add(review);
                db.SaveChanges();

                string partnerId = userData.partnerId;

                AddHistoryEvent(HistoryEventEnum.REVIEW_GIVEN, user.Id, partnerId, user.VenueId, user.VenueName, user.VenueImageUrl);
                AddHistoryEvent(HistoryEventEnum.REVIEW_RECEIVED, partnerId, user.Id, user.VenueId, user.VenueName, user.VenueImageUrl);

                bool blockUser = userData.blockUser;
                if (blockUser) {
                    var block = new Block() { Id = userData.id, BlockedUserId = userData.partnerId, Created = DateTime.UtcNow };
                    db.Blocks.Add(block);
                    db.SaveChanges();
                }

                ResetUser(userData);

                return "";
            }
        }

        public static List<Review> GetReviews(dynamic userData) {
            GeneralHelpers.AddLogEvent(userData, "GetReviews", "Start", JsonConvert.SerializeObject(userData));
            string id = userData.id;

            using (DB db = new DB()) {
                // Users.Id = Reviews.FromId is to get the full name of the person given the review
                string sql = "SELECT Reviews.ReviewId, Reviews.Id, Reviews.Stars, Reviews.Evaluation, Reviews.FromId, Reviews.Created, Reviews.CreatedDate, Users.Name AS FromName, Users.ImageUrl AS FromImageUrl ";
                sql += "FROM Reviews, Users WHERE Reviews.Id = '" + id + "' AND Reviews.Evaluation <> '' AND Users.Id = Reviews.FromId ORDER BY Reviews.Id DESC";
                return db.Reviews.SqlQuery(sql).ToList();
            }
        }

        public static string SignUp(dynamic userData) {
            GeneralHelpers.AddLogEvent("", "", "UserHelpers", "SignUp", "Start", JsonConvert.SerializeObject(userData));

            string firstName = userData.firstName;
            string lastName = userData.lastName;
            string fullName = firstName + " " + lastName;
            string email = userData.email;
            string passwordHash = userData.passwordHash;
            string birthday = userData.birthday;
            string localTimeStamp = userData.localTimeStamp;

            using (DB db = new DB()) {
                // For testing only
                if (email.ToLower() == "niko.wessman@nsd.fi") RemoveTestAccountNwe();

                User thisUser = db.Users.SingleOrDefault(z => (z.Email == email || z.OriginalEmail == email) && z.IsPnmAccount);
                if (thisUser != null) {
                    // User is already in db
                    return "This user is already registered";
                }

                // New user
                // TODO: Change loggingLevel = 20
                var user = new User() { Deleted = false, TestUser = false, IsPnmAccount = true, SessionId = "", ImageUrl = "", PartnerId = "", State = "", Lat = 0, Lng = 0, VenueId = "",
                                        LoggingLevel = 1, LogEvents = true, CheckinDatetime = new DateTime(2000, 1, 1), Created = DateTime.UtcNow, NewPoisExist = false, 
                                        SwLat = 0, SwLng = 0, NeLat = 0, NeLng = 0, VenueOutOfRange = false, Banned = false, GeolocationUpdatedDatetime = new DateTime(2000, 1, 1), CheckinAlertCount = 0, SendCheckinAlerts = true
                };

                Task.Factory.StartNew(() => EmailHelpers.SendNewUserEmail(fullName, ""));

                // PnM data
                user.Id = System.Guid.NewGuid().ToString();
                user.Birthday = birthday;
                user.First_name = firstName;
                user.Last_name = lastName;
                user.Name = fullName;
                user.Email = email;
                user.OriginalEmail = email;
                user.EmailVerified = false;
                user.EmailVerificationCode = System.Guid.NewGuid().ToString();
                user.PasswordHash = passwordHash;
                user.TimeZoneBias = getBias(localTimeStamp);


                // Other data
                if (userData.testUser != null) user.TestUser = userData.testUser;

                db.Users.Add(user);
                db.SaveChanges();
                user.Id = user.UserId.ToString();
                if (userData.testUser != null) user.Id = "UnitTest" + user.Id;

                db.SaveChanges();

                if (user.First_name == "Pin'n'Meet") user.First_name = "Pin'n'Meet?";


                //user.Email = "niko.wessman@nsd.fi";
                EmailHelpers.SendVerificationEmail(user.First_name, user.Email, "http://pinandmeet.com/verify.html?code=" + user.EmailVerificationCode);
                AddHistoryEvent(HistoryEventEnum.ACCOUNT_CREATED, user.Id);
                return "";
            }
        }

        public static string SetProfileImageUrl(dynamic userData) {
            GeneralHelpers.AddLogEvent(userData, "SetProfileImageUrl", "Start", JsonConvert.SerializeObject(userData));

            using (DB db = new DB()) {
                User user = getUser(userData, db);
                if (user == null) return "ERROR";
                
                string url = userData.imageUrl;
                user.ImageUrl = url;
                user.NonFbImage = true; // Note

                db.SaveChanges();
                return "";
            }
        }

        public static string VerifyEmail(string verificationCode) {
            GeneralHelpers.AddLogEvent("", "", "UserHelpers", "VerifyEmail", "Start", verificationCode);

            using (DB db = new DB()) {
                if (verificationCode.StartsWith("unittest") && verificationCode.EndsWith("nsd.fi")) {
                    User thisUser = db.Users.SingleOrDefault(z => z.Email == verificationCode); // Only for unit testing, email is given as parameter instead of code
                    if (thisUser == null) {
                        return "Verification code not found";
                    }
                    thisUser.EmailVerified = true;
                } else {
                    User thisUser = db.Users.SingleOrDefault(z => z.EmailVerificationCode == verificationCode);
                    if (thisUser == null) {
                        return "Verification code not found";
                    }
                    thisUser.EmailVerified = true;
                }

                db.SaveChanges();
                return "";
            }
        }

        public static ValidateSessionAndStateResult SignIn(dynamic userData) {
            string email = userData.email;
            string passwordHash = userData.passwordHash;
            GeneralHelpers.AddLogEvent(email, "", "UserHelpers", "SignIn", "Start", JsonConvert.SerializeObject(userData));

            using (DB db = new DB()) {
                User thisUser = db.Users.SingleOrDefault(z => z.Email == email && z.IsPnmAccount);
                if (thisUser == null) {
                    GeneralHelpers.AddLogEvent("", "", "UserHelpers", "SignIn", "User not found", email);
                    return new ValidateSessionAndStateResult() { error = "1", errorCode = "User not found" };
                }

                if (thisUser.PasswordHash != passwordHash) {
                    GeneralHelpers.AddLogEvent("", "", "UserHelpers", "SignIn", "Incorrect password", email);
                    return new ValidateSessionAndStateResult() { error = "2", errorCode = "Incorrect password" };
                }

                if (thisUser.EmailVerified == false) {
                    GeneralHelpers.AddLogEvent("", "", "UserHelpers", "SignIn", "Email not verified", email);
                    return new ValidateSessionAndStateResult() { error = "3", errorCode = "Email not verified" };
                }

                string uuid = userData.device.uuid;
                if(db.Users.Any(z => z.UuId == uuid && z.Banned)) {
                    GeneralHelpers.AddLogEvent("", "", "UserHelpers", "SignIn", "Banned uuid", email);
                    return new ValidateSessionAndStateResult() { error = "4", errorCode = "You are banned from this application due to inappropriate behaviour or content that is violating the Terms of use (UCLA)" };
                }

                // Device data
                thisUser.Platform = userData.device.platform;
                thisUser.Model = userData.device.model;
                thisUser.Version = userData.device.version;
                thisUser.UuId = userData.device.uuid;
                thisUser.Deleted = false;
                string localTimeStamp = userData.localTimeStamp;
                thisUser.TimeZoneBias = getBias(localTimeStamp);

                db.SaveChanges();
                AddHistoryEvent(HistoryEventEnum.SIGNED_IN, thisUser.Id);

                var data = new { sessionId = "", id = thisUser.Id, loggingLevel = thisUser.LoggingLevel }; 

                return ValidateSessionAndState(data);
            }
        }

        public static string ResendVerificationEmail(dynamic userData) {
            string email = userData.email;
            GeneralHelpers.AddLogEvent(email, "", "UserHelpers", "ResendVerificationEmail", "Start", email);

            using (DB db = new DB()) {
                User thisUser = db.Users.SingleOrDefault(z => z.Email == email & z.IsPnmAccount);
                if (thisUser != null) {
                    EmailHelpers.SendVerificationEmail(thisUser.First_name, thisUser.Email, "http://pinandmeet.com/verify.html?code=" + thisUser.EmailVerificationCode);
                } else {
                    GeneralHelpers.AddLogEvent(email, "", "UserHelpers", "ResendVerificationEmail", "Email address not found", email);
                    return "Email address not found";
                }
            }
            return "";
        }


        public static string SendNewPasswordEmail(dynamic userData) {
            string email = userData.email;
            GeneralHelpers.AddLogEvent(email, "", "UserHelpers", "SendNewPasswordEmail", "Start", email);

            using (DB db = new DB()) {
                User thisUser = db.Users.SingleOrDefault(z => z.Email == email & z.IsPnmAccount);
                if (thisUser != null) {
                    string pwd = generatePassword();
                    thisUser.PasswordHash = createMD5("sikapossu" + pwd);
                    db.SaveChanges();
                    //thisUser.Email = "niko.wessman@nsd.fi";
                    EmailHelpers.SendNewPasswordEmail(thisUser.First_name, thisUser.Email, pwd);
                }
            }
            return "";
        }

        private static string generatePassword() {
            Random rnd = new Random();
            string code = "";
            for (int i = 0; i < 3; i++) code += Convert.ToChar(rnd.Next(65, 65 + 25)).ToString(); // 26
            code += rnd.Next(100, 999).ToString();
            return code;
        }

        private static string createMD5(string input) {
            // Use input string to calculate MD5 hash
            MD5 md5 = System.Security.Cryptography.MD5.Create();
            byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(input);
            byte[] hashBytes = md5.ComputeHash(inputBytes);

            // Convert the byte array to hexadecimal string
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < hashBytes.Length; i++) {
                sb.Append(hashBytes[i].ToString("X2"));
            }
            return sb.ToString().ToLower();
        }

        public static string UpdateAccount(dynamic userData) {
            GeneralHelpers.AddLogEvent(userData, "UpdateAccount", "Start", JsonConvert.SerializeObject(userData));

            using (DB db = new DB()) {
                string id = userData.id;
                string sessionId = userData.sessionId;

                User user = getUser(userData, db);

                // Special for unit testing. No id or sessionid exist at this point
                string email = userData.email;
                if (email.StartsWith("unittest") && email.EndsWith("nsd.fi")) {
                    user = db.Users.SingleOrDefault(z => z.Email == email && z.IsPnmAccount);
                }

                if (user == null) return "User not found";

                User sameEmail = db.Users.FirstOrDefault(z => z.Id != id && z.IsPnmAccount && (z.Email == email || z.OriginalEmail == email));
                if (sameEmail != null) {
                    GeneralHelpers.AddLogEvent(userData, "UpdateAccount", "Email reserved", email);
                    return "This email address is already in use";
                }


                user.Birthday = userData.birthday;
                user.First_name = userData.firstName;
                user.Last_name = userData.lastName;
                user.Name = user.First_name + " " + user.Last_name;
                user.Email = userData.email;
                if (userData.passwordHash != "") user.PasswordHash =  userData.passwordHash;
                user.Deleted = userData.deleted;

                if (user.First_name == "Pin'n'Meet") user.First_name = "Pin'n'Meet?";

                db.SaveChanges();
                AddHistoryEvent(HistoryEventEnum.ACCOUNT_UPDATED, user.Id);

                return "";
            }
        }

        public static string RegisterPushNotification(dynamic userData) {
            GeneralHelpers.AddLogEvent(userData, "RegisterPushNotification", "Start", JsonConvert.SerializeObject(userData));

            using (DB db = new DB()) {
                User user = getUser(userData, db);
                if (user == null) return "ERROR";

                string gcm = userData.gcmRegistrationId;
                string apms = userData.apmsRegistrationId;
                user.GcmRegistrationId = gcm;
                user.ApmsRegistrationId = apms;
                db.SaveChanges();

                // Send push validation message
                GeneralHelpers.AddLogEvent(userData, "RegisterPushNotification", "Send push verification", user.Id);
                Task.Factory.StartNew(() => sendMessage("", "", user.Id, "Pin'n'Meet communication on", 1));

                return "";
            }
        }

        public static string SignOut(dynamic userData) {
            GeneralHelpers.AddLogEvent(userData, "SignOut", "Start", JsonConvert.SerializeObject(userData));
            using (DB db = new DB()) {
                User user = getUser(userData, db);
                if (user == null) return "ERROR";
                string id = userData.id;

                // Clear any possible partner. SHOULD never be
                User partner = db.Users.FirstOrDefault(z => z.PartnerId == id && !z.Deleted); // id == FB user id
                if (partner != null) {
                    GeneralHelpers.AddLogError(userData, "SignOut", "PartnerId should not be found (this user as partner)", "");

                    // Clear this user values
                    clearCheckInValues(partner);
                    db.SaveChanges();
                    sendStateChangeAlert(partner.Id, "The other user signed out");
                }
                clearCheckInValues(user);
                user.SendCheckinAlerts = false;
                db.SaveChanges();
                AddHistoryEvent(HistoryEventEnum.SIGNED_OUT, id);

                return "";
            }
        }

        // -----------------------------------------------------------------------------------
        // History
        // -----------------------------------------------------------------------------------
        public enum HistoryEventEnum {
            ACCOUNT_CREATED, SIGNED_IN, CHECK_IN, MATCH, CONTACT, NO_CONTACT, REVIEW_GIVEN, REVIEW_RECEIVED, ACCOUNT_UPDATED, SIGNED_OUT, ERROR, REPORT
        }

        public static void AddHistoryEvent(HistoryEventEnum eventHistoryType, string userId, string partnerId = "", string venueId = "", string venueName = "", 
            string venueImageUrl = "", string errorInfo = "") {

            using (DB db = new DB()) {
                User user = getUser(userId, db);
                User partner = new User();
                if (partnerId != "") partner = getUser(partnerId, db);
                
                var h = new HistoryEvent() {
                    Created = DateTime.UtcNow,
                    Id = userId,
                    PartnerId = partnerId,
                    PartnerName = partner.Name,
                    PartnerImageUrl = partner.ImageUrl,
                    VenueId = venueId,
                    VenueName = venueName,
                    VenueImageUrl = venueImageUrl,
                    LocalTimeStamp = DateTime.UtcNow.AddMinutes(-user.TimeZoneBias)
                };

                switch (eventHistoryType.ToString()) {
                    case "ACCOUNT_CREATED": h.Description = "Account created"; break;
                    case "SIGNED_IN": h.Description = string.Format("Signed in"); break;
                    case "CHECK_IN": h.Description = string.Format("Checked in at {0}", venueName); break;
                    case "MATCH": h.Description = string.Format("Match with {0} at {1}", partner.Name, venueName); break;
                    case "CONTACT": h.Description = string.Format("Met with {0} at {1}", partner.Name, venueName); break;
                    case "NO_CONTACT": h.Description = string.Format("Could not find {0} at {1}", partner.Name, venueName); break;
                    case "REVIEW_GIVEN": h.Description = string.Format("Gave review about {0}", partner.Name, venueName); break;
                    case "REVIEW_RECEIVED": h.Description = string.Format("Received a review from {0}", partner.Name, venueName); break;
                    case "PROFILE_IMAGE_UPDATED": h.Description = string.Format("Updated profile picture"); break;
                    case "ACCOUNT_UPDATED": h.Description = string.Format("Updated account information"); break;
                    case "SIGNED_OUT": h.Description = string.Format("Signed out"); break;
                    case "ERROR": h.Description = string.Format("Had an error: " + errorInfo); break;
                    case "REPORT": h.Description = string.Format("Received a report from {0}", partner.Name); break;


                    default: GeneralHelpers.AddLogError(userId, "", "UserHelpers", "AddHistoryEvent", "Invalid history event", eventHistoryType.ToString()); break;
                }
                db.HistoryEvents.Add(h);
                db.SaveChanges();
            }
        }

        private static User getUser(string id, string sessionId, DB db) {
            User user = db.Users.SingleOrDefault(z => z.Id == id && z.SessionId == sessionId); // id == FB user id
            if (user == null) GeneralHelpers.AddLogError(id, sessionId, "UserHelpers", "private getUser", "User not found", ""); // TODO: true
            return user;
        }

        // With sessionid
        private static User getUser(dynamic userData, DB db) {
            string id = userData.id;
            string sessionId = userData.sessionId;
            return getUser(id, sessionId, db);
        }

        // Without sessionid
        private static User getUser(string id, DB db) {
            User user = db.Users.SingleOrDefault(z => z.Id == id ); // id == FB user id
            if (user == null) GeneralHelpers.AddLogError(id, "", "UserHelpers", "private getUser", "User not found", ""); // TODO: true
            return user;
        }

        // Async update those users NewPoisExist = 1 who's map area views this user state
        private static void updateNewPoisExist(string id) {
            using (DB db = new DB()) {
                User user = getUser(id, db);
                decimal lat = user.Lat; //1
                decimal lng = user.Lng;
                string sql = "UPDATE Users SET NewPoisExist = 1 WHERE Deleted = 0 AND Id <> '{0}' AND neLat > {1} AND swLat < {1} AND  swLng < {2} AND neLng > {2}";
                sql = string.Format(sql, id, lat.ToString().Replace(',', '.'), lng.ToString().Replace(',', '.'));

                db.Database.ExecuteSqlCommand(sql);
            }
        }

        // -----------------------------------------------------------------------------------------------------------------
        // Testing methods
        // -----------------------------------------------------------------------------------------------------------------

        public static void RemoveTestUsers() {
            GeneralHelpers.AddLogEvent("", "", "UserHelpers", "RemoveTestUsers", "Start", "");
            using (DB db = new DB()) {
                string sql = "DELETE FROM LogEvents WHERE Id IN (SELECT Id FROM Users WHERE TestUser = 1)";
                db.Database.ExecuteSqlCommand(sql);

                sql = "DELETE FROM Reviews WHERE Id IN (SELECT Id FROM Users WHERE TestUser = 1)";
                db.Database.ExecuteSqlCommand(sql);

                sql = "DELETE FROM Messages WHERE ToId IN (SELECT Id FROM Users WHERE TestUser = 1)";
                db.Database.ExecuteSqlCommand(sql);

                sql = "DELETE FROM Messages WHERE FromId IN (SELECT Id FROM Users WHERE TestUser = 1)";
                db.Database.ExecuteSqlCommand(sql);

                sql = string.Format("DELETE FROM Users WHERE TestUser = 1");
                db.Database.ExecuteSqlCommand(sql);
            }
        }

        public static void RemoveTestAccountNwe() {
            GeneralHelpers.AddLogEvent("", "", "UserHelpers", "RemoveTestAccountNwe", "Start", "");
            using (DB db = new DB()) {
                var user = db.Users.SingleOrDefault(z => z.Email == "niko.wessman@nsd.fi" && z.IsPnmAccount);
                if (user == null) return;
                string id = user.Id;

                string sql = "DELETE FROM LogEvents WHERE Id = '" + id + "'";
                db.Database.ExecuteSqlCommand(sql);

                sql = "DELETE FROM Reviews WHERE Id = '" + id + "'";
                db.Database.ExecuteSqlCommand(sql);

                sql = "DELETE FROM Messages WHERE ToId = '" + id + "'";
                db.Database.ExecuteSqlCommand(sql);

                sql = "DELETE FROM Users WHERE Id = '" + id + "'";
                db.Database.ExecuteSqlCommand(sql);
            }
        }

        public static void ClearContacts() {
            GeneralHelpers.AddLogEvent("", "", "UserHelpers", "ClearContacts", "Start", "");
            using (DB db = new DB()) {
                string sql = "DELETE FROM Contacts";
                db.Database.ExecuteSqlCommand(sql);
            }
        }
    }
}