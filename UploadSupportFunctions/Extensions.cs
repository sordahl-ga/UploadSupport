using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNetCore.Http;
namespace UploadSupportFunctions
{
    public static class Extensions
    {
        public static string GetIPAddress(this HttpRequest Request)
        {
            if (Request.Headers.Keys.Contains("CF-CONNECTING-IP")) return Request.Headers["CF-CONNECTING-IP"].ToString();

            if (Request.Headers.Keys.Contains("HTTP_X_FORWARDED_FOR"))
            {
                string ipAddress = Request.Headers["HTTP_X_FORWARDED_FOR"];

                if (!string.IsNullOrEmpty(ipAddress))
                {
                    string[] addresses = ipAddress.Split(',');
                    if (addresses.Length != 0)
                    {
                        return addresses[0];
                    }
                }
            }

            return Request.Host.ToString();
        }
    }
}
