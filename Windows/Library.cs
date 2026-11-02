using System.IO;
using System.IO.Compression;

namespace OwlLuna.Windows;

public sealed class Library
{
    public string Root { get; }
    public string BooksPath => Path.Combine(Root, "Library");
    public Settings Settings { get; }
    public List<NoteBook> Books { get; } = [];
    public List<string> Warnings { get; } = [];
    public Library(string root)
    {
        Root = Path.GetFullPath(root); Directory.CreateDirectory(BooksPath);
        try { Settings = Json.Read<Settings>(File.ReadAllBytes(Path.Combine(Root, "settings.json"))); } catch { Settings = new(); }
        foreach (var file in Directory.GetFiles(BooksPath, "*.owlbook"))
        {
            try { Books.Add(Json.Book(File.ReadAllBytes(file))); }
            catch (Exception ex)
            {
                Warnings.Add(Path.GetFileName(file) + ": " + ex.Message);
                try { Books.Add(Json.Book(File.ReadAllBytes(file + ".previous"))); Warnings.Add("Die letzte gültige Sicherung wurde geöffnet. Die beschädigte Datei bleibt erhalten."); }
                catch { }
            }
        }
    }
    public void SaveSettings() => Json.Atomic(Path.Combine(Root, "settings.json"), Json.Bytes(Settings));
    public string Fingerprint => Json.Hash(System.Text.Encoding.UTF8.GetBytes(string.Join("\n", Books.OrderBy(b => b.Id).Select(b => b.Id + ":" + b.Revision))));
    public void Save(NoteBook book, bool revision = true)
    {
        if (revision)
        {
            if (!book.Ancestors.Contains(book.Revision)) book.Ancestors.Add(book.Revision);
            book.Revision = Guid.NewGuid().ToString(); book.ModifiedAt = DateTimeOffset.UtcNow;
        }
        var bytes = Json.Bytes(book); Json.Book(bytes);
        var path = Path.Combine(BooksPath, book.Id + ".owlbook");
        if (File.Exists(path))
        {
            var previous = File.ReadAllBytes(path);
            try { Json.Book(previous); Json.Atomic(path + ".previous", previous); }
            catch { File.Copy(path, path + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"), false); }
        }
        Json.Atomic(path, bytes);
        if (!Books.Contains(book)) Books.Add(book);
    }
    public NoteBook Import(byte[] bytes, bool copy = true)
    {
        var book = Json.Book(bytes);
        if (copy) { book.Id = Guid.NewGuid().ToString(); book.Revision = Guid.NewGuid().ToString(); book.Ancestors = []; }
        Save(book, false); return book;
    }
    public string Backup(string folder)
    {
        Directory.CreateDirectory(folder);
        var name = "OwlLuna-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N")[..6] + ".owlbackup";
        var target = Path.Combine(folder, name); var temporary = Path.Combine(Root, name + ".tmp");
        try
        {
            using (var zip = ZipFile.Open(temporary, ZipArchiveMode.Create))
            {
                foreach (var book in Books)
                {
                    var entry = zip.CreateEntry(book.Id + ".owlbook", CompressionLevel.Optimal);
                    using var stream = entry.Open(); stream.Write(Json.Bytes(book));
                }
            }
            ValidateBackup(temporary);
            // Remote providers see only the complete file.
            Json.Atomic(target, File.ReadAllBytes(temporary));
            if (Json.Hash(File.ReadAllBytes(target)) != Json.Hash(File.ReadAllBytes(temporary))) throw new IOException("Die Backup-Prüfsumme stimmt nicht.");
            return target;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public static List<NoteBook> ValidateBackup(string path)
    {
        using var zip = ZipFile.OpenRead(path);
        if (zip.Entries.Count == 0 || zip.Entries.Count > 5000 || zip.Entries.Sum(e => e.Length) > 1024L * 1024 * 1024) throw new InvalidDataException("Ungültiges oder zu großes Backup.");
        var books = new List<NoteBook>(); var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in zip.Entries)
        {
            if (Path.GetFileName(entry.FullName) != entry.FullName || !entry.Name.EndsWith(".owlbook") || !names.Add(entry.Name) || entry.Length > 256L * 1024 * 1024) throw new InvalidDataException("Ungültiger Backup-Eintrag.");
            using var stream = entry.Open(); using var data = new MemoryStream(); stream.CopyTo(data);
            books.Add(Json.Book(data.ToArray()));
        }
        return books;
    }
    public int Restore(string path)
    {
        var validated = ValidateBackup(path); // Validate everything before changing the library.
        foreach (var book in validated) { book.Id = Guid.NewGuid().ToString(); book.Title += " (wiederhergestellt)"; book.Revision = Guid.NewGuid().ToString(); book.Ancestors = []; Save(book, false); }
        return validated.Count;
    }
    public string Sync()
    {
        var folder = Settings.ICloudFolder;
        if (string.IsNullOrWhiteSpace(folder)) return "iCloud nicht verbunden · lokal gespeichert";
        if (!Directory.Exists(folder)) throw new DirectoryNotFoundException("Der iCloud-Ordner ist nicht erreichbar. Deine Hefte bleiben lokal erhalten.");
        int received = 0, conflicts = 0;
        var remote = new List<NoteBook>();
        foreach (var file in Directory.EnumerateFiles(folder, "*.owlbook", SearchOption.AllDirectories).Take(20000))
        {
            try { remote.Add(Json.Book(File.ReadAllBytes(file))); }
            catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException or InvalidDataException) { Warnings.Add("Cloud-Datei nicht lesbar: " + Path.GetFileName(file)); }
        }
        foreach (var group in remote.GroupBy(b => b.Id))
        {
            var all = group.DistinctBy(b => b.Revision).ToList();
            var heads = all.Where(b => !all.Any(other => other.Ancestors.Contains(b.Revision))).OrderBy(b => b.Revision).ToList();
            foreach (var head in heads)
            {
                var local = Books.FirstOrDefault(b => b.Id == head.Id);
                if (local is null) { Import(Json.Bytes(head), false); received++; continue; }
                if (local.Revision == head.Revision || local.Ancestors.Contains(head.Revision)) continue;
                if (head.Ancestors.Contains(local.Revision))
                {
                    Books.Remove(local); Import(Json.Bytes(head), false); received++; continue;
                }
                if (Settings.SeenConflicts.Contains(head.Revision)) continue;
                var copy = head.Clone(); copy.Id = Guid.NewGuid().ToString(); copy.Title += " (Konfliktkopie)";
                copy.Ancestors = []; copy.Revision = Guid.NewGuid().ToString(); Save(copy, false);
                Settings.SeenConflicts.Add(head.Revision); conflicts++;
            }
        }
        foreach (var book in Books)
        {
            var path = Path.Combine(folder, book.Id, book.Revision + ".owlbook");
            if (!File.Exists(path)) Json.Atomic(path, Json.Bytes(book));
        }
        SaveSettings();
        return $"Cloud-Ordner abgeglichen · {received} empfangen · {conflicts} Konfliktkopien. Uploadstatus: iCloud für Windows.";
    }
}
