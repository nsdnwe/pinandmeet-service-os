using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using PinAndMeetService.Models;
using RestSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web;

namespace PinAndMeetService.Helpers {
    public static class GeneralHelpers {
        public enum EventType { EVENT, ERROR };
        public enum LoggingLevels {
            NO = 0,                     // Only errors
            ONLY_SERVICE_EVENTS = 1,    // Errors and service events (no events from client)
            ALL_NO_MAP_INSTANT = 10,    // Errors and events (not map events)
            ALL_NO_MAP_BATCH = 20,      // Errors and events (not map events).  Send as a batch
            ALL_INSTANT = 110,          // Errors and events 
            ALL_BATCH = 120             // Errors and events                    Send as a batch
        };


        // Call this from values controller
        public static void AddLog(dynamic logEvent, string stage, int eventType) {
            string id = logEvent.id;
            string sessionId = logEvent.sessionId;
            string module = logEvent.module;
            string method = logEvent.method;
            string eventName = logEvent.eventName;
            string parameters = logEvent.parameters;
            //bool logEvents = logEvent.logEvents;
            int loggingLevel = logEvent.loggingLevel;
            DateTime localTimeStamp = logEvent.localTimeStamp;
            addLog(id, sessionId, module, method, eventName, parameters, loggingLevel, localTimeStamp, stage, eventType);
        }

        // Call this from values controller
        public static void AddLogBatch(dynamic logEventBatch) {
            foreach (dynamic logEvent in logEventBatch) {
                string id = logEvent.id;
                string sessionId = logEvent.sessionId;
                string module = logEvent.module;
                string method = logEvent.method;
                string eventName = logEvent.eventName;
                string parameters = logEvent.parameters;
                //bool logEvents = logEvent.logEvents;
                int loggingLevel = logEvent.loggingLevel;
                DateTime localTimeStamp = logEvent.localTimeStamp;
                addLog(id, sessionId, module, method, eventName, parameters, loggingLevel, localTimeStamp, "CLIENT", (int)GeneralHelpers.EventType.EVENT);
            }
        }

        private static void addLog(string id, string sessionId, string module, string method, string eventName, string parameters, int loggingLevel, DateTime? localTimeStamp, string stage, int eventType) {
            if (loggingLevel == 0 && eventType == (int)EventType.EVENT) return;
            if (loggingLevel == 1 && eventType == (int)EventType.EVENT && stage == "CLIENT") return; // Log service events but not client

            EventType et = (EventType)eventType;

            using (DB db = new DB()) {

                DateTime timeStamp = DateTime.UtcNow;

                // If called from client
                if (localTimeStamp != null && id != "") {
                    User user = db.Users.SingleOrDefault(z => z.Id == id);
                    if (user != null)  timeStamp = ((DateTime)localTimeStamp).AddMinutes(user.TimeZoneBias);
                }

                string type = et.ToString();
                if (type == "EVENT") type = "Event";

                var logEvent = new LogEvent() {
                    Id = id,
                    SessionId = sessionId,
                    Stage = stage,
                    Module = module,
                    Method = method,
                    Type = type,
                    TypeId = (int)eventType,
                    EventName = eventName,
                    Parameters = parameters,
                    Created = DateTime.UtcNow,
                    LocalTimeStamp = timeStamp
                };

                db.LogEvents.Add(logEvent);
                db.SaveChanges();
            }
        }

        // Call those from this service solution methods
        public static void AddLogEvent(string id, string sessionId, string module, string method, string eventName, string parameters, int loggingLevel = 110) {
            addLog(id, sessionId, module, method, eventName, parameters, loggingLevel, null, "SERVICE", (int)EventType.EVENT);
        }

        public static void AddLogEvent(dynamic userData, string method, string eventName, string parameters) {
            string id = userData.id;
            string sessionId = userData.sessionId;
            int loggingLevel = 0;
            if (userData.loggingLevel == null) loggingLevel = (int)LoggingLevels.ALL_INSTANT; else loggingLevel = userData.loggingLevel;
            
            addLog(id, sessionId, "UserHelpers", method, eventName, parameters, loggingLevel, null, "SERVICE", (int)EventType.EVENT);
        }

        public static void AddLogEvent(dynamic userData, string method, string eventName) {
            AddLogEvent(userData, method, eventName, "");
        }

        public static void AddLogError(string id, string sessionId, string module, string method, string eventName, string parameters) {
            addLog(id, sessionId, module, method, eventName, parameters, 110, null, "SERVICE", (int)EventType.ERROR);
        }

        public static void AddLogError(dynamic userData, string method, string eventName, string parameters) {
            string id = userData.id;
            string sessionId = userData.sessionId;
            int loggingLevel = 0;
            if (userData.loggingLevel == null) loggingLevel = (int)LoggingLevels.ALL_INSTANT; else loggingLevel = userData.loggingLevel;

            addLog(id, sessionId, "UserHelpers", method, eventName, parameters, loggingLevel, null, "SERVICE", (int)EventType.ERROR);
        }

        public static void ClearAllLogEvents() {
            using (DB db = new DB()) {
                db.Database.ExecuteSqlCommand("DELETE FROM LogEvents");
            }
        }

        // Get decimal value fom dynamic string (can't do directly)
        public static decimal GetDecimalValueFromDynamic(string dynamicString, string name) {
            // Find string and split to right spot: "{\r\n  \"swLat\": 60.636085562313447,\r\n  \"swLng\": 24.879795440965722,...
            name = "\"" + name + "\":"; 
            int pos = dynamicString.IndexOf(name);
            if (pos == -1) return 0;
            int start = pos + name.Length;
            string rest = dynamicString.Substring(start, dynamicString.Length - start);
            string[] parts = rest.Split(',');
            string result = parts[0].Trim();

            // Change . -> , (if needed)
            string separator = 1.1.ToString().Substring(1,1);
            string nonSeparator = ".";
            if (separator == ".") nonSeparator = ",";
            return decimal.Parse(result.Replace(nonSeparator, separator));
        }
    }
}





//var client = new RestClient("http://restlog.azurewebsites.net/");
//var request = new RestRequest("api", Method.POST);
//var timeStamp = DateTime.Now.ToString("hh:mm:ss.ff");

//request.AddParameter("id", "pnm");
//request.AddParameter("timeStamp", timeStamp);
//request.AddParameter("value", " SERVICE: " + value);

//try {
//    client.ExecuteAsync(request, response => { });
//} catch {}
