using PinAndMeetService.Helpers;
using PinAndMeetService.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web;
using System.Web.Http;
using System.Web.Http.Cors;

namespace PinAndMeetService.Controllers {
    public class ValuesController : ApiController {

        [Route("testPing")]
        [HttpGet]
        public string testPing() {
            GeneralHelpers.AddLogEvent("", "", "ValuesController", "testPing", "Start", "");
            return "Pong";
        }

        [Route("testError")]
        [HttpGet]
        public string testError() {
            throw new Exception("Test Exception " + DateTime.UtcNow.ToLongTimeString());
        }

        [Route("addLogEvent")]
        [HttpPost]
        public void addLogEvent([FromBody] dynamic logEvent) {
            GeneralHelpers.AddLog(logEvent, "CLIENT", (int)GeneralHelpers.EventType.EVENT);
        }

        [Route("addLogEventBatch")]
        [HttpPost]
        public void addLogEventBatch([FromBody] dynamic logEventBatch) {
            GeneralHelpers.AddLogBatch(logEventBatch);
        }

        [Route("addLogError")]
        [HttpPost]
        public void addLogError([FromBody] dynamic logEvent) {
            GeneralHelpers.AddLog(logEvent, "CLIENT", (int)GeneralHelpers.EventType.ERROR);
        }

        [Route("clearAllLogEvents")]
        [HttpPost]
        public void clearAllLogEvents() {
            GeneralHelpers.ClearAllLogEvents();
        }

        [Route("connectionCheck")]
        [HttpPost]
        public string connectionCheck() {
            GeneralHelpers.AddLogEvent("", "", "ValuesController", "connectionCheck", "Start", "");
            return "OK";
        }

        [Route("updateFbUserData")]
        [HttpPost]
        public string updateFbUserData([FromBody] dynamic userData) {
            return UserHelpers.UpdateFbUserData(userData);
        }

        [Route("validateSessionAndState")]
        [HttpPost]
        public ValidateSessionAndStateResult validateSessionAndState([FromBody] dynamic userSession) {
            var result = UserHelpers.ValidateSessionAndState(userSession);
            return result;
        }

        [Route("getPois")]
        [HttpPost]
        public IEnumerable<PoiResult> getPois([FromBody] dynamic userData) {
            return PoiHelpers.GetPois(userData);
        }

        [Route("setStateForTesting")]
        [HttpPost]
        public void setStateForTesting([FromBody] dynamic userData) {
            UserHelpers.SetStateForTesting(userData);
        }

        [Route("checkIn")]
        [HttpPost]
        public CheckInResult checkIn([FromBody] dynamic userData) {
            return UserHelpers.CheckIn(userData);
        }

        [Route("extendCheckIn")]
        [HttpPost]
        public void extendCheckIn([FromBody] dynamic userData) {
            UserHelpers.ExtendCheckIn(userData);
        }

        [Route("checkOut")]
        [HttpPost]
        public string checkOut([FromBody] dynamic userData) {
            return UserHelpers.CheckOut(userData);
        }

        [Route("getPartner")]
        [HttpPost]
        public PartnerResult getPartner([FromBody] dynamic userData) {
            return UserHelpers.GetPartner(userData);
        }

        [Route("getState")]
        [HttpPost]
        public StateResult getState([FromBody] dynamic userData) {
            return UserHelpers.GetState(userData);
        }

        [Route("sendMessage")]
        [HttpPost]
        public string sendMessage([FromBody] dynamic messageData) {
            return UserHelpers.SendMessage(messageData);
        }

        [Route("getSingleChatMessages")]
        [HttpPost]
        public ChatMessageResult getSingleChatMessages([FromBody] dynamic messageData) {
            return UserHelpers.GetSingleChatMessages(messageData);
        }

        [Route("getChatMessages")]
        [HttpPost]
        public List<ChatMessageResult> getChatMessages([FromBody] dynamic messageData) {
            return UserHelpers.GetChatMessages(messageData);
        }

        [Route("partnerFound")]
        [HttpPost]
        public string partnerFound([FromBody] dynamic userData) {
            return UserHelpers.PartnerFound(userData);
        }

