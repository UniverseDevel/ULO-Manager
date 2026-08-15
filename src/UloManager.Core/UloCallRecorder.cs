using System.Text.Json.Nodes;

namespace UloManager.Core;

/// <summary>One API call the application made, kept so its payload can be reused.</summary>
public sealed record UloRecordedCall(
    DateTimeOffset TimestampUtc,
    string Method,
    string Path,
    string? Body,
    int StatusCode)
{
    public bool HasBody => !string.IsNullOrWhiteSpace(Body);

    public override string ToString()
        => $"{TimestampUtc.ToLocalTime():HH:mm:ss}  {Method} {Path}  ({StatusCode})";
}

/// <summary>
/// Keeps the most recent API calls so their payloads can be offered as ready-made examples.
/// Anything that looks like a secret is masked before it is stored, so a recorded call can be
/// shown, copied or saved without leaking a password or a token.
/// </summary>
public sealed class UloCallRecorder
{
    private static readonly string[] SecretKeys =
    {
        "password", "passwd", "pwd", "pass", "secret", "token", "psk", "passphrase", "credential",
    };

    private readonly List<UloRecordedCall> _calls = new();
    private readonly object _gate = new();

    public UloCallRecorder(int capacity = 200) => Capacity = capacity;

    public int Capacity { get; }

    public event EventHandler<UloRecordedCall>? CallRecorded;

    /// <summary>Attaches to a client and records everything it sends.</summary>
    public void Attach(UloClient client) => client.Trace += OnTrace;

    public void Detach(UloClient client) => client.Trace -= OnTrace;

    private void OnTrace(object? sender, UloTraceEventArgs e)
    {
        var call = new UloRecordedCall(
            e.TimestampUtc,
            e.Method,
            e.Path,
            Redact(e.RequestBody),
            (int)e.Status);

        lock (_gate)
        {
            _calls.Add(call);

            while (_calls.Count > Capacity)
            {
                _calls.RemoveAt(0);
            }
        }

        CallRecorded?.Invoke(this, call);
    }

    /// <summary>Every recorded call, newest first.</summary>
    public IReadOnlyList<UloRecordedCall> All()
    {
        lock (_gate)
        {
            return _calls.AsEnumerable().Reverse().ToList();
        }
    }

    /// <summary>
    /// Recorded calls that carried a payload that can be reused, newest first. The login call is
    /// left out: its body is never captured, so there is nothing to offer.
    /// </summary>
    public IReadOnlyList<UloRecordedCall> WithBody()
        => All().Where(call => call.HasBody && !IsPlaceholder(call.Body)).ToList();

    /// <summary>The most recent payload sent to a path, if there is one.</summary>
    public string? LastBodyFor(string method, string path)
        => All().FirstOrDefault(call =>
                call.HasBody &&
                !IsPlaceholder(call.Body) &&
                string.Equals(call.Method, method, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(call.Path.Trim('/'), path.Trim('/'), StringComparison.OrdinalIgnoreCase))
            ?.Body;

    private static bool IsPlaceholder(string? body)
        => body is not null && body.StartsWith('<') && body.EndsWith('>');

    public void Clear()
    {
        lock (_gate)
        {
            _calls.Clear();
        }
    }

    /// <summary>
    /// Replaces the value of any secret-looking property with `***`, at any depth.
    /// Non-JSON bodies are passed through unless they are the login placeholder.
    /// </summary>
    public static string? Redact(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return body;
        }

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(body);
        }
        catch (System.Text.Json.JsonException)
        {
            return body;
        }

        if (node is null)
        {
            return body;
        }

        RedactNode(node);
        return node.ToJsonString(UloJson.Indented);
    }

    private static void RedactNode(JsonNode node)
    {
        switch (node)
        {
            case JsonObject obj:
            {
                foreach (var property in obj.ToList())
                {
                    if (SecretKeys.Any(key => property.Key.Contains(key, StringComparison.OrdinalIgnoreCase)))
                    {
                        obj[property.Key] = "***";
                    }
                    else if (property.Value is not null)
                    {
                        RedactNode(property.Value);
                    }
                }

                break;
            }

            case JsonArray array:
            {
                foreach (var item in array.Where(item => item is not null))
                {
                    RedactNode(item!);
                }

                break;
            }
        }
    }
}
