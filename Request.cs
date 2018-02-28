using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OAuth;
using System.Collections.Specialized;
using System.Xml.Linq;


namespace APIs
{
    public enum requestMethod { GET, POST };

    public class Request
    {

        private static readonly HttpUtility HttpUtility = new HttpUtility();

        internal Dictionary<String, String> m_Parameters = new Dictionary<String, String>();
        internal WebHeaderCollection m_Headers = new WebHeaderCollection();
        internal NetworkCredential m_Credentials;
        internal string m_RequestUri;
        
        internal HttpWebRequest m_HttpRequest;
        private IAsyncResult m_AsyncHandle;

        public event EventHandler RequestComplete;

        public Request(string uri)
        {
            this.m_RequestUri = uri;
        }

        public Request(string uri, string consumerKey, string consumerSecret)
        {
            this.m_RequestUri = uri;
            this.oAuth_ConsumerKey = consumerKey;
            this.oAuth_ConsumerSecret = consumerSecret;
        }

        public Request (string uri, string consumerKey, string consumerSecret, string token, string secret)
        {
            this.m_RequestUri = uri;
            this.oAuth_ConsumerKey = consumerKey;
            this.oAuth_ConsumerSecret = consumerSecret;

            this.oAuth_Token = token;
            this.oAuth_Secret = secret;
        }

        public Dictionary<String, String> Parameters
        {
            get { return this.m_Parameters; }
        }

        public WebHeaderCollection Headers
        {
            get { return this.m_Headers; }
        }
        public NetworkCredential Credentials
        {
            set {this.m_Credentials= value ;} 
        }



        public string oAuth_ConsumerKey { get; set; }
        public string oAuth_ConsumerSecret { get; set; }


        public string oAuth_Token { get; set; }
        public string oAuth_Secret { get; set; }

        private requestMethod _Method = requestMethod.GET;
        public requestMethod Method
        {
            get { return _Method; }
            set { _Method = value; }
        }

        
        private OAuthBase.SignatureTypes _oAuthSignatureTypes = OAuthBase.SignatureTypes.HMACSHA1;
        public OAuthBase.SignatureTypes oAuthSignatureTypes 
        {
            get { return _oAuthSignatureTypes ; }
            set { _oAuthSignatureTypes= value ;} 
        }

      

        private string _oAuthVersion = "1.0";
        public string oAuthVersion
        {
            get { return _oAuthVersion; }
            set { _oAuthVersion = value; }
        }




        public T ExecuteRequest<T>()
        {
            this.CreateRequest();
            
            Monitor.Enter(Services.GlobalLock);

            try
            {
                TimeSpan timeSinceLastRequest = DateTime.Now.Subtract(Services.LastRequest);

                if (timeSinceLastRequest.TotalSeconds < 0.50)
                {
                    Thread.Sleep(500 - (Int32)timeSinceLastRequest.TotalMilliseconds);
                }

                return this.ProcessResponse<T>((HttpWebResponse)this.m_HttpRequest.GetResponse());
            }
            finally
            {
                Services.LastRequest = DateTime.Now;

                Monitor.Exit(Services.GlobalLock);
            }
        }

        public void BeginRequest()
        {
            this.CreateRequest();
            Monitor.Enter(Services.GlobalLock);
            TimeSpan timeSinceLastRequest = DateTime.Now.Subtract(Services.LastRequest);
            if (timeSinceLastRequest.TotalSeconds < 0.1)
            {
                Thread.Sleep(100 - (Int32)timeSinceLastRequest.TotalMilliseconds);
            }
            this.m_AsyncHandle = this.m_HttpRequest.BeginGetResponse(new AsyncCallback(GetResponseCallback), null);
        }

        public T EndRequest<T>()
        {
            try
            {
                return this.ProcessResponse<T>((HttpWebResponse)this.m_HttpRequest.EndGetResponse(this.m_AsyncHandle));
            }
            finally
            {
                Services.LastRequest = DateTime.Now;
                Monitor.Exit(Services.GlobalLock);
            }
        }

