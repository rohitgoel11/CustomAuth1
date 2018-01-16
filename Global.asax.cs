using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using System.Web.Security;

namespace WebApplication2
{
    public class MvcApplication : System.Web.HttpApplication
    {
        public const int LOGON32_LOGON_INTERACTIVE = 2;
        public const int LOGON32_PROVIDER_DEFAULT = 0;

        public static WindowsIdentity guestIdentity;
        static IntPtr token = IntPtr.Zero;

        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern int LogonUser(
            string lpszUsername, string lpszDomain,
            string lpszPassword, int dwLogonType,
            int dwLogonProvider, out IntPtr phToken
            );

        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool RevertToSelf();

        //[DllImport("kernel32.dll", SetLastError = true)]
        //public static extern int CloseHandle(IntPtr hObject);


        public void ImpersonationProcess(string username, string domain, string password)
        {
            try
            {
                if (HttpContext.Current.Cache["TK"] != null)
                {
                    var cachedToken = (IntPtr)HttpContext.Current.Cache["TK"];

                    if (cachedToken != IntPtr.Zero)
                    {
                        guestIdentity = new WindowsIdentity(cachedToken);
                        guestIdentity.Impersonate();
                        return;
                    }
                }
            }
            catch
            {
                // intentionally supressing the exception
            }

            if (RevertToSelf())
            {
                //logon using service/guest account
                if (LogonUser(username, domain, password, LOGON32_LOGON_INTERACTIVE, LOGON32_PROVIDER_DEFAULT, out token) != 0)
                {
                    guestIdentity = new WindowsIdentity(token);
                    var impersonationContext = guestIdentity.Impersonate();

                    //cache the token
                    HttpContext.Current.Cache["TK"] = token;
                }
                else
                {
                    // service account logon failed. Log the messsage or send notification to admin
                    throw new System.Security.SecurityException("Guest account failed to login!");
                }
            }

        }


        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
        }


        protected void Application_BeginRequest()
        {
            bool hasHeaderValue = false;

            string headerValue = HttpContext.Current.Request.Headers["MyHeader1"];

            if (headerValue == "true")
                hasHeaderValue = true;

            if (hasHeaderValue)
            {
                // perform custom authentication here
             
                // on successful custom authentication impersonate windows user (service account)
                // else raise authentication exception
                ImpersonationProcess("rgoe11", "sapient", "43Rfv#edc");
                //HttpContext.Current.Request.Headers.Add("MyCustomHeader", "Rohit");
                HttpContext.Current.User = new WindowsPrincipal(guestIdentity);
            }
        }

        void WindowsAuthentication_OnAuthenticate(object sender, WindowsAuthenticationEventArgs e)
        {
            bool hasHeaderValue = false;

            string headerValue = HttpContext.Current.Request.Headers["MyHeader1"];

            if (headerValue == "true")
                hasHeaderValue = true;


            if (hasHeaderValue)
            {
                if (guestIdentity != null)
                    HttpContext.Current.User = new WindowsPrincipal(guestIdentity);
            }
        }

        //// Do not use this as we are caching the token
        //protected void Application_EndRequest()
        //{
        //    if (token != IntPtr.Zero)
        //    {
        //        CloseHandle(token);
        //    }
        //}


    }
}
