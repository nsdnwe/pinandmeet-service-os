using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Web;
using System.Web.Hosting;

// To test APMS locally on own iMAC, change IS_RUNNING_LOCALLY to true. NO certificate change needed on Xcode or iMac

namespace PinAndMeetService.Helpers {
    public static class PushHelpers {

        private const bool IS_RUNNING_LOCALLY = false;

        // This is not needed, use .p12 file only. So no need to upload cert to Azure
        private const string P12_PRODUCTION_FILE_NAME = "aps_production.p12"; // [Input here] meaning put in Azure
        private const string P12_DEVELOPMENT_FILE_NAME = "aps_development.p12"; // [Input here] meaning put in Azure
        private const string P12_PASSWORD = "[Input here]";

        // ------------------------------------------------------------------------------------------
        // Android
        // ------------------------------------------------------------------------------------------

        public static void SendGcmNotification(string registrationID, string message, string title) {
            GeneralHelpers.AddLogEvent(registrationID, "", "PushHelpers", "SendGcmNotification", "Start", title + " " + message);

            // GCM ProjectID 
            string senderId = "PinAndMeetAPI"; 

            // GCM Api key                                                                                                     
            var applicationID = "[Input here]"; 

            WebRequest tRequest;
            tRequest = WebRequest.Create("https://android.googleapis.com/gcm/send");
            tRequest.Method = "post";
            tRequest.ContentType = "application/x-www-form-urlencoded;charset=UTF-8";
            tRequest.Headers.Add(string.Format("Authorization: key={0}", applicationID));
            tRequest.Headers.Add(string.Format("Sender: id={0}", senderId));

            //Data post to server                                                                                                                                         
            string postData = string.Format("data.message={0}&data.time={1}&registration_id={2}&data.title={3}", HttpUtility.UrlEncode(message), System.DateTime.Now.ToString(), registrationID,
                HttpUtility.UrlEncode(title));


            Byte[] byteArray = Encoding.UTF8.GetBytes(postData);
            tRequest.ContentLength = byteArray.Length;
            Stream dataStream = tRequest.GetRequestStream();
            dataStream.Write(byteArray, 0, byteArray.Length);
            dataStream.Close();
            WebResponse tResponse = tRequest.GetResponse();
            dataStream = tResponse.GetResponseStream();
            StreamReader tReader = new StreamReader(dataStream);
            String sResponseFromServer = tReader.ReadToEnd();   //Get response from GCM server.  
            tReader.Close();
            dataStream.Close();
            tResponse.Close();
            GeneralHelpers.AddLogEvent(registrationID, "", "PushHelpers", "SendGcmNotification", "Completed", "");

            //return sResponseFromServer;
        }

        // ------------------------------------------------------------------------------------------
        // IOS
        // ------------------------------------------------------------------------------------------



        public static SslStream sslStream;
        public static X509Certificate2Collection certs;

        public static void SendApnsNotification(string registrationID, string name, string message, int messageId) {

            GeneralHelpers.AddLogEvent(registrationID, name, "PushHelpers", "SendApnsNotification", "Start", messageId.ToString() + " " + message);

            // Note first param is short message (cat at 30 chars)
            pushMessage(message, registrationID, 0, "name=" + HttpUtility.UrlEncode(name) + ";id=" + messageId.ToString());

            GeneralHelpers.AddLogEvent(registrationID, name, "PushHelpers", "SendApnsNotification", "Completed", "");
        }

        private static bool pushMessage(string shortMessage, string DeviceToken, int Badge, string Custom_Field) {

            if (shortMessage.Length > 30) shortMessage = shortMessage.Substring(0, 30) + "...";

            if (IS_RUNNING_LOCALLY) connectToDevelopmentAPNS(); else connectToProductionAPNS();
            List<string> Key_Value_Custom_Field = new List<string>();
            String cToken = DeviceToken;
            int iBadge = Badge;

            // Ready to create the push notification
            byte[] buf = new byte[256];
            MemoryStream ms = new MemoryStream();
            BinaryWriter bw = new BinaryWriter(ms);
            bw.Write(new byte[] { 0, 0, 32 });

            byte[] deviceToken = HexToData(cToken);
            bw.Write(deviceToken);

            bw.Write((byte)0);

            // Create the APNS payload - new.caf is an audio file saved in the application bundle on the device
            string msg = "";
            msg = "{\"aps\":{\"alert\":\"" + shortMessage + "\",\"badge\":\"" + iBadge.ToString() + "\",\"sound\":\"noti.aiff\"}";
            //msg = "{\"aps\":{\"alert\":\"" + HttpUtility.UrlEncode(cAlert) + "\",\"badge\":\"" + iBadge.ToString() + "\",\"sound\":\"noti.aiff\"}";


            String PayloadMess = "";
            if (string.IsNullOrWhiteSpace(Custom_Field) == false) {
                List<string> list_Custom_Field = Custom_Field.Split(';').ToList();

                if (list_Custom_Field.Count > 0) {
                    for (int indx = 0; indx < list_Custom_Field.Count; indx++) {
                        Key_Value_Custom_Field = list_Custom_Field[indx].Split('=').ToList();
                        if (Key_Value_Custom_Field.Count > 1) {
                            if (PayloadMess != "") PayloadMess += ", ";
                            PayloadMess += "\"" + Key_Value_Custom_Field[0].ToString() + "\":\"" + Key_Value_Custom_Field[1].ToString() + "\"";
                        }
                    }
                }
            }

            if (PayloadMess != "") {
                msg += ", " + PayloadMess;
            }
            msg += "}";


            // Write the data out to the stream
            byte[] apnMessageLength = BitConverter.GetBytes((Int16)Encoding.UTF8.GetBytes(msg).Length);
            bw.Write(apnMessageLength[0]);
            bw.Write(Encoding.UTF8.GetBytes(msg), 0, Encoding.UTF8.GetBytes(msg).Length);
            bw.Flush();

            if (sslStream != null) {
                sslStream.Write(ms.ToArray());
                return true;
            }

            return false;
        }

