using SendGrid;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Web;

// NuGet: SendGrid
// Spam testing
// Parempi: http://www.mail-tester.com
// http://www.isnotspam.com 

namespace PinAndMeetService.Helpers {
    public static class EmailHelpers {
        public static void  SendVerificationEmail(string name, string email, string targerUrl) {
            MailMessage msg = new MailMessage();

            msg.To.Add(email);
            msg.Subject = "Pin'n'Meet email verification";
            msg.Body = getVerificationEmailBody(name, email, targerUrl);

            sendEmail(msg);
        }

        public static void SendNewPasswordEmail(string name, string email, string password) {
            MailMessage msg = new MailMessage();

            msg.To.Add(email);
            msg.Subject = "New Pin'n'Meet password";
            msg.Body = getNewPasswordEmailBody(name, email, password);

            sendEmail(msg);
        }

        public static void SendViolationReportEmail(string id, string name, string targetId, string targetName, string description) {
            MailMessage msg = new MailMessage();

            msg.To.Add("niko.wessman@nsd.fi");
            msg.Subject = "Pin'n'Meet violation report";
            msg.Body = getViolationEmailBody(id, name, targetId, targetName, description);

            sendEmail(msg);
        }

        public static void SendNewUserEmail(string name, string imageLink) {
            MailMessage msg = new MailMessage();

            msg.To.Add("niko.wessman@nsd.fi");
            msg.Subject = "New Pin'n'Meet user";
            msg.Body = getNewUserBody(name, imageLink);

            sendEmail(msg);
        }

        public static void SendNewMatchEmail(string name1, string imageLink1, string name2, string imageLink2, string venueName, string venueCity) {
            MailMessage msg = new MailMessage();

            msg.To.Add("niko.wessman@nsd.fi");
            msg.Subject = "New Pin'n'Meet - Match";
            msg.Body = getNewMatchBody(name1, imageLink1, name2, imageLink2, venueName, venueCity);

            sendEmail(msg);
        }


        private static void sendEmail(MailMessage msg) {
            msg.From = new MailAddress("no-reply@pinandmeet.com");
            msg.IsBodyHtml = false;
            SmtpClient smtpClient = new SmtpClient("smtp.sendgrid.net", Convert.ToInt32(587));
            var credentials = new NetworkCredential("[Input here]", "[Input here]");
            smtpClient.Credentials = credentials;

            smtpClient.Send(msg);
        }

        private static string getVerificationEmailBody(string name, string email, string targetUrl) {
            string template = @"
Hi {name},

Help us secure your Pin'n'Meet account by verifying your email address {email} using the link below. 

{targetUrl}
 
You’re receiving this email because you recently created a new Pin'n'Meet account or added a new email address. If this wasn’t you, please ignore this email.

Regads,
Pin’n’Meet security team
                ";

            template = template.Replace("{name}", name);
            template = template.Replace("{email}", email);
            template = template.Replace("{targetUrl}", targetUrl);
            return template;
        }

        private static string getViolationEmailBody(string id, string name, string targetId, string targetName, string description) {
            string template = @"
Violating user: {targetName} {targetId}

Reporting user: {name} {id} 

{description}
                ";

            template = template.Replace("{name}", name);
            template = template.Replace("{id}", id);
            template = template.Replace("{targetName}", targetName);
            template = template.Replace("{targetId}", targetId);
            template = template.Replace("{description}", description);
            return template;
        }

        private static string getNewUserBody(string name, string imageLink) {
            string template = @"
New user name: {targetName} 
Image: {targetImage} 
                ";

            template = template.Replace("{targetName}", name);
            template = template.Replace("{targetImage}", imageLink);
            return template;
        }

        private static string getNewMatchBody(string name1, string imageLink1, string name2, string imageLink2, string venueName, string venueCity) {
            string template = @"
User1: {name1} {imageLink1} 
User2: {name2} {imageLink2}
Venue: {venueName} in {venueCity}
                ";

            template = template.Replace("{name1}", name1);
            template = template.Replace("{name2}", name2);
            template = template.Replace("{imageLink1}", imageLink1);
            template = template.Replace("{imageLink2}", imageLink2);
            template = template.Replace("{venueName}", venueName);
            template = template.Replace("{venueCity}", venueCity);
            return template;
        }


        private static string getNewPasswordEmailBody(string name, string email, string password) {
            string template = @"
Hi {name},

Your new Pin'n'Meet password is {password} 
 
We highly recommend you to change the password the next time you sign in Pin'n'Meet. 

You’re receiving this email because you requested a new password for your Pin'n'Meet account. If this wasn’t you, please ignore this email.

Regards, 
Pin’n’Meet security team
                ";

            template = template.Replace("{name}", name);
            template = template.Replace("{email}", email);
            template = template.Replace("{password}", password);
            return template;
        }
    }
}