        internal virtual void CreateRequest()
        {
            if (this.m_HttpRequest != null)
            {
                throw new InvalidOperationException("Request instance can only be used for a single request.");
            }
            
            StringBuilder uriBuilder = new StringBuilder();

            foreach (KeyValuePair<String, String> param in this.m_Parameters)
            {
                if (!String.IsNullOrEmpty(param.Value))
                {
                    if (uriBuilder.Length != 0)
                    { uriBuilder.Append('&');} 
                    uriBuilder.Append(param.Key);
                    uriBuilder.Append('=');
                    uriBuilder.Append(this.UrlEncode(param.Value));
                }
            }

            string strData = uriBuilder.ToString();
            string strUrl = this.m_RequestUri;

            if (oAuth_ConsumerKey != null)
            {
                OAuthBase oAuth = new OAuthBase();
                string nonce = oAuth.GenerateNonce();
                string timeStamp = oAuth.GenerateTimeStamp();
                
                Uri reqUri = null;

                reqUri = new Uri(this.m_RequestUri + ((strData == "") ? "" : "?" + strData));

                string signature = oAuth.GenerateSignature(reqUri, oAuth_ConsumerKey, oAuth_ConsumerSecret,oAuth_Token , oAuth_Secret,
                    Method.ToString() , timeStamp, nonce, oAuthSignatureTypes,
                    out strUrl, out strData);

                strData +=  "&oauth_signature=" + this.UrlEncode(signature);

            }

            HttpWebRequest WebReq = null;

            if (Method == requestMethod.GET)
            {
                WebReq = (HttpWebRequest)HttpWebRequest.Create(strUrl + ((strData == "") ? "" : "?") + strData);
                
                if (m_Headers != null)
                {
                    WebReq.Headers = this.m_Headers;
                }
                WebReq.ServicePoint.Expect100Continue = false;

                WebReq.Method = Method.ToString();

            }
            else if (Method == requestMethod.POST)
            {
                WebReq = (HttpWebRequest)HttpWebRequest.Create(strUrl);
                if (m_Headers != null)
                {
                    WebReq.Headers = this.m_Headers;
                }
                //Encoding the post vars

                byte[] buffer = Encoding.UTF8.GetBytes(strData);
                //Initialisation with provided url
                WebReq.ServicePoint.Expect100Continue = false;
                //Set method to post, otherwise postvars will not be used
                WebReq.Method = Method.ToString();
                WebReq.ContentType = "application/x-www-form-urlencoded";
                WebReq.ContentLength = buffer.Length;
                Stream PostData = WebReq.GetRequestStream();
                PostData.Write(buffer, 0, buffer.Length);
                //Closing is always important
                PostData.Close();

            }
            else
            {
                throw new Exception("Posting using Method [" + Method + "] is not implemented");
            }

            if (m_Credentials!=null)
            {
                WebReq.Credentials = this.m_Credentials;
            }

            //WebReq.UserAgent  = "Twtri";
            //WebReq.Timeout = 20000;
            this.m_HttpRequest = WebReq;

        }

        private void GetResponseCallback(IAsyncResult result)
        {
            if (this.RequestComplete != null)
            {
                this.RequestComplete(this, new EventArgs());
            }
        }

        private T ProcessResponse<T>(HttpWebResponse response)
        {
            
            if (((Int32)response.StatusCode / 100) == 2)
            {
                using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                {
                    if (typeof(T) == typeof(JObject))
                    {
                        return (T)(object) JObject.Parse(reader.ReadToEnd());
                    }
                    else if (typeof(T) == typeof(string))
                    {
                        return (T)(object)reader.ReadToEnd();
                    }
                    else if (typeof(T) == typeof(XDocument))
                    {
                        return (T)(object)XDocument.Parse(reader.ReadToEnd());
                    }
                    else if (typeof(T) == typeof(Nullable))
                    {
                        reader.ReadToEnd();
                        return (T) (object) null ;
                    }
                    else
                    {
                        throw new Exception(typeof(T).Name + " parsing is not implemented") ;
                    }
                }
            }
            else
            {
                throw new Exception("Unexpected Response");
            }
        }


        /// <summary>
        /// Exchange the request token for an access token.
        /// </summary>
        /// <param name="authToken">The oauth_token is supplied by Twitter's authorization page following the callback.</param>
        public void GetAccessToken(string AccessTokenUri)
        {

            Request r = new Request(AccessTokenUri,this.oAuth_ConsumerKey, this.oAuth_ConsumerSecret, this.oAuth_Token, this.oAuth_Secret);
            
            string response = r.ExecuteRequest<string>();

            if (response.Length > 0)
            {
                //Store the Token and Token Secret
                NameValueCollection qs = HttpUtility.ParseQueryString(response);

                if (qs["oauth_token"] != null)
                {
                    this.oAuth_Token= qs["oauth_token"];
                }
                if (qs["oauth_token_secret"] != null)
                {
                    this.oAuth_Secret = qs["oauth_token_secret"];
                }
            }


        }



        protected string unreservedChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_.~";

        /// <summary>
        /// This is a different Url Encode implementation since the default .NET one outputs the percent encoding in lower case.
        /// While this is not a problem with the percent encoding spec, it is used in upper case throughout OAuth
        /// </summary>
        /// <param name="value">The value to Url encode</param>
        /// <returns>Returns a Url encoded string</returns>
        protected string UrlEncode(string value)
        {
            StringBuilder result = new StringBuilder();

            foreach (char symbol in value)
            {
                if (unreservedChars.IndexOf(symbol) != -1)
                {
                    result.Append(symbol);
                }
                else
                {
                    result.Append('%' + String.Format("{0:X2}", (int)symbol));
                }
            }

            return result.ToString();
        }


    }
}