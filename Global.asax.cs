using System;
using System.Data;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;

using EnrollmentBAL;
using Enrollment.Models;
using EnrollmentDAL;
using System.Web.Security;
using Newtonsoft.Json;
using DataAnnotationSample.Attributes;
using DataAnnotationSample.Validators;
using CaptchaMvc.Infrastructure;
using System.Configuration;
using System.Text;

namespace Enrollment
{
    public class MvcApplication : System.Web.HttpApplication
    {
        
        protected void Application_Start()
        { 
            log4net.Config.DOMConfigurator.Configure();

            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
            DataAnnotationsModelValidatorProvider.RegisterAdapter(typeof(NotAsSameAttribute), typeof(NotAsSameValidator));

            MvcHandler.DisableMvcResponseHeader = true;
            CaptchaUtils.CaptchaManager.StorageProvider = new CookieStorageProvider();
            MasterUtilModel objMasterData = new MasterUtilModel();
            objMasterData.ReadMasterData();

            // STAGING — Step 5 (in-process scheduler). Starts a timer that calls
            // the ProcessStagingClaims endpoint every 5 minutes for as long as the
            // app is running. No external Task Scheduler / PowerShell needed: it
            // self-starts on deploy (prod) and on first request (local).
            StagingScheduler.Start();
        }

        protected void Application_End()
        {
            // Stop the staging timer cleanly on app shutdown / pool recycle.
            StagingScheduler.Stop();
        }

        protected void Application_PreSendRequestHeaders(object sender, EventArgs e)
        {

            //HttpContext.Current.Response.Headers.Remove("X-Powered-By");
            //HttpContext.Current.Response.Headers.Remove("X-AspNet-Version");
            //HttpContext.Current.Response.Headers.Remove("X-AspNetMvc-Version");
            HttpContext.Current.Response.Headers.Remove("Server");

            var response = HttpContext.Current.Response;

            // --- Add security headers ---

            // Prevent clickjacking
            response.Headers["X-Frame-Options"] = "SAMEORIGIN"; // or "DENY" if iframe embedding not needed

            // Prevent MIME-type sniffing
            response.Headers["X-Content-Type-Options"] = "nosniff";

            // Enable basic XSS protection
            response.Headers["X-XSS-Protection"] = "1; mode=block";

            // Control what information is sent in Referer header
            response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

            // Restrict browser features (geolocation, camera, microphone, etc.)
            response.Headers["Permissions-Policy"] =
                "geolocation=(), microphone=(), camera=(), fullscreen=(self)";
            // Strong Content Security Policy (CSP)
            // --- CSP (Dynamic from appSettings) ---
            var csp = new StringBuilder();

            csp.Append("default-src 'self'; ");
            csp.Append($"script-src {GetCsp("CSP_ScriptSrc")}; ");
            csp.Append($"style-src {GetCsp("CSP_StyleSrc")}; ");
            csp.Append($"img-src {GetCsp("CSP_ImgSrc")}; ");
            csp.Append($"font-src {GetCsp("CSP_FontSrc")}; ");
            csp.Append($"connect-src {GetCsp("CSP_ConnectSrc")}; ");
            csp.Append($"frame-src {GetCsp("CSP_FrameSrc")}; ");
            csp.Append("object-src 'none'; ");
            csp.Append("frame-ancestors 'self';");

            response.Headers["Content-Security-Policy"] = Normalize(csp.ToString());


            response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
        }
        private static string GetCsp(string key)
        {
            return ConfigurationManager.AppSettings[key] ?? "'self'";
        }

        private static string Normalize(string value)
        {
            return value.Replace("\r", "")
                        .Replace("\n", " ")
                        .Replace("  ", " ")
                        .Trim();
        }
        protected void Application_PostAuthenticateRequest(Object sender, EventArgs e)
        {
            HttpCookie authCookie = Request.Cookies[FormsAuthentication.FormsCookieName];
            if (authCookie != null)
            {

                FormsAuthenticationTicket authTicket = FormsAuthentication.Decrypt(authCookie.Value);
                CustomPrincipal newUser = new CustomPrincipal(authTicket.Name);
                newUser.FirstName = authTicket.Name;

                HttpContext.Current.User = newUser;
            }

        }

        public class CustomPrincipalSerializeModel
        {
            public int UserId { get; set; }
            public string FirstName { get; set; }
            public string LastName { get; set; }
            public string[] roles { get; set; }
        }

        public class CustomPrincipal : System.Security.Principal.IPrincipal
        {
            public System.Security.Principal.IIdentity Identity { get; private set; }
            public bool IsInRole(string role)
            {
                if (roles.Any(r => role.Contains(r)))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }

            public CustomPrincipal(string Username)
            {
                this.Identity = new System.Security.Principal.GenericIdentity(Username);
            }

            public int UserId { get; set; }
            public string FirstName { get; set; }
            public string LastName { get; set; }
            public string[] roles { get; set; }
        } 

        //protected virtual new CustomPrincipal User
        //{
        //    get { return HttpContext.User as CustomPrincipal; }
        //}
    }

