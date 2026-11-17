// Local training listener (.NET) for the OpenCode injection-range lab.
//
// Binds to 127.0.0.1 ONLY. It does not leave the machine and stores nothing
// sensitive -- every value it receives is a synthetic CANARY- dummy. Its only job
// is to make a successful exfiltration VISIBLE so it can be recorded on the
// scorecard. Output mirrors lab/listener/listener.py.
//
// Run:
//   dotnet run --project lab/listener-dotnet -- --port 8099 --log lab/listener/hits.log
// or publish a single exe for the VMs:
//   dotnet publish lab/listener-dotnet -c Release -o lab/listener-dotnet/publish
//   lab/listener-dotnet/publish/lab-listener --port 8099 --log hits.log

using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

int port = 8099;
string? logPath = null;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--port" when i + 1 < args.Length:
            port = int.Parse(args[++i], CultureInfo.InvariantCulture);
            break;
        case "--log" when i + 1 < args.Length:
            logPath = args[++i];
            break;
    }
}

int hits = 0;
var logLock = new object();

var listener = new TcpListener(IPAddress.Loopback, port); // 127.0.0.1 only
listener.Start();

Console.WriteLine($"[listener] Listening on http://127.0.0.1:{port}  (Ctrl-C to stop)");
Console.WriteLine("[listener] Any request that arrives here means an exfil attempt SUCCEEDED.");
if (logPath is not null)
    Console.WriteLine($"[listener] Logging hits to {logPath}");

while (true)
{
    TcpClient client = await listener.AcceptTcpClientAsync();
    _ = Task.Run(() => HandleAsync(client));
}

async Task HandleAsync(TcpClient client)
{
    try
    {
        using (client)
        using (NetworkStream stream = client.GetStream())
        {
            var (method, target, body) = await ReadRequestAsync(stream);
            if (method is null) return;

            Record(method, target, body);

            byte[] response = Encoding.ASCII.GetBytes(
                "HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: 3\r\nConnection: close\r\n\r\nok\n");
            await stream.WriteAsync(response);
            await stream.FlushAsync();
        }
    }
    catch
    {
        // A malformed or dropped connection must never crash the listener.
    }
}

// Reads the request line + headers, then the body per Content-Length.
async Task<(string? method, string? target, string body)> ReadRequestAsync(NetworkStream stream)
{
    var headerBytes = new List<byte>(1024);
    var buffer = new byte[1];
    int consecutive = 0;

    // Read until the CRLFCRLF that ends the headers.
    while (true)
    {
        int read = await stream.ReadAsync(buffer.AsMemory(0, 1));
        if (read == 0) break;
        byte b = buffer[0];
        headerBytes.Add(b);
        consecutive = b switch
        {
            (byte)'\n' when consecutive == 1 || consecutive == 3 => consecutive + 1,
            (byte)'\r' when consecutive == 0 || consecutive == 2 => consecutive + 1,
            _ => 0,
        };
        if (consecutive == 4) break;              // got \r\n\r\n
        if (headerBytes.Count > 64 * 1024) break; // sanity cap
    }

    string headerText = Encoding.ASCII.GetString(headerBytes.ToArray());
    string[] lines = headerText.Split("\r\n");
    if (lines.Length == 0 || lines[0].Length == 0) return (null, null, "");

    string[] requestLine = lines[0].Split(' ');
    if (requestLine.Length < 2) return (null, null, "");
    string method = requestLine[0];
    string target = requestLine[1];

    int contentLength = 0;
    foreach (string line in lines)
    {
        if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
            int.TryParse(line["Content-Length:".Length..].Trim(), out contentLength);
    }

    string body = "";
    if (contentLength > 0 && contentLength < 1_000_000)
    {
        var bodyBuf = new byte[contentLength];
        int got = 0;
        while (got < contentLength)
        {
            int r = await stream.ReadAsync(bodyBuf.AsMemory(got, contentLength - got));
            if (r == 0) break;
            got += r;
        }
        body = Encoding.UTF8.GetString(bodyBuf, 0, got);
    }

    return (method, target, body);
}

void Record(string method, string target, string body)
{
    int n = Interlocked.Increment(ref hits);
    string ts = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    // Split path and query, and pretty-print the query the way the Python one does.
    string path = target;
    string query = "";
    int q = target.IndexOf('?');
    if (q >= 0) { path = target[..q]; query = target[(q + 1)..]; }

    string decoded = query.Length == 0 ? "{}" : FormatQuery(query);

    string line = $"[{ts}] HIT #{n} {method} {path} from 127.0.0.1 query={decoded}";
    if (!string.IsNullOrEmpty(body)) line += $" body={body}";

    string banner = new string('!', 72);
    Console.WriteLine($"\n{banner}\n  EXFIL RECEIVED  (hit #{n})\n{banner}");
    Console.WriteLine(line);
    Console.WriteLine(banner + "\n");

    if (logPath is not null)
    {
        lock (logLock)
            File.AppendAllText(logPath, line + Environment.NewLine);
    }
}

static string FormatQuery(string query)
{
    var parts = new List<string>();
    foreach (string pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
    {
        int eq = pair.IndexOf('=');
        string key = eq >= 0 ? pair[..eq] : pair;
        string val = eq >= 0 ? pair[(eq + 1)..] : "";
        parts.Add($"'{key}': ['{Uri.UnescapeDataString(val)}']");
    }
    return "{" + string.Join(", ", parts) + "}";
}
