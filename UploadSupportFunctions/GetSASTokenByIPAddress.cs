using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Drawing;
using System.Collections.Generic;
using System.Net;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace UploadSupportFunctions
{
    public static class GetSASTokenByIPAddress
    {
        public static readonly string GEOSERVICEURL = "http://api.ipstack.com/";
        public static readonly string DEFAULTREGION = "EASTUS";
        [FunctionName("GetSASTokenByIPAddress")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {

            log.LogInformation("GetSASTokenByIPAddress HTTP trigger function processed a request.");

            string ipaddress = req.Query["ipaddress"];
            if (string.IsNullOrEmpty(ipaddress)) ipaddress = req.GetIPAddress();
            string boundedregion = await GetClosestRegion(ipaddress);

            log.LogInformation("Bounded Region=" + boundedregion);

            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(System.Environment.GetEnvironmentVariable(boundedregion+"CONNECT"));

            // Create a new access policy for the account.
            SharedAccessAccountPolicy policy = new SharedAccessAccountPolicy()
            {
                Permissions = SharedAccessAccountPermissions.Write,
                Services = SharedAccessAccountServices.Blob,
                ResourceTypes = SharedAccessAccountResourceTypes.Object,
                SharedAccessExpiryTime = DateTime.UtcNow.AddHours(24),
                Protocols = SharedAccessProtocol.HttpsOnly
            };

            string blobEndpoint = storageAccount.BlobEndpoint.AbsoluteUri;
            string sharedAccessSignature = storageAccount.GetSharedAccessSignature(policy);

            string sasConnectionString = $"BlobEndpoint={blobEndpoint};SharedAccessSignature={sharedAccessSignature}";

            
            return new OkObjectResult(sasConnectionString);
            
        }
        public static bool EastOf(PointF upperWest,PointF lowerWest,PointF p)
        {
            if (p.X > lowerWest.X + p.Y * (upperWest.X - lowerWest.X) / (upperWest.Y - lowerWest.Y)) return true;
            return false;
        }
        public static bool WestOf(PointF upperEast, PointF lowerEast, PointF p)
        {
            if (p.X < lowerEast.X + p.Y * (upperEast.X - lowerEast.X) / (upperEast.Y - lowerEast.Y)) return true;
            return false;
        }
        public static async Task<string> GetClosestRegion(string ipaddress)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(GEOSERVICEURL+ipaddress+ "?access_key=" + System.Environment.GetEnvironmentVariable("GEOSERVICEKEY"));
            request.Method = "GET";
            request.Headers.Add("Content-Type:application/json");
            request.ContentLength = 0;
           // Get the response.
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            string responseFromServer = "";
            using (Stream dataStream = response.GetResponseStream())
            {
                // Open the stream using a StreamReader for easy access.
                using (StreamReader reader = new StreamReader(dataStream))
                {
                    // Read the content.
                    responseFromServer = reader.ReadToEnd();
                }
            }
            JObject o = JObject.Parse(responseFromServer);
            string slon = (string)o["longitude"];
            string slat = (string)o["latitude"];
            PointF p = new PointF(float.Parse(slon), float.Parse(slat));
            string sregions = System.Environment.GetEnvironmentVariable("REGIONS");
            if (sregions == null) sregions = "";
            string[] regions = sregions.Split(",");
            string boundedregion = DEFAULTREGION;
            foreach (string r in regions)
            {

                string sregcords = System.Environment.GetEnvironmentVariable("REGION" + r);
                if (sregcords == null) continue;
                List<PointF> rect = new List<PointF>();
                string[] regcords = sregcords.Split(":");
                foreach (string c in regcords)
                {
                    string[] d = c.Split(",");
                    rect.Add(new PointF(float.Parse(d[0]), float.Parse(d[1])));
                }
               
                if (EastOf(rect[0], rect[1], p) && WestOf(rect[2], rect[3], p))
                {
                    boundedregion = r;
                    break;
                }

            }
            return boundedregion;
        }
       
    }
}