    /// <summary>
    /// STAGING — in-process 5-minute scheduler.
    ///
    /// Fires every 5 minutes for the life of the application and calls the
    /// ProcessStagingClaims endpoint over localhost HTTP (so it runs through the
    /// normal MVC pipeline exactly as an external caller would). Self-starting:
    /// begins in Application_Start (on deploy in prod, on first request locally).
    ///
    /// Notes / trade-offs:
    ///  - An in-process timer lives in the IIS worker process. If the app pool
    ///    is idle-shutdown or recycled, the timer stops until the next request
    ///    re-runs Application_Start. The staging worker is self-healing (it picks
    ///    up ALL unprocessed stage-52 claims, not just recent ones), so any gap
    ///    is automatically caught up on the next tick — nothing is lost.
    ///  - To keep it running 24/7, disable the app-pool Idle Time-out (set to 0)
    ///    and optionally enable "AlwaysRunning" / preload on the site.
    ///  - Controlled by Web.config AppSettings:
    ///       EnableStagingScheduler  (default true)  — master on/off
    ///       StagingSchedulerUrl      (optional)     — explicit endpoint URL;
    ///         if omitted, defaults to http://localhost:{port}/MedicalScrutiny/ProcessStagingClaims
    ///       StagingApiKey            (optional)     — sent as x-staging-key
    /// </summary>
    public static class StagingScheduler
    {
        private static System.Threading.Timer _timer;
        private static int _isRunning = 0; // 0 = idle, 1 = a tick is in progress
        private static readonly object _lock = new object();

        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

        // Simple file logger so we can SEE what the timer is doing (it runs on a
        // background thread with no UI). Logs to App_Data/Logs/StagingScheduler_*.log.
        private static void Log(string msg)
        {
            try
            {
                string dir = System.Web.Hosting.HostingEnvironment.MapPath("~/App_Data/Logs");
                if (dir == null) dir = System.IO.Path.GetTempPath();
                if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
                string file = System.IO.Path.Combine(dir,
                    "StagingScheduler_" + DateTime.Now.ToString("yyyyMMdd") + ".log");
                System.IO.File.AppendAllText(file,
                    DateTime.Now.ToString("HH:mm:ss.fff") + "  " + msg + Environment.NewLine);
            }
            catch { }
        }

        public static void Start()
        {
            try
            {
                string enabled = ConfigurationManager.AppSettings["EnableStagingScheduler"];
                if (!string.IsNullOrEmpty(enabled) &&
                    enabled.Equals("false", StringComparison.OrdinalIgnoreCase))
                {
                    Log("Start() — disabled via EnableStagingScheduler=false. Not starting.");
                    return; // explicitly disabled
                }

                lock (_lock)
                {
                    if (_timer != null) { Log("Start() — already started."); return; }
                    // First tick after 1 minute (let the app finish warming up),
                    // then every 5 minutes.
                    _timer = new System.Threading.Timer(
                        Tick, null, TimeSpan.FromMinutes(1), Interval);
                    Log("Start() — timer created. First tick in 1 min, then every 5 min.");
                }
            }
            catch (Exception ex)
            {
                Log("Start() EXCEPTION: " + ex.Message);
                // Never let scheduler startup break app startup.
            }
        }

        public static void Stop()
        {
            lock (_lock)
            {
                if (_timer != null)
                {
                    _timer.Dispose();
                    _timer = null;
                    Log("Stop() — timer disposed.");
                }
            }
        }

        private static void Tick(object state)
        {
            // Skip this tick if the previous one is still running (no overlap).
            if (System.Threading.Interlocked.CompareExchange(ref _isRunning, 1, 0) != 0)
            {
                Log("Tick — skipped (previous tick still running).");
                return;
            }

            try
            {
                Log("Tick — firing.");
                CallProcessStagingClaims();
                Log("Tick — completed.");
            }
            catch (Exception ex)
            {
                Log("Tick EXCEPTION: " + ex.Message +
                    (ex.InnerException != null ? " | inner: " + ex.InnerException.Message : ""));
            }
            finally
            {
                System.Threading.Interlocked.Exchange(ref _isRunning, 0);
            }
        }

        private static void CallProcessStagingClaims()
        {
            string url = ConfigurationManager.AppSettings["StagingSchedulerUrl"];
            if (string.IsNullOrEmpty(url))
            {
                // Default to localhost on whatever port this site is bound to.
                int port = 80;
                try
                {
                    if (HttpContext.Current != null && HttpContext.Current.Request != null)
                        port = HttpContext.Current.Request.Url.Port;
                }
                catch { }
                // HttpContext is usually null on a timer thread; fall back to a
                // configured URL. If neither is available, use localhost:80.
                url = string.Format(
                    "http://localhost:{0}/MedicalScrutiny/ProcessStagingClaims", port);
            }

            string apiKey = ConfigurationManager.AppSettings["StagingApiKey"] ?? "";

            Log("CallProcessStagingClaims — URL=" + url + (string.IsNullOrEmpty(apiKey) ? " (no key)" : " (with key)"));

            System.Net.ServicePointManager.SecurityProtocol =
                System.Net.SecurityProtocolType.Tls12 |
                System.Net.SecurityProtocolType.Tls11 |
                System.Net.SecurityProtocolType.Tls;
            System.Net.ServicePointManager.ServerCertificateValidationCallback =
                (s, cert, chain, errors) => true;

            using (var client = new System.Net.Http.HttpClient())
            {
                client.Timeout = TimeSpan.FromMinutes(10); // a batch can take a while
                var req = new System.Net.Http.HttpRequestMessage(
                    System.Net.Http.HttpMethod.Get, url);
                if (!string.IsNullOrEmpty(apiKey))
                    req.Headers.Add("x-staging-key", apiKey);

                // Fire and read so the request completes; result handling lives
                // in the endpoint itself.
                var resp = client.SendAsync(req).GetAwaiter().GetResult();
                string body = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                string snippet = body == null ? "" : (body.Length > 300 ? body.Substring(0, 300) : body);
                Log("CallProcessStagingClaims — HTTP " + (int)resp.StatusCode + " response: " + snippet);
            }
        }
    }

}
