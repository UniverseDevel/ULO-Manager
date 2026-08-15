using UloManager.Core;

namespace UloManager.Gui;

/// <summary>An endpoint offered in the console, with a ready-made payload where one is needed.</summary>
public sealed record ApiEndpoint(string Method, string Path, string? Example = null, string? Description = null)
{
    public bool NeedsBody => Method is "POST" or "PUT" or "PATCH";

    public override string ToString()
    {
        var head = $"{Method} {Path}";
        if (string.IsNullOrWhiteSpace(Description))
        {
            return head;
        }

        // The dropdown is opened wide (see DropDownWidth), but a very long note still has to stop
        // somewhere.
        var note = Description.Length > 90 ? Description[..90] + "..." : Description;
        return $"{head}  -  {note}";
    }
}

public sealed partial class MainForm
{
    private TabPage BuildApiTab()
    {
        var page = new TabPage("API console") { Padding = new Padding(10) };

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 130));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // Request line
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };

        toolbar.Controls.Add(new Label { Text = "Method", AutoSize = true, Margin = new Padding(0, 8, 4, 0) });
        _methodBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90 };
        _methodBox.Items.AddRange(new object[] { "GET", "PUT", "POST", "PATCH", "DELETE" });
        _methodBox.SelectedIndex = 0;
        toolbar.Controls.Add(_methodBox);

        toolbar.Controls.Add(new Label { Text = "Path", AutoSize = true, Margin = new Padding(10, 8, 4, 0) });
        _pathBox = new TextBox { Width = 420, Text = "api/v1/state" };
        toolbar.Controls.Add(_pathBox);

        var send = new Button { Text = "Send", Width = 90, Margin = new Padding(10, 0, 0, 0) };
        send.Click += async (_, _) => await SendApiAsync();
        toolbar.Controls.Add(send);

        // Pickers
        var pickers = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };

        pickers.Controls.Add(new Label { Text = "Known endpoints", AutoSize = true, Margin = new Padding(0, 8, 4, 0) });

        // Probing usually happens one method at a time, so the list can be narrowed to it.
        _methodFilterBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 80, Margin = new Padding(0, 3, 4, 3) };
        _methodFilterBox.Items.AddRange(new object[] { "All", "GET", "POST", "PUT", "PATCH", "DELETE" });
        _methodFilterBox.SelectedIndex = 0;
        _methodFilterBox.SelectedIndexChanged += (_, _) => RefreshKnownEndpoints();
        pickers.Controls.Add(_methodFilterBox);

        // Ninety-odd endpoints are too many to find by eye - type any part of a path or of its
        // description ("fota", "media", "picture") and the list narrows to it.
        _endpointSearchBox = new TextBox
        {
            Width = 120,
            PlaceholderText = "search...",
            Margin = new Padding(0, 3, 4, 3),
        };
        _endpointSearchBox.TextChanged += (_, _) => RefreshKnownEndpoints();
        pickers.Controls.Add(_endpointSearchBox);

        _knownBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 300,
            // The list itself opens far wider than the box so the description stays readable.
            DropDownWidth = 900,
            Margin = new Padding(0, 3, 10, 3),
        };
        _knownBox.SelectedIndexChanged += (_, _) => ApplyKnownEndpoint();
        pickers.Controls.Add(_knownBox);

        var fillFromDevice = new Button { Text = "Body from current value", Width = 160, Margin = new Padding(0, 3, 0, 3) };
        fillFromDevice.Click += async (_, _) => await FillBodyFromDeviceAsync();
        pickers.Controls.Add(fillFromDevice);

        // Body and response
        _bodyBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Consolas", 9F),
            PlaceholderText = "Request body (JSON). Pick a known endpoint for an example - the payload this application last sent to it is reused when there is one.",
        };

        _responseBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            ReadOnly = true,
            WordWrap = false,
            Font = new Font("Consolas", 9F),
            BackColor = SystemColors.Window,
            ForeColor = SystemColors.WindowText,
        };

        layout.Controls.Add(toolbar, 0, 0);
        layout.Controls.Add(pickers, 0, 1);
        layout.Controls.Add(_bodyBox, 0, 2);
        layout.Controls.Add(_responseBox, 0, 3);

        page.Controls.Add(layout);
        return page;
    }

    /// <summary>Applies the selected known endpoint, including a payload where one is needed.</summary>
    /// <summary>
    /// Fills the endpoint picker from the registry for the firmware of the camera in use, so a
    /// camera on 06.0601 never offers a call that only exists on 10.1308 and the other way round.
    /// Entries the registry knows are marked as absent on this firmware are left out; example
    /// bodies come from the table below.
    /// </summary>
    private void RefreshKnownEndpoints()
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            Invoke(RefreshKnownEndpoints);
            return;
        }

        var firmware = _device?.FirmwareVersion ?? default;
        var filter = _methodFilterBox.SelectedItem as string ?? "All";
        var search = _endpointSearchBox.Text.Trim();

        var fromRegistry = UloEndpointRegistry.ForFirmware(firmware)
            .Where(e => e.Method is "GET" or "POST" or "PUT" or "PATCH" or "DELETE")
            .Select(e => new ApiEndpoint(e.Method, e.Path, ExampleFor(e.Method, e.Path), e.Description));

        // Anything in the local table the registry does not know about is still offered.
        var extras = KnownEndpoints.Where(k =>
            !UloEndpointRegistry.All.Any(e =>
                string.Equals(e.Path, k.Path, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(e.Method, k.Method, StringComparison.OrdinalIgnoreCase)));

        var endpoints = fromRegistry
            .Concat(extras)
            .Where(e => filter == "All" || string.Equals(e.Method, filter, StringComparison.OrdinalIgnoreCase))
            .Where(e => search.Length == 0 ||
                        e.Path.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        (e.Description ?? "").Contains(search, StringComparison.OrdinalIgnoreCase))
            .GroupBy(e => e.Method + " " + e.Path)
            .Select(g => g.First())
            // Grouped by method - all the GETs together - and alphabetical inside each group.
            .OrderBy(e => MethodOrder(e.Method))
            .ThenBy(e => e.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        _knownBox.BeginUpdate();
        _knownBox.Items.Clear();
        _knownBox.Items.AddRange(endpoints.Cast<object>().ToArray());
        _knownBox.EndUpdate();

        if (search.Length > 0)
        {
            SetStatus($"{endpoints.Length} endpoint(s) match '{search}'.");
        }
    }

    private static int MethodOrder(string method) => method switch
    {
        "GET" => 0,
        "POST" => 1,
        "PUT" => 2,
        "PATCH" => 3,
        "DELETE" => 4,
        _ => 5,
    };

    /// <summary>
    /// Turns the placeholders in a registry path into something that can be sent as it stands.
    /// <c>{day}</c> becomes the day according to the <b>camera's</b> clock - which is not always this
    /// machine's date, the camera drifts and resets to 01/01/70 after a reboot - and <c>{id}</c>
    /// becomes the account that is signed in.
    /// </summary>
    private string FillPlaceholders(string path)
    {
        if (!path.Contains('{'))
        {
            return path;
        }

        var cameraTime = _monitor?.Latest?.DeviceTime ?? _info?.DeviceTime ?? DateTime.Now;
        if (cameraTime == DateTime.MinValue)
        {
            cameraTime = DateTime.Now;
        }

        var userId = _info?.CurrentUser.Id ?? _device?.Client.UserId ?? 1;

        return path
            .Replace("{day}", cameraTime.ToString("yyyyMMdd"))
            .Replace("{id}", userId.ToString());
    }

    private static string? ExampleFor(string method, string path)
        => KnownEndpoints.FirstOrDefault(k =>
            string.Equals(k.Method, method, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(k.Path, path, StringComparison.OrdinalIgnoreCase))?.Example;

    private void ApplyKnownEndpoint()
    {
        if (_knownBox.SelectedItem is not ApiEndpoint endpoint)
        {
            return;
        }

        _methodBox.SelectedItem = _methodBox.Items.Contains(endpoint.Method) ? endpoint.Method : "GET";
        _pathBox.Text = FillPlaceholders(endpoint.Path);

        if (!endpoint.NeedsBody)
        {
            _bodyBox.Text = string.Empty;
            return;
        }

        // A payload this application actually sent beats a hand-written example.
        var recorded = _recorder.LastBodyFor(endpoint.Method, endpoint.Path);

        _bodyBox.Text = (recorded ?? endpoint.Example ?? "{}").Replace("\n", Environment.NewLine);
        SetStatus(recorded is not null
            ? $"Payload taken from a recorded {endpoint.Method} {endpoint.Path}."
            : $"Example payload for {endpoint.Method} {endpoint.Path}.");
    }

    /// <summary>
    /// Reads the current value of the path with GET and puts it in the body box, which turns any
    /// readable resource into a ready-to-edit payload for the matching PUT.
    /// </summary>
    private async Task FillBodyFromDeviceAsync()
    {
        await RunAsync("Reading the current value", async ct =>
        {
            var response = await RequireDevice().CallApiAsync(_pathBox.Text.Trim(), "GET", null, ct);
            _bodyBox.Text = UloJson.Pretty(response).Replace("\n", Environment.NewLine);

            if ((string?)_methodBox.SelectedItem == "GET")
            {
                _methodBox.SelectedItem = "PUT";
            }
        });
    }

    private async Task SendApiAsync()
    {
        await RunAsync("Calling the camera API", async ct =>
        {
            var method = (string)_methodBox.SelectedItem!;
            var body = string.IsNullOrWhiteSpace(_bodyBox.Text) ? null : _bodyBox.Text;
            var response = await RequireDevice().CallApiAsync(_pathBox.Text.Trim(), method, body, ct);

            _responseBox.Text = UloJson.Pretty(response).Replace("\n", Environment.NewLine);
        });
    }

    /// <summary>
    /// Every endpoint confirmed on the camera, by sweeping it with OPTIONS (it answers with
    /// <c>Access-Control-Allow-Methods</c>) and by reading the <c>ApiPaths</c> table out of the
    /// camera's own web application. Payload examples show the shape the camera accepts; where this
    /// application has already sent one, the recorded payload is offered instead.
    /// <para>
    /// Not listed, because they are not HTTP: the event channel <c>ws://&lt;host&gt;/api/v1</c> and
    /// the live video <c>ws://&lt;host&gt;/api/v1/live</c>.
    /// </para>
    /// </summary>
    private static readonly ApiEndpoint[] KnownEndpoints =
    {
        // Session
        new("POST", "api/v1/login", "{ \"iOSAgent\": false }"),
        new("POST", "api/v1/logout", "{}"),

        // Day to day operation
        new("GET", "api/v1/state"),
        new("GET", "api/v1/mode"),
        new("PUT", "api/v1/mode", "{ \"mode\": \"alert\" }"),
        new("GET", "api/v1/time"),
        new("PUT", "api/v1/time", "{ \"time\": \"2026-01-31T18:45:00\" }"),
        new("POST", "api/v1/snapshot", "{ \"savePicture\": 0 }"),
        new("GET", "api/v1/record"),
        new("PUT", "api/v1/record", "{ \"running\": true }"),

        // Configuration
        new("GET", "api/v1/config"),
        new("PUT", "api/v1/config", "{ \"device\": { \"name\": \"Ulo\" } }"),
        new("GET", "api/v1/config/access"),
        new("PUT", "api/v1/config/access", "{ \"accountId\": \"\", \"type\": \"private\" }"),
        new("GET", "api/v1/config/alert"),
        new("PUT", "api/v1/config/alert",
            "{\n  \"disableOnAppRequest\": true,\n  \"disableOnDoubleTap\": true,\n  \"disableOnRecognizedUser\": false\n}"),
        new("GET", "api/v1/config/device"),
        new("PUT", "api/v1/config/device", "{ \"name\": \"Ulo\" }"),
        new("GET", "api/v1/config/email"),
        new("PUT", "api/v1/config/email",
            "{\n  \"login\": \"\",\n  \"password\": \"\",\n  \"port\": 25,\n  \"server\": \"\",\n  \"ssl\": false\n}"),
        new("GET", "api/v1/config/exclusion"),
        new("PUT", "api/v1/config/exclusion",
            "{\n  \"top\": 0,\n  \"left\": 0,\n  \"bottom\": 100,\n  \"right\": 100,\n  \"reverse\": false,\n  \"resetOnDisplacement\": false\n}"),
        new("GET", "api/v1/config/eyes"),
        new("PUT", "api/v1/config/eyes",
            "{\n  \"irisHue\": 201,\n  \"irisSize\": 85,\n  \"pupilSize\": 55,\n  \"reflection\": \"circles\"\n}"),
        new("GET", "api/v1/config/face"),
        new("PUT", "api/v1/config/face",
            "{\n  \"alert\": false,\n  \"battery\": false,\n  \"spy\": false,\n  \"standard\": false\n}"),
        new("GET", "api/v1/config/firmware"),
        new("PUT", "api/v1/config/firmware", "{ \"firmwareStatus\": \"update\" }"),
        new("GET", "api/v1/config/language"),
        new("PUT", "api/v1/config/language", "{ \"language\": \"en\" }"),
        new("GET", "api/v1/config/language/languages"),
        new("GET", "api/v1/config/time"),
        new("PUT", "api/v1/config/time", "{ \"auto\": true, \"timeZone\": \"Europe/Bratislava\" }"),
        new("GET", "api/v1/config/time/countries"),
        new("POST", "api/v1/config/time/zones", "{ \"code\": \"SK\" }"),
        new("GET", "api/v1/config/video"),
        new("PUT", "api/v1/config/video", "{ \"quality\": \"720p\" }"),
        new("GET", "api/v1/config/voice"),
        new("PUT", "api/v1/config/voice",
            "{\n  \"alert\": false,\n  \"battery\": false,\n  \"spy\": false,\n  \"standard\": false,\n" +
            "  \"commands\": {\n    \"alertOff\": true,\n    \"alertOn\": true,\n    \"goToSleep\": true,\n" +
            "    \"startVideo\": true,\n    \"stopVideo\": true,\n    \"takePicture\": true\n  }\n}"),
        new("GET", "api/v1/config/wifi"),
        new("PUT", "api/v1/config/wifi", "{ \"ssid\": \"MyNetwork\", \"password\": \"...\" }"),
        new("GET", "api/v1/config/wifi/networks"),
        new("GET", "api/v1/config/reset"),

        // Accounts
        new("GET", "api/v1/users"),
        new("POST", "api/v1/users",
            "{\n  \"email\": \"user@example.com\",\n  \"name\": \"user@example.com\",\n" +
            "  \"password\": \"...\",\n  \"account\": \"user\"\n}"),
        new("GET", "api/v1/users/{id}"),
        new("PUT", "api/v1/users/{id}",
            "{\n  \"id\": 1,\n  \"email\": \"user@example.com\",\n  \"name\": \"user@example.com\",\n" +
            "  \"emailAlert\": false,\n  \"emailSpy\": false,\n  \"pushAlert\": false,\n  \"pushSpy\": false\n}"),
        new("DELETE", "api/v1/users/{id}"),
        new("PUT", "api/v1/users/{id}/notifications",
            "{\n  \"notifications\": [\n    { \"object\": \"movement\", \"enabledInAlert\": true, \"enabledInSpy\": false }\n  ]\n}"),
        new("GET", "api/v1/users/{id}/devices"),

        // Recordings and storage
        new("GET", "api/v1/files/media"),
        new("DELETE", "api/v1/files/media"),
        new("GET", "api/v1/files/media/{day}"),
        new("DELETE", "api/v1/files/media/{day}"),
        new("GET", "api/v1/files/media/{day}/count"),
        new("GET", "api/v1/files/directoryCount"),
        new("GET", "api/v1/files/stats"),
        new("GET", "api/v1/files/backup"),
        new("PUT", "api/v1/files/backup?filename=all", "{ \"running\": true }"),
        new("DELETE", "api/v1/files/delete?removeType=0"),

        // System
        new("GET", "api/v1/system/log"),
        new("GET", "api/v1/system/backups"),
        new("POST", "api/v1/system/backup", "{}"),
        new("POST", "api/v1/system/restore", "{ \"name\": \"...\" }"),
        new("POST", "api/v1/system/reset", "{}"),

        // Firmware over the air
        new("GET", "api/v1/interface/fotaStatus"),
        new("GET", "api/v1/interface/fotaNumberOfUpdates"),
        new("GET", "api/v1/interface/fotaIsInstallAvailable"),
        new("GET", "api/v1/interface/fotaStartDownload"),
        new("POST", "api/v1/interface/fotaInstallFirmware", "{}"),

        // Present in the firmware, unused by any known client - payloads unknown
        new("GET", "api/v1/behaviors"),
        new("POST", "api/v1/behaviors", "{}"),
        new("GET", "api/v1/neighbors"),
        new("POST", "api/v1/admin", "{}"),
        new("GET", "api/v1/import"),
    };

    private ComboBox _methodBox = null!;
    private ComboBox _methodFilterBox = null!;
    private TextBox _endpointSearchBox = null!;
    private TextBox _pathBox = null!;
    private ComboBox _knownBox = null!;
    private TextBox _bodyBox = null!;
    private TextBox _responseBox = null!;
}
