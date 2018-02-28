using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace APIs
{
    internal class Services
    {
     
        private static Object p_GlobalLock = new Object();
        public static Object GlobalLock
        {
            get { return Services.p_GlobalLock; }
            set { Services.p_GlobalLock = value; }
        }

        private static DateTime p_LastRequest;
        public static DateTime LastRequest
        {
            get { return Services.p_LastRequest; }
            set { Services.p_LastRequest = value; }
        }


    }
}
