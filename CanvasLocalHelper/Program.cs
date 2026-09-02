using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

Console.WriteLine("USC Canvas Local Helper");
Console.WriteLine("Token and environment are held in memory only and cleared when this process exits.");
Console.WriteLine(new string('=', 72));
WriteColor("STEP 1 - Choose the Canvas environment and enter the access token below.", ConsoleColor.Cyan);
Console.WriteLine(new string('=', 72));
Console.WriteLine("  1. Production  - https://usc-online.instructure.com");
Console.WriteLine("  2. Test        - https://usc-online.test.instructure.com");
Console.WriteLine("  3. Beta        - https://usc-online.beta.instructure.com");
Console.WriteLine("  4. Other Canvas instance");
Console.Write("Enter 1, 2, 3, or 4: ");
var environmentChoice = (Console.ReadLine() ?? "").Trim();
var environmentInput = environmentChoice switch
{
    "1" => "production",
    "2" => "test",
    "3" => "beta",
    "4" => "other",
    _ => ""
};
var canvasBaseUrl = environmentInput switch
{
    "production" => "https://usc-online.instructure.com",
    "test" => "https://usc-online.test.instructure.com",
    "beta" => "https://usc-online.beta.instructure.com",
    "other" => PromptForCanvasUrl(),
    _ => ""
};
if (string.IsNullOrEmpty(environmentInput) || string.IsNullOrEmpty(canvasBaseUrl))
{
    Console.Error.WriteLine("Invalid selection or Canvas URL.");
    return 1;
}

Console.Write("Canvas access token: ");
var token = ReadSecret();
if (token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
    token = token["Bearer ".Length..].Trim();
if (string.IsNullOrWhiteSpace(token))
{
    Console.Error.WriteLine("A Canvas access token is required.");
    return 1;
}

using var httpClient = new HttpClient { BaseAddress = new Uri(canvasBaseUrl) };
httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("USC-Academics-Designer-Tools/1.0");
httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

using (var validationResponse = await httpClient.GetAsync("/api/v1/users/self"))
{
    if (!validationResponse.IsSuccessStatusCode)
    {
        Console.Error.WriteLine($"Canvas rejected the access token with HTTP {(int)validationResponse.StatusCode} ({validationResponse.StatusCode}).");
        Console.Error.WriteLine("Confirm that the token is active, was created in the selected environment, and was pasted without quotes.");
        return 1;
    }
}

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://127.0.0.1:0");
var app = builder.Build();

app.Use(async (context, next) =>
{
    if (!IPAddress.IsLoopback(context.Connection.RemoteIpAddress ?? IPAddress.None))
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return;
    }
    if (HttpMethods.IsOptions(context.Request.Method))
    {
        AddCorsHeaders(context.Response, context.Request.Headers.Origin);
        context.Response.StatusCode = StatusCodes.Status204NoContent;
        return;
    }
    AddCorsHeaders(context.Response, context.Request.Headers.Origin);
    await next();
});

app.MapGet("/api/status", () => Results.Ok(new { connected = !string.IsNullOrEmpty(token), environment = environmentInput, canvasBaseUrl }));
app.MapGet("/api/courses/{courseId}", async (string courseId) => await CanvasProxy($"/api/v1/courses/{Uri.EscapeDataString(courseId)}?include[]=sis_course_id&include[]=account"));
app.MapGet("/api/courses/{courseId}/pages", async (string courseId) => await GetAllPages(courseId));
app.MapPut("/api/courses/{courseId}/pages/{pageUrl}", async (string courseId, string pageUrl, HttpRequest request) =>
{
    using var body = await JsonDocument.ParseAsync(request.Body);
    return await CanvasProxy($"/api/v1/courses/{Uri.EscapeDataString(courseId)}/pages/{Uri.EscapeDataString(pageUrl)}", HttpMethod.Put, body.RootElement.GetRawText());
});
app.MapPost("/api/logout", () =>
{
    httpClient.DefaultRequestHeaders.Authorization = null;
    token = string.Empty;
    return Results.Ok(new { cleared = true });
});

await app.StartAsync();
var server = app.Services.GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>();
var addresses = server.Features.Get<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>()?.Addresses
    ?? throw new InvalidOperationException("The local helper could not determine its listening address.");
