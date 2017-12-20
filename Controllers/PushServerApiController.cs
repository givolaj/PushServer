using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;

namespace PushServer.Controllers
{
    [AllowAnonymous]
    [Authorize]
    public class PushServerApiController : ApiController
    {
        [Route("test")]
        [HttpGet]
        public IHttpActionResult test()
        {
            return Ok("service is working");
        }

        [Route("getCurrentMillis")]
        [HttpGet]
        public IHttpActionResult getCurrentMillis()
        {
            TimeSpan t = DateTime.UtcNow - new DateTime(1970, 1, 1);
            return Ok((long)t.TotalMilliseconds);
        }

        public static readonly string API_KEY = "abcd1234";
        public class PushRequest
        {
            public string apiKey { get; set; }
            public string senderId { get; set; }
            public string serverKey { get; set; }
            public string deviceToken { get; set; }
            public string title { get; set; }
            public string message { get; set; }
            public string data { get; set; }
            public string categoryIdentifier { get; set; }
            public string delay { get; set; }
            public string badge { get; set; }
        }

        [Route("push")]
        [HttpPost]
        public IHttpActionResult push([FromBody] PushRequest request)
        {
            if (request.apiKey != API_KEY)
                return BadRequest("bad api key");

            double delay = 0;
            try
            {
                delay = double.Parse(request.delay);
            }
            catch (Exception) { }

            if (delay == 0)
            {
                string[] tokens = request.deviceToken.Split(',');
                if (request.categoryIdentifier == null || request.categoryIdentifier == "")
                    request.categoryIdentifier = "Chat";
                WebRequest tRequest = WebRequest.Create("https://fcm.googleapis.com/fcm/send");
                tRequest.Method = "post";
                tRequest.ContentType = "application/json";
                dynamic objNotification;
                if (string.IsNullOrEmpty(request.title) && string.IsNullOrEmpty(request.message))
                {
                    objNotification = new
                    {
                        registration_ids = tokens,
                        content_available = true,
                        categoryIdentifier = request.categoryIdentifier,
                        priority = "high",
                        data = new
                        {
                            data = request.data
                        }
                    };
                }
                else
                {
                    objNotification = new
                    {
                        registration_ids = tokens,
                        priority = "high",
                        notification = new
                        {
                            body = request.message,
                            title = request.title,
                            sound = "default",
                            categoryIdentifier = request.categoryIdentifier,
                            badge = request.badge
                        },

                        data = new
                        {
                            data = request.data
                        }
                    };
                }
                string jsonNotificationFormat = Newtonsoft.Json.JsonConvert.SerializeObject(objNotification);

                Byte[] byteArray = Encoding.UTF8.GetBytes(jsonNotificationFormat);
                tRequest.Headers.Add(string.Format("Authorization: key={0}", request.serverKey));
                tRequest.Headers.Add(string.Format("Sender: id={0}", request.senderId));
                tRequest.ContentLength = byteArray.Length;
                tRequest.ContentType = "application/json";
                using (Stream dataStream = tRequest.GetRequestStream())
                {
                    dataStream.Write(byteArray, 0, byteArray.Length);

                    using (WebResponse tResponse = tRequest.GetResponse())
                    {
                        using (Stream dataStreamResponse = tResponse.GetResponseStream())
                        {
                            using (StreamReader tReader = new StreamReader(dataStreamResponse))
                            {
                                String responseFromFirebaseServer = tReader.ReadToEnd();

                                FCMResponse response = Newtonsoft.Json.JsonConvert.DeserializeObject<FCMResponse>(responseFromFirebaseServer);
                                if (response.success == 1)
                                {
                                    return Ok("success");
                                }
                                else if (response.failure == 1)
                                {
                                    return Ok(string.Format("Error sent from FCM server, after sending request : {0} , for following device info: {1}", responseFromFirebaseServer, jsonNotificationFormat));
                                }
                            }
                        }
                    }
                }
                return Ok();
            }

            else
            {
                CancellationTokenSource source = new CancellationTokenSource();
                var t = Task.Run(async delegate
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(delay), source.Token);

                    string[] tokens = request.deviceToken.Split(',');
                    if (request.categoryIdentifier == null || request.categoryIdentifier == "")
                        request.categoryIdentifier = "Chat";
                    WebRequest tRequest = WebRequest.Create("https://fcm.googleapis.com/fcm/send");
                    tRequest.Method = "post";
                    tRequest.ContentType = "application/json";
                    dynamic objNotification;
                    if (string.IsNullOrEmpty(request.title) && string.IsNullOrEmpty(request.message))
                    {
                        objNotification = new
                        {
                            registration_ids = tokens,
                            content_available = true,
                            categoryIdentifier = request.categoryIdentifier,
                            priority = "high",
                            data = new
                            {
                                data = request.data
                            }
                        };
                    }
                    else
                    {
                        objNotification = new
                        {
                            registration_ids = tokens,
                            priority = "high",
                            notification = new
                            {
                                body = request.message,
                                title = request.title,
                                sound = "default",
                                categoryIdentifier = request.categoryIdentifier,
                                badge = request.badge
                            },

                            data = new
                            {
                                data = request.data
                            }
                        };
                    }
                    string jsonNotificationFormat = Newtonsoft.Json.JsonConvert.SerializeObject(objNotification);

                    Byte[] byteArray = Encoding.UTF8.GetBytes(jsonNotificationFormat);
                    tRequest.Headers.Add(string.Format("Authorization: key={0}", request.serverKey));
                    tRequest.Headers.Add(string.Format("Sender: id={0}", request.senderId));
                    tRequest.ContentLength = byteArray.Length;
                    tRequest.ContentType = "application/json";
                    using (Stream dataStream = tRequest.GetRequestStream())
                    {
                        dataStream.Write(byteArray, 0, byteArray.Length);
                    }
                });                
                return Ok();
            }
        }

        public class FCMResponse
        {
            public long multicast_id { get; set; }
            public int success { get; set; }
            public int failure { get; set; }
            public int canonical_ids { get; set; }
            public List<FCMResult> results { get; set; }
        }
        public class FCMResult
        {
            public string message_id { get; set; }
        }
    }
}
