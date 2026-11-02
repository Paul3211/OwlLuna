using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace OwlLuna.Windows;

public sealed class GoogleDrive
{
    readonly string tokenPath, clientPath;
    readonly HttpClient http = new() { Timeout = TimeSpan.FromMinutes(10) };
    JsonObject? token;
    DateTimeOffset expires;
    public bool Connected => File.Exists(tokenPath);
    public bool Configured => File.Exists(clientPath);
    public GoogleDrive(string root) {
        Directory.CreateDirectory(root);
        tokenPath = Path.Combine(root, "google-token.protected"); clientPath = Path.Combine(root, "google-client.protected");
        var bundled = Path.Combine(AppContext.BaseDirectory, "google-client.protected");
        if (!File.Exists(clientPath) && File.Exists(bundled) && Path.GetFullPath(clientPath) != Path.GetFullPath(bundled)) Json.Atomic(clientPath, File.ReadAllBytes(bundled));
    }
    static byte[] Protect(byte[] bytes) => ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
    static byte[] Unprotect(byte[] bytes) => ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser);
    public void Configure(string file)
    {
        var root = JsonNode.Parse(File.ReadAllText(file))?.AsObject() ?? throw new InvalidDataException("Keine OAuth-Konfiguration.");
        var client = root["installed"]?.AsObject() ?? throw new InvalidDataException("Bitte eine Google-OAuth-Konfiguration vom Typ Desktop-App auswählen.");
        if (!(client["client_id"]?.GetValue<string>()?.EndsWith(".apps.googleusercontent.com") ?? false)) throw new InvalidDataException("Ungültige Google Client-ID.");
        SignOut();
        Json.Atomic(clientPath, Protect(Encoding.UTF8.GetBytes(client.ToJsonString())));
    }
    JsonObject Client() => JsonNode.Parse(Unprotect(File.ReadAllBytes(clientPath)))!.AsObject();
    static string Random() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    public async Task SignIn()
    {
        if (!Configured) throw new InvalidOperationException("Zuerst die OAuth-Desktop-Konfiguration importieren. Eine echte Google-Anmeldung benötigt eine registrierte Client-ID.");
        var client = Client(); var verifier = Random(); var state = Random();
        var challenge = Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        try
        {
            var redirect = $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/";
            var auth = "https://accounts.google.com/o/oauth2/v2/auth?" + Query(new() {
                ["client_id"] = client["client_id"]!.GetValue<string>(), ["redirect_uri"] = redirect,
                ["response_type"] = "code", ["scope"] = "https://www.googleapis.com/auth/drive.file",
                ["state"] = state, ["code_challenge"] = challenge, ["code_challenge_method"] = "S256",
                ["access_type"] = "offline", ["prompt"] = "consent"
            });
            Process.Start(new ProcessStartInfo(auth) { UseShellExecute = true });
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            string? code = null;
            while (code is null)
            {
                using var connection = await listener.AcceptTcpClientAsync(cancellation.Token);
                using var stream = connection.GetStream(); using var reader = new StreamReader(stream, leaveOpen: true);
                var firstLine = await reader.ReadLineAsync(cancellation.Token) ?? "";
                if (firstLine.Length > 16000) throw new InvalidDataException("Ungültige OAuth-Antwort.");
                int headerBytes = firstLine.Length; string? headerLine;
                while (!string.IsNullOrEmpty(headerLine = await reader.ReadLineAsync(cancellation.Token))) {
                    headerBytes += headerLine.Length;
                    if (headerBytes > 64000) throw new InvalidDataException("Zu große OAuth-Antwort.");
                }
                var path = firstLine.Split(' ').ElementAtOrDefault(1) ?? "/";
                var uri = new Uri("http://127.0.0.1" + path);
                var values = uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries).Select(p => p.Split('=', 2)).ToDictionary(p => Uri.UnescapeDataString(p[0]), p => Uri.UnescapeDataString(p.ElementAtOrDefault(1) ?? ""));
                bool valid = values.GetValueOrDefault("state") == state;
                var message = valid ? "Anmeldung empfangen. Du kannst zu OwlLuna zurückkehren." : "Keine passende Anmeldung.";
                var body = Encoding.UTF8.GetBytes(message);
                var header = Encoding.ASCII.GetBytes($"HTTP/1.1 {(valid ? "200 OK" : "400 Bad Request")}\r\nContent-Type: text/plain; charset=utf-8\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
                await stream.WriteAsync(header, cancellation.Token); await stream.WriteAsync(body, cancellation.Token);
                if (!valid) continue;
                if (values.ContainsKey("error")) throw new InvalidOperationException("Die Google-Anmeldung wurde abgebrochen.");
                code = values.GetValueOrDefault("code");
            }
            token = await Token(new() { ["client_id"] = client["client_id"]!.GetValue<string>(), ["client_secret"] = client["client_secret"]?.GetValue<string>() ?? "", ["code"] = code, ["code_verifier"] = verifier, ["redirect_uri"] = redirect, ["grant_type"] = "authorization_code" });
            SaveToken();
        }
        finally { listener.Stop(); }
    }
    static string Query(Dictionary<string, string> values) => string.Join("&", values.Select(v => Uri.EscapeDataString(v.Key) + "=" + Uri.EscapeDataString(v.Value)));
    async Task<JsonObject> Token(Dictionary<string, string> values)
    {
        using var response = await http.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(values));
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Google konnte die Anmeldung nicht bestätigen. OAuth-Konfiguration und Testnutzer prüfen oder erneut anmelden.");
        var result = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
        expires = DateTimeOffset.UtcNow.AddSeconds(result["expires_in"]?.GetValue<int>() ?? 3600); return result;
    }
    void SaveToken() => Json.Atomic(tokenPath, Protect(Encoding.UTF8.GetBytes(token!.ToJsonString())));
    public void SignOut() { if (File.Exists(tokenPath)) File.Delete(tokenPath); token = null; expires = default; }
    async Task Authorize(HttpRequestMessage request)
    {
        token ??= JsonNode.Parse(Unprotect(File.ReadAllBytes(tokenPath)))!.AsObject();
        if (expires < DateTimeOffset.UtcNow.AddMinutes(1))
        {
            var client = Client(); var refresh = token["refresh_token"]?.GetValue<string>() ?? throw new InvalidOperationException("Bitte erneut bei Google anmelden.");
            token = await Token(new() { ["client_id"] = client["client_id"]!.GetValue<string>(), ["client_secret"] = client["client_secret"]?.GetValue<string>() ?? "", ["refresh_token"] = refresh, ["grant_type"] = "refresh_token" });
            token["refresh_token"] = refresh; SaveToken();
        }
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token["access_token"]!.GetValue<string>());
    }
    async Task<JsonObject> Send(HttpRequestMessage request)
    {
        using (request)
        {
            await Authorize(request); using var response = await http.SendAsync(request);
            if (!response.IsSuccessStatusCode) throw new IOException($"Google Drive meldet HTTP {(int)response.StatusCode}. Lokal gespeicherte Notizen sind weiterhin verfügbar.");
            return JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
        }
    }
    public async Task<string> UploadBackup(string file, Settings settings)
    {
        var folderId = settings.DriveFolderId;
        if (folderId is null)
        {
            var folder = await Send(new(HttpMethod.Post, "https://www.googleapis.com/drive/v3/files?fields=id") { Content = new StringContent(new JsonObject { ["name"] = "OwlLuna Backups", ["mimeType"] = "application/vnd.google-apps.folder" }.ToJsonString(), Encoding.UTF8, "application/json") });
            folderId = settings.DriveFolderId = folder["id"]!.GetValue<string>();
        }
        using var multipart = new MultipartContent("related");
        multipart.Add(new StringContent(new JsonObject { ["name"] = Path.GetFileName(file), ["parents"] = new JsonArray(folderId), ["appProperties"] = new JsonObject { ["owlLuna"] = "backup", ["sha256"] = Json.Hash(File.ReadAllBytes(file)) } }.ToJsonString(), Encoding.UTF8, "application/json"));
        var media = new StreamContent(File.OpenRead(file)); media.Headers.ContentType = new("application/zip"); multipart.Add(media);
        var result = await Send(new(HttpMethod.Post, "https://www.googleapis.com/upload/drive/v3/files?uploadType=multipart&fields=id") { Content = multipart });
        return result["id"]!.GetValue<string>();
    }
    public async Task<List<(string Id, string Name)>> ListBackups()
    {
        var result = await Send(new(HttpMethod.Get, "https://www.googleapis.com/drive/v3/files?" + Query(new() { ["q"] = "trashed=false and appProperties has { key='owlLuna' and value='backup' }", ["orderBy"] = "createdTime desc", ["pageSize"] = "100", ["fields"] = "files(id,name)" })));
        return result["files"]!.AsArray().Select(n => (n!["id"]!.GetValue<string>(), n["name"]!.GetValue<string>())).ToList();
    }
    public async Task Download(string id, string target)
    {
        var metadata = await Send(new(HttpMethod.Get, "https://www.googleapis.com/drive/v3/files/" + Uri.EscapeDataString(id) + "?fields=appProperties"));
        var expectedHash = metadata["appProperties"]?["sha256"]?.GetValue<string>() ?? throw new InvalidDataException("Dieses Backup hat keine Prüfsumme und wird nicht automatisch wiederhergestellt.");
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://www.googleapis.com/drive/v3/files/" + Uri.EscapeDataString(id) + "?alt=media");
        await Authorize(request); using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        using var stream = await response.Content.ReadAsStreamAsync(); using var bytes = new MemoryStream();
        var buffer = new byte[65536]; int read;
        while ((read = await stream.ReadAsync(buffer)) > 0) { if (bytes.Length + read > 512L * 1024 * 1024) throw new IOException("Backup größer als 512 MB."); bytes.Write(buffer, 0, read); }
        var data = bytes.ToArray();
        if (!string.Equals(Json.Hash(data), expectedHash, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Die Prüfsumme des Google-Backups stimmt nicht.");
        Json.Atomic(target, data); Library.ValidateBackup(target);
    }
}
