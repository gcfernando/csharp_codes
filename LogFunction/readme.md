This project provides a reliable and consistent way to capture and record messages and events across various scenarios. It ensures that updates, warnings, and errors are clearly documented, making it easier to understand what is happening and why. By organizing information in a structured and systematic manner, it helps teams:

- Track progress effectively  
- Identify and troubleshoot issues quickly  
- Investigate problems thoroughly  

All of this is achieved while maintaining clarity, consistency, and readability in the logs.

using System.Collections;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;

//
// Developer ::> Gehan Fernando
// Date      ::> 15-Sep-2025
//
// ──────────────────────────────────────────────────────────────
// 📘 ExLogger: High-Performance Logging Helper
// ──────────────────────────────────────────────────────────────
//
// PURPOSE
//   - Centralizes all logging calls into a single static utility.
//   - Reduces allocations with LoggerMessage.Define delegates,
//     cached EventIds, pooled StringBuilders, and struct scopes.
//   - Provides consistent log formatting across the codebase.
//
// WHEN TO USE
//   ✅ High-throughput apps (e.g., APIs, Azure Functions, services).
//   ✅ Where allocation-sensitive performance matters.
//   ❌ Not strictly needed for small apps or low-volume logging.
//
// INTEGRATION
//   1. Register logging in Program.cs or Startup.cs:
//        var logger = LoggerFactory.Create(builder => builder.AddConsole())
//                                  .CreateLogger("AppLogger");
//
//   2. Use ExLogger for all logs instead of raw ILogger:
//        ExLogger.LogInformation(logger, "Starting service...");
//        ExLogger.LogError(logger, "Failed to connect to {Db}", dbName);
//
//   3. For scoped logging (e.g., RequestId):
//        using (ExLogger.BeginScope(logger, "RequestId", request.Id))
//        {
//            ExLogger.LogDebug(logger, "Handling request");
//        }
//
//   4. For exception logging (formats stacktrace, inner exception, etc):
//        try
//        {
//            ...
//        }
//        catch (Exception ex)
//        {
//            ExLogger.LogException(logger, ex);
//        }
//
// NOTES
//   - Fast path (no args) avoids allocations completely.
//   - Structured logging with args falls back to cached EventIds.
//   - NullScope ensures BeginScope() is always safe (never null).
//
// ──────────────────────────────────────────────────────────────
//
