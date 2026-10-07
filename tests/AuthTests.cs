using System.Net;
using System.Net.Sockets;
using System.Text;
using Jotdash.Services;

internal static class AuthTests
{
    public static async Task RunAsync(LocalStore store)
    {
        Check(!new ConnectionSettings { ServerUrl = "/" }.IsSet, "A blank address normalized to a slash is not a server.");
        Check(!new ConnectionSettings { ServerUrl = "file:///tmp/server" }.IsSet, "Only HTTP(S) server addresses are accepted.");
        using var portFinder = new TcpListener(IPAddress.Loopback, 0);
        portFinder.Start();
        int port = ((IPEndPoint)portFinder.LocalEndpoint).Port;
        portFinder.Stop();
        string origin = $"http://127.0.0.1:{port}/";
        using var listener = new HttpListener();
        listener.Prefixes.Add(origin);
        listener.Start();

        string mode = "redirect";
        int loginRequests = 0, writeRequests = 0;
        bool tokenReceived = false, basicReceived = false;
        var server = Task.Run(async () =>
        {
            try
            {
                while (listener.IsListening)
                {
                    var ctx = await listener.GetContextAsync();
                    string path = ctx.Request.Url!.AbsolutePath;
                    if (path == "/login") loginRequests++;
                    if (ctx.Request.HttpMethod != "GET") writeRequests++;
                    tokenReceived |= ctx.Request.Headers["P-Access-Token-Id"] == "test-id"
                        && ctx.Request.Headers["P-Access-Token"] == "test-token";
                    basicReceived |= ctx.Request.Headers["Authorization"] ==
                        "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("test-user:test-password"));
                    string body;
                    if (mode == "redirect")
                    {
                        ctx.Response.StatusCode = 302;
                        ctx.Response.RedirectLocation = origin + "login";
                        body = "";
                    }
                    else if (mode == "denied")
                    {
                        ctx.Response.StatusCode = 401;
                        body = "";
                    }
                    else if (mode is "html" or "untyped-html")
                    {
                        if (mode == "html") ctx.Response.ContentType = "text/html";
                        body = "<html><body>Sign in</body></html>";
                    }
                    else
                    {
                        ctx.Response.ContentType = "application/json";
                        body = "{\"workspaces\":[]}";
                    }
                    byte[] bytes = Encoding.UTF8.GetBytes(body);
                    ctx.Response.ContentLength64 = bytes.Length;
                    await ctx.Response.OutputStream.WriteAsync(bytes);
                    ctx.Response.Close();
                }
            }
            catch (Exception e) when (!listener.IsListening && e is HttpListenerException or ObjectDisposedException) { }
        });

        try
        {
            var conn = new ConnectionSettings { ServerUrl = origin + "quicknotes/", AuthMode = "basic", User = "test-user", Secret = "test-password" };
            foreach (string failure in new[] { "redirect", "html", "untyped-html", "denied" })
            {
                mode = failure;
                string report = "";
                string result = await SyncService.TestAsync(conn, text => report = text);
                Check(result.Contains("account username/password is not HTTP Basic"), $"{failure}: actionable account-vs-Basic explanation.");
                Check(report.Contains("Response: HTTP"), "Diagnostics include HTTP response metadata.");
                Check(!report.Contains("test-user") && !report.Contains("test-password") && !report.Contains("<html>"), "Diagnostics omit credentials and page contents.");
                var settings = new SettingsService();
                await settings.SaveAsync(conn);
                string? error = await new SyncService(store, settings).SyncAsync();
                Check(error?.Contains("Your tasks remain saved") == true, $"{failure}: background sync reports authentication failure.");
            }
            Check(loginRequests == 0, "API client never follows a login redirect.");
            Check(writeRequests == 0, "Authentication failure never sends notes or files.");
            Check(store.PendingCount > 0, "Blocked sync keeps local work queued.");
            Check(basicReceived, "Explicit HTTP Basic credentials are sent.");
            mode = "json";
            conn.AuthMode = "token";
            conn.User = "test-id";
            conn.Secret = "test-token";
            Check((await SyncService.TestAsync(conn)).StartsWith("✅"), "A JSON API response passes connection test.");
            Check(tokenReceived, "Both documented Pangolin token headers are sent.");
            using var safe = new ConnectionDiagnostics(new ConnectionSettings
            {
                ServerUrl = "https://secret-user:secret-password@example.com/secret-path?token=secret-token",
                AuthMode = "secret-mode", User = "secret-user", Secret = "secret-password", ApiKey = "secret-key",
            });
            safe.Failure(new HttpRequestException("secret-token"));
            Check(!safe.Report.Contains("secret-user") && !safe.Report.Contains("secret-password") &&
                !safe.Report.Contains("secret-token") && !safe.Report.Contains("secret-key") &&
                !safe.Report.Contains("secret-path") && !safe.Report.Contains("secret-mode"),
                "Diagnostics omit credential-bearing URL parts, unknown auth labels and exception messages.");
            for (int i = 0; i < 25; i++) safe.Failure(new IOException());
            Check(safe.Report.Split("Failure type:").Length == 21, "Diagnostics retain at most twenty transport events.");
        }
        finally
        {
            listener.Stop();
            await server;
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
