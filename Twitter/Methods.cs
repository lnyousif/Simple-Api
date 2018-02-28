using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using System.Web;
using System.Net;
using System.IO;
using System.Configuration;

namespace APIs.Twitter
{
    public static class Methods
    {

        private const string source = "your app name";
        private const string twitterClientVersion = "your app version";
        private const string twitterClientUrl = "your app link";
        private const string TwitterBaseUrlFormat = "http://twitter.com/{0}/{1}.{2}";


        private static string _consumerKey = "";
        private static string _consumerSecret = "";

        public static string ConsumerKey
        {
            get
            {
                if (_consumerKey.Length == 0)
                {
                    _consumerKey = ConfigurationManager.AppSettings["twitterConsumerKey"];
                }
                return _consumerKey;
            }
            set { _consumerKey = value; }
        }

        public static string ConsumerSecret
        {
            get
            {
                if (_consumerSecret.Length == 0)
                {
                    _consumerSecret = ConfigurationManager.AppSettings["twitterConsumerSecret"];
                }
                return _consumerSecret;
            }
            set { _consumerSecret = value; }
        }


        public static Request GetRequest(string uri, string UN, string PWD)
        {
            Request r = new Request(uri);
            r.Headers.Add("X-Twitter-Client", source);
            r.Headers.Add("X-Twitter-Version", twitterClientVersion);
            r.Headers.Add("X-Twitter-URL", twitterClientUrl);
            r.Parameters.Add("source", source);
            r.Credentials = new NetworkCredential(UN, PWD);
            return r;
        }


        public static Request GetoAuthRequest(string uri, string token, string secret)
        {
            Request ro = new Request(uri);
            ro.Headers.Add("X-Twitter-Client", source);
            ro.Headers.Add("X-Twitter-Version", twitterClientVersion);
            ro.Headers.Add("X-Twitter-URL", twitterClientUrl);
            ro.Parameters.Add("source", source);

            ro.oAuth_ConsumerKey = ConsumerKey;
            ro.oAuth_ConsumerSecret = ConsumerSecret; 
            ro.oAuth_Token = token;
            ro.oAuth_Secret = secret;

            return ro;
        }

        public static  void UpdateStatus_oAuth(string token, string secret, string status)
        {
            string url = string.Format(TwitterBaseUrlFormat, "statuses", "update", "json");
            Request ro = GetoAuthRequest(url, token, secret);
            ro.Method = requestMethod.POST;
            ro.Parameters.Add("status", status);
            string s = ro.ExecuteRequest<string>();
        }

        public static void UpdateStatus(string UN, string PWD, string status)
        {
            Request r = GetRequest(string.Format(TwitterBaseUrlFormat, "statuses", "update", "json"), UN, PWD);
            r.Method = requestMethod.POST; 
            r.Parameters.Add("status", status);
            r.ExecuteRequest<string>();
        }

    }
}