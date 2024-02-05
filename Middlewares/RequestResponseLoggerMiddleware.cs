using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Net;

namespace Web.Api.Middlewares
{
    public class RequestResponseLoggerMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly RequestResponseLoggerOption _options;
        private readonly IRequestResponseLogger _logger;

        public RequestResponseLoggerMiddleware(RequestDelegate next, IOptions<RequestResponseLoggerOption> options, IRequestResponseLogger logger)
        {
            _next = next;
            _options = options.Value;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext httpContext, IRequestResponseLogModelCreator logCreator)
        {
            RequestResponseLogModel log = logCreator.LogModel;
            // Middleware is enabled only when the EnableRequestResponseLogging config value is set.
            if (_options == null || !_options.IsEnabled)
            {
                await _next(httpContext);
                return;
            }

            log.Level = "Info";
            log.Time = DateTime.UtcNow;
            HttpRequest request = httpContext.Request;

            /*log*/
            var ip = request.HttpContext.Connection.RemoteIpAddress;
            if (ip is not null) {
                log.ClientName = Dns.GetHostEntry(ip).HostName;
                log.ClientIp = ip == null ? null : ip.ToString();
            }            
           
            /*request*/
            log.ApiMethodName = request.Path.ToString().Split(new[] { '/' }).Last();
            log.RequestQuery = request.QueryString.ToString();
            log.Message = await ReadBodyFromRequest(request);
            log.RequestHost = request.Host.Host;

            _logger.Log(logCreator);

            // Temporarily replace the HttpResponseStream, which is a write-only stream, with a MemoryStream to capture it's value in-flight.
            HttpResponse response = httpContext.Response;
            var originalResponseBody = response.Body;
            using var newResponseBody = new MemoryStream();
            response.Body = newResponseBody;

            // Call the next middleware in the pipeline
            try
            {
                await _next(httpContext);
            }
            catch (Exception exception)
            {
                /*exception: but was not managed at app.UseExceptionHandler() or by any middleware*/
                LogError(log, exception);
            }

            newResponseBody.Seek(0, SeekOrigin.Begin);
            var responseBodyText = await new StreamReader(response.Body).ReadToEndAsync();

            newResponseBody.Seek(0, SeekOrigin.Begin);
            await newResponseBody.CopyToAsync(originalResponseBody);

            /*response*/

            if (log.Level == "Info")
            {
                log.Message = responseBodyText;
            }

            /*exception: but was managed at app.UseExceptionHandler() or by any middleware*/
            var contextFeature = httpContext.Features.Get<IExceptionHandlerPathFeature>();
            if (contextFeature != null && contextFeature.Error != null)
            {
                Exception exception = contextFeature.Error;
                LogError(log, exception);
            }

            _logger.Log(logCreator);
        }

        private void LogError(RequestResponseLogModel log, Exception exception)
        {
            log.Level = "Error";
            log.Message = exception.Message;
        }

        private Dictionary<string, string> FormatHeaders(IHeaderDictionary headers) 
        {
            Dictionary<string, string> pairs = new Dictionary<string, string>();
            foreach (var header in headers)
            {
                pairs.Add(header.Key, header.Value);
            }
            return pairs;
        }

        private List<KeyValuePair<string, string>> FormatQueries(string queryString)
        {
            List<KeyValuePair<string, string>> pairs = new List<KeyValuePair<string, string>>();
            string key, value;
            foreach (var query in queryString.TrimStart('?').Split("&"))
            {
                var items = query.Split("=");
                key = items.Count() >= 1 ? items[0] : string.Empty;
                value = items.Count() >= 2 ? items[1] : string.Empty;
                if (!String.IsNullOrEmpty(key))
                {
                    pairs.Add(new KeyValuePair<string, string>(key, value));
                }    
            }
            return pairs;
        }

        private async Task<string> ReadBodyFromRequest(HttpRequest request)
        {
            // Ensure the request's body can be read multiple times (for the next middlewares in the pipeline).
            request.EnableBuffering();
            using var streamReader = new StreamReader(request.Body, leaveOpen: true);
            var requestBody = await streamReader.ReadToEndAsync();
            // Reset the request's body stream position for next middleware in the pipeline.
            request.Body.Position = 0;
            return requestBody;
        }
    }

    public class RequestResponseLoggerOption
    {
        public bool IsEnabled { get; set; }
        public string? Name { get; set; }
        public string? DateTimeFormat { get; set; }
    }

    public class RequestResponseLogModel
    {
        public string? Level { get; set; }
        public DateTime? Time { get; set; }
        public string? ClientIp { get; set; }
        public string? ClientName { get; set; }
        public string? RequestHost { get; set; }
        public string? ApiMethodName { get; set; }
        public string? RequestQuery { get; set; }
        public string? Message { get; set; }
    }
   
    public interface IRequestResponseLogModelCreator
    {
        RequestResponseLogModel LogModel { get; }
        string LogString();
    }
   
    public interface IRequestResponseLogger
    {
        void Log(IRequestResponseLogModelCreator logCreator);
    }
    public class RequestResponseLogModelCreator : IRequestResponseLogModelCreator
    {
        public RequestResponseLogModel LogModel { get; private set; }

        public RequestResponseLogModelCreator()
        {
            LogModel = new RequestResponseLogModel();
        }

        public string LogString()
        {
            var jsonString = JsonConvert.SerializeObject(LogModel);
            return jsonString;
        }
    }

    public class RequestResponseLogger : IRequestResponseLogger
    {
        private readonly ILogger<RequestResponseLogger> _logger;

        public RequestResponseLogger(ILogger<RequestResponseLogger> logger)
        {
            _logger = logger;
        }
        public void Log(IRequestResponseLogModelCreator logCreator)
        {
            _logger.LogCritical(logCreator.LogString());
        }
    }
}