        [Route("partnerNotFound")]
        [HttpPost]
        public string partnerNotFound([FromBody] dynamic userData) {
            return UserHelpers.PartnerNotFound(userData);
        }

        [Route("setVenueOutOfRange")]
        [HttpPost]
        public string setVenueOutOfRange([FromBody] dynamic userData) {
            return UserHelpers.SetVenueOutOfRange(userData);
        }

        [Route("saveReview")]
        [HttpPost]
        public string saveReview([FromBody] dynamic userData) {
            return UserHelpers.SaveReview(userData);
        }

        [Route("getReviews")]
        [HttpPost]
        public List<Review> getReviews([FromBody] dynamic userData) {
            return UserHelpers.GetReviews(userData);
        }

        [Route("signUp")]
        [HttpPost]
        public string signUp([FromBody] dynamic userData) {
            return UserHelpers.SignUp(userData);
        }

        [Route("uploadImage")]
        [HttpPost]
        public string UploadPhoto() { // Note: web.config requires maxRequestLength ="1048576" executionTimeout="3600"
            GeneralHelpers.AddLogEvent("", "", "ValuesController", "UploadPhoto", "Start", "");
            var httpPostedFile = HttpContext.Current.Request;
            HttpPostedFileBase filebase = new HttpPostedFileWrapper(HttpContext.Current.Request.Files[0]);
            string fileName = ImageHelpers.UploadAndProcessAvatarImage(filebase);
            string fullUrl = ImageHelpers.GetAvatarFullUrl(fileName);
            GeneralHelpers.AddLogEvent("", "", "ValuesController", "UploadPhoto", "Completed", fullUrl);

            return fullUrl;
        }

        [Route("setProfileImageUrl")]
        [HttpPost]
        public string setProfileImageUrl([FromBody] dynamic userData) {
            return UserHelpers.SetProfileImageUrl(userData);
        }

        [Route("verifyEmail")]
        [HttpGet] // GET
        public string verifyEmail(string code) {
            return UserHelpers.VerifyEmail(code);
        }

        [Route("verifyEmail")]
        [HttpPost] // Post - For unit testing only
        public string verifyEmail([FromBody] dynamic userData) {
            string code = userData.code;
            return UserHelpers.VerifyEmail(code);
        }


        [Route("signIn")]
        [HttpPost]
        public ValidateSessionAndStateResult signIn([FromBody] dynamic userData) {
            return UserHelpers.SignIn(userData);
        }

        [Route("resendVerificationEmail")]
        [HttpPost]
        public string resendVerificationEmail([FromBody] dynamic userData) {
            return UserHelpers.ResendVerificationEmail(userData);
        }

        [Route("sendNewPasswordEmail")]
        [HttpPost]
        public string sendNewPasswordEmail([FromBody] dynamic userData) {
            return UserHelpers.SendNewPasswordEmail(userData);
        }

        [Route("updateAccount")]
        [HttpPost]
        public string updateAccount([FromBody] dynamic userData) {
            return UserHelpers.UpdateAccount(userData);
        }

        [Route("registerPushNotification")]
        [HttpPost]
        public string registerPushNotification([FromBody] dynamic userData) {
            return UserHelpers.RegisterPushNotification(userData);
        }

        [Route("signOut")]
        [HttpPost]
        public string signOut([FromBody] dynamic userData) {
            return UserHelpers.SignOut(userData);
        }

        [Route("resetUser")]
        [HttpPost]
        public string resetUser([FromBody] dynamic userData) {
            return UserHelpers.ResetUser(userData);
        }

        [Route("reportUser")]
        [HttpPost]
        public string reportUser([FromBody] dynamic userData) {
            return UserHelpers.ReportUser(userData);
        }

        [Route("removeTestUsers")]
        [HttpPost]
        public void removeTestUsers() {
            UserHelpers.RemoveTestUsers();
        }

        [Route("clearContacts")]
        [HttpGet]
        public string clearContacts() {
            UserHelpers.ClearContacts();
            return "Cleared";
        }
    }
}
