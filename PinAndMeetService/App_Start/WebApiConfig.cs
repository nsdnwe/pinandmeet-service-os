using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using System.Web.Http;  
using System.Web.Http.Cors;
using System.Web.Http.ExceptionHandling;

namespace PinAndMeetService
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            // Web API configuration and services
            var corsAttr = new EnableCorsAttribute("*", "*", "*");
            config.EnableCors(corsAttr);

            // Global error logging
            config.Services.Add(typeof(IExceptionLogger), new ElmahExceptionLogger());

            // Web API routes
            config.MapHttpAttributeRoutes();

            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );
        }

        public class ElmahExceptionLogger : ExceptionLogger {
            public override void Log(ExceptionLoggerContext context) {
                string connString = ConfigurationManager.ConnectionStrings["DB"].ConnectionString;
                string clientIp = System.Web.HttpContext.Current.Request.ServerVariables["REMOTE_ADDR"];
                string dateStamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.ff");
                string value = context.Exception.Message.Replace("'", "´");

                string query = string.Format("INSERT INTO [LogEvents] ([Id],[Stage],[Type],[TypeId],[Module],[EventName],[Created],[LocalTimeStamp]) VALUES ('{0}', 'Service', 'UNHANDLED_ERROR', 30 ,'ExceptionLogger', '{1}', '{2}', '{2}')",
                    clientIp, value, dateStamp);

                using (SqlConnection conn = new SqlConnection(connString)) {
                    conn.Open();

                    using (SqlCommand command = new SqlCommand(query, conn)) {
                        command.ExecuteNonQuery();
                    }
                }
            }
        }
    }
}
