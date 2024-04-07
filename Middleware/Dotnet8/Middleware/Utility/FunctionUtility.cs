using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace Middleware.Utility;
internal static class FunctionUtility
{
    internal static void LogCustomException(this ILogger logger, string title, Exception ex, string requestMessage = "")
    {
        var errorBuilder = new StringBuilder();

        errorBuilder.AppendFormat("Title\t\t : {0}\n\n", title);
        errorBuilder.AppendFormat("Exception\t : {0}\n\n", ex.Message);
        errorBuilder.AppendFormat("Inner Exception\t : {0}\n\n", ex.InnerException?.Message);
        errorBuilder.AppendFormat("Stack Trace\t : {0}\n\n", ex.StackTrace);
        errorBuilder.AppendFormat("Message\t\t : {0}\n\n", requestMessage);

        logger.LogError("{errorMessage}", errorBuilder.ToString());
    }

    internal async static Task CreateErrorResponse(FunctionContext context, ProblemDetails details, int statusCode)
    {
        var httpReqData = await context.GetHttpRequestDataAsync();

        if (httpReqData is not null)
        {
            var newHttpResponse = httpReqData.CreateResponse((HttpStatusCode)statusCode);
            await newHttpResponse.WriteAsJsonAsync(details, (HttpStatusCode)statusCode);

            var invocationResult = context.GetInvocationResult();
            var httpOutputBinding = context.GetOutputBindings<HttpResponseData>().FirstOrDefault(b => b.BindingType == "http" && b.Name != "$return");

            if (httpOutputBinding is not null)
            {
                httpOutputBinding.Value = newHttpResponse;
            }
            else
            {
                invocationResult.Value = newHttpResponse;
            }
        }
    }
}