        // This set sslStream object only !
        // NOTE: 
        //      - Azure WebSite must be Basic
        //      - Rename .p12 -> .pfx
        //      - Upload certificate
        //      - Add App setting: WEBSITE_LOAD_CERTIFICATES, 
        // See: https://azure.microsoft.com/en-us/blog/using-certificates-in-azure-websites-applications/
        //
        private static bool connectToProductionAPNS() {
            GeneralHelpers.AddLogEvent("", "", "PushHelpers", "connectToAPNS", "Start", "");

            X509Store certStore = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            certStore.Open(OpenFlags.ReadOnly);
            X509Certificate2Collection certCollection = certStore.Certificates.Find(X509FindType.FindByThumbprint, "[Input here]", false);

            // Apple development server address
            string apsHost;
            if (IS_RUNNING_LOCALLY)
                apsHost = "gateway.sandbox.push.apple.com";
            else
                apsHost = "gateway.push.apple.com";

            GeneralHelpers.AddLogEvent("", "", "PushHelpers", "connectToAPNS", "Start", "1");

            X509Certificate2 cert = certCollection[0];
            GeneralHelpers.AddLogEvent("", "", "PushHelpers", "connectToAPNS", "Start", cert.FriendlyName);
            

            // Create a TCP socket connection to the Apple server on port 2195
            TcpClient tcpClient = new TcpClient(apsHost, 2195);

            // Create a new SSL stream over the connection
            sslStream = new SslStream(tcpClient.GetStream());
            GeneralHelpers.AddLogEvent("", "", "PushHelpers", "connectToAPNS", "Start", "2");

            // Authenticate using the Apple cert
            sslStream.AuthenticateAsClient(apsHost, certCollection, SslProtocols.Default, false);
            GeneralHelpers.AddLogEvent("", "", "PushHelpers", "connectToAPNS", "Start", "3");

            return true;
        }


        private static bool connectToDevelopmentAPNS() {
            GeneralHelpers.AddLogEvent("", "", "PushHelpers", "connectToAPNS", "1", "");

            certs = new X509Certificate2Collection();
            X509Certificate2 xcert;

            //if (isRunningLocally) {

            var pathAndFile = HostingEnvironment.MapPath("~/App_Data/Indicator/" + P12_PRODUCTION_FILE_NAME);
            if (IS_RUNNING_LOCALLY) pathAndFile = HostingEnvironment.MapPath("~/App_Data/Indicator/" + P12_DEVELOPMENT_FILE_NAME);

            xcert = new X509Certificate2();
            xcert.Import(pathAndFile, P12_PASSWORD, X509KeyStorageFlags.UserKeySet); // PROBLEM IS HERE

            // Add the Apple cert to our collection
            certs.Add(xcert);

            // Apple development server address
            string apsHost;

            //if (P12_PRODUCTION_FILE_NAME.ToString().ToLower().Contains("production"))

            if (IS_RUNNING_LOCALLY)
                apsHost = "gateway.sandbox.push.apple.com";
            else
                apsHost = "gateway.push.apple.com";


            GeneralHelpers.AddLogEvent("", "", "PushHelpers", "connectToAPNS", "Start", apsHost);

            // Create a TCP socket connection to the Apple server on port 2195
            TcpClient tcpClient = new TcpClient(apsHost, 2195);

            // Create a new SSL stream over the connection
            sslStream = new SslStream(tcpClient.GetStream());

            // Authenticate using the Apple cert
            sslStream.AuthenticateAsClient(apsHost, certs, SslProtocols.Default, false);

            //PushMessage();

            return true;
        }

        private static byte[] HexToData(string hexString) {
            if (hexString == null)
                return null;

            if (hexString.Length % 2 == 1)
                hexString = '0' + hexString; // Up to you whether to pad the first or last byte

            byte[] data = new byte[hexString.Length / 2];

            for (int i = 0; i < data.Length; i++)
                data[i] = Convert.ToByte(hexString.Substring(i * 2, 2), 16);

            return data;
        }

        
    }
}