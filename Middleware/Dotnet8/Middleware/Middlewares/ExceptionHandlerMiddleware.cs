﻿using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;

namespace Middleware.Middlewares;
internal class ExceptionHandlerMiddleware(ILogger<ExceptionHandlerMiddleware> logger) : IFunctionsWorkerMiddleware
{
	private readonly ILogger<ExceptionHandlerMiddleware> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
	{
		try
		{
			// Calls the next function in the pipeline with the updated function context.
			await next.Invoke(context);
		}
		catch (Exception ex)
		{
			_logger.LogError("Error, {message}", ex.Message);
        }
	}
}
