using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;

namespace OwlLuna.Windows;

public sealed class InkPoint
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Pressure { get; set; } = .5;
    public double TiltX { get; set; }
    public double TiltY { get; set; }
    public double Time { get; set; }
}
public sealed class InkStroke
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Tool { get; set; } = "pen";
    public string Color { get; set; } = "#243B53";
    public double Width { get; set; } = 2.5;
    public List<InkPoint> Points { get; set; } = [];
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}
public sealed class PageItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Kind { get; set; } = "text";
    public string Text { get; set; } = "";
    public string? Image { get; set; }
    public double X { get; set; } = 60;
    public double Y { get; set; } = 60;
    public double Width { get; set; } = 240;
    public double Height { get; set; } = 100;
    public double FontSize { get; set; } = 18;
    public string Color { get; set; } = "#243B53";
    public double Rotation { get; set; }
    public bool Bold { get; set; }
    public bool Italic { get; set; }
    public string FontFamily { get; set; } = "Segoe UI";
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}
public sealed class NotePage
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = "";
    public double Width { get; set; } = 595;
    public double Height { get; set; } = 842;
    public string Paper { get; set; } = "grid";
    public string PaperColor { get; set; } = "#FFFEF9";
    public string? BackgroundImage { get; set; }
    public string? BackgroundPdf { get; set; }
    public int PdfIndex { get; set; }
    public bool Bookmark { get; set; }
    public string IndexText { get; set; } = "";
    public List<InkStroke> Strokes { get; set; } = [];
    public List<PageItem> Items { get; set; } = [];
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}
public sealed class NoteBook
{
    public int Schema { get; set; } = 1;
    public string Format { get; set; } = "org.owlluna.portable";
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Revision { get; set; } = Guid.NewGuid().ToString();
    public List<string> Ancestors { get; set; } = [];
    public string Title { get; set; } = "Neues Heft";
    public string Folder { get; set; } = "";
    public string CoverColor { get; set; } = "#537264";
    public string Tags { get; set; } = "";
    public bool Favorite { get; set; }
    public bool Deleted { get; set; }
    public DateTimeOffset ModifiedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<NotePage> Pages { get; set; } = [new()];
    public List<StudyCard> Cards { get; set; } = [];
    public List<AudioNote> Recordings { get; set; } = [];
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
    [JsonIgnore] public string Label => (Favorite ? "★  " : "") + Title;
    public NoteBook Clone() => Json.Read<NoteBook>(Json.Bytes(this));
}
public sealed class StudyCard
{
    public string Question { get; set; } = "";
    public string Answer { get; set; } = "";
    public int Level { get; set; }
    public DateTimeOffset Due { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class AudioNote
{
    public string Name { get; set; } = "Aufnahme";
    public string Wave { get; set; } = "";
    public string Transcript { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
public static class Json
{
    public static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = false, PropertyNameCaseInsensitive = true, MaxDepth = 64 };
    public static byte[] Bytes<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(value, Options);
    public static T Read<T>(byte[] bytes) => JsonSerializer.Deserialize<T>(bytes, Options) ?? throw new InvalidDataException("Leere Datei.");
    public static NoteBook Book(byte[] bytes)
    {
        if (bytes.Length > 256 * 1024 * 1024) throw new InvalidDataException("Das Heft ist größer als 256 MB.");
        var book = Read<NoteBook>(bytes);
        if (book.Format != "org.owlluna.portable" || book.Schema != 1 || !Guid.TryParse(book.Id, out _) || !Guid.TryParse(book.Revision, out _) || book.Pages.Count == 0 || book.Pages.Count > 5000)
            throw new InvalidDataException("Unbekanntes oder beschädigtes Heftformat.");
        foreach (var p in book.Pages)
        {
            if (!Guid.TryParse(p.Id, out _) || !double.IsFinite(p.Width) || !double.IsFinite(p.Height) || p.Width < 10 || p.Height < 10 || p.Width > 20000 || p.Height > 20000) throw new InvalidDataException("Ungültige Seitengröße.");
            foreach (var s in p.Strokes)
                if (!double.IsFinite(s.Width) || s.Width <= 0 || s.Width > 500 || s.Points.Any(x => !double.IsFinite(x.X) || !double.IsFinite(x.Y) || !double.IsFinite(x.Pressure) || x.Pressure < 0 || x.Pressure > 1)) throw new InvalidDataException("Ungültige Strichdaten.");
        }
        return book;
    }
    public static void Atomic(string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.WriteThrough)) { stream.Write(bytes); stream.Flush(true); }
            File.Move(temp, path, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    public static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
}
public sealed class Settings
{
    public string? ICloudFolder { get; set; }
    public string? LocalBackupFolder { get; set; }
    public string? DriveFolderId { get; set; }
    public DateTimeOffset LastBackup { get; set; }
    public string LastBackupFingerprint { get; set; } = "";
    public List<string> SeenConflicts { get; set; } = [];
    public bool Dark { get; set; }
    public bool LeftHanded { get; set; }
    public double Zoom { get; set; } = 1;
}