var address = addresses.Single();
Console.WriteLine($"Helper running at: {address}");
Console.WriteLine($"Connected Canvas environment: {environmentInput} ({canvasBaseUrl})");
Console.WriteLine();
Console.WriteLine(new string('=', 72));
WriteColor("STEP 2 - CONNECT THE WEBPAGE TO THIS HELPER", ConsoleColor.Green);
Console.WriteLine(new string('=', 72));
Console.WriteLine("In the Whole Page CPG to Canvas Converter webpage:");
Console.WriteLine("  1. Find the field named: Local Canvas helper URL");
WriteColor("  2. COPY AND ENTER EXACTLY THIS URL:", ConsoleColor.Yellow);
Console.WriteLine();
WriteColor($"     {address}", ConsoleColor.Green);
Console.WriteLine();
WriteColor("  3. Click: Connect to Helper", ConsoleColor.Yellow);
Console.WriteLine();
Console.WriteLine("Do not enter the Canvas URL in the webpage field.");
Console.WriteLine("The webpage URL must begin with http://127.0.0.1 and use the port shown above.");
Console.WriteLine();
Console.WriteLine("Leave this window open while using the web tool.");
Console.WriteLine("Press Ctrl+C to stop and clear the in-memory token.");
await app.WaitForShutdownAsync();
return 0;

static void WriteColor(string message, ConsoleColor color)
{
    var previousColor = Console.ForegroundColor;
    Console.ForegroundColor = color;
    Console.WriteLine(message);
    Console.ForegroundColor = previousColor;
}

async Task<IResult> CanvasProxy(string path, HttpMethod? method = null, string? content = null)
{
    using var request = new HttpRequestMessage(method ?? HttpMethod.Get, path);
    if (content is not null) request.Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json");
    using var response = await httpClient.SendAsync(request);
    var responseBody = await response.Content.ReadAsStringAsync();
    return Results.Content(responseBody, response.Content.Headers.ContentType?.ToString() ?? "application/json", statusCode: (int)response.StatusCode);
}

async Task<IResult> GetAllPages(string courseId)
{
    var pages = new List<JsonElement>();
    var next = $"/api/v1/courses/{Uri.EscapeDataString(courseId)}/pages?per_page=100";
    while (!string.IsNullOrEmpty(next))
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, next);
        using var response = await httpClient.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode) return Results.Content(responseBody, "application/json", statusCode: (int)response.StatusCode);
        using var document = JsonDocument.Parse(responseBody);
        pages.AddRange(document.RootElement.EnumerateArray().Select(item => item.Clone()));
        next = response.Headers.TryGetValues("Link", out var links)
            ? ParseNextLink(links)
            : string.Empty;
    }
    return Results.Json(pages);
}

static string ParseNextLink(IEnumerable<string> links)
{
    var match = System.Text.RegularExpressions.Regex.Match(string.Join(",", links), "<([^>]+)>;\\s*rel=\"next\"");
    return match.Success ? new Uri(match.Groups[1].Value).PathAndQuery : string.Empty;
}

static void AddCorsHeaders(HttpResponse response, string? origin)
{
    // `null` is the browser origin for the repository's file:// single-file workflow.
    // The helper remains loopback-only, so this does not expose Canvas outside the local machine.
    if (origin is "null" or "https://usconline.github.io" or "http://localhost:8000" or "http://127.0.0.1:8000")
    {
        response.Headers.AccessControlAllowOrigin = origin;
        response.Headers.AccessControlAllowMethods = "GET,PUT,POST,OPTIONS";
        response.Headers.AccessControlAllowHeaders = "Content-Type";
        response.Headers["Access-Control-Allow-Private-Network"] = "true";
    }
}

static string ReadSecret()
{
    var value = new System.Text.StringBuilder();
    ConsoleKeyInfo key;
    do
    {
        key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Backspace && value.Length > 0)
        {
            value.Length--;
            Console.Write("\b \b");
        }
        else if (!char.IsControl(key.KeyChar))
        {
            value.Append(key.KeyChar);
            Console.Write('*');
        }
    } while (key.Key != ConsoleKey.Enter);
    Console.WriteLine();
    return value.ToString();
}

static string PromptForCanvasUrl()
{
    Console.Write("Enter the full Canvas URL (HTTPS): ");
    var value = (Console.ReadLine() ?? "").Trim().TrimEnd('/');
    if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
    {
        Console.Error.WriteLine("The custom Canvas URL must be a valid HTTPS URL without a query string or fragment.");
        return "";
    }
    return uri.ToString().TrimEnd('/');
}
