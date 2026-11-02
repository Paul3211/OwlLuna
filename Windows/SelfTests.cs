using System.IO;
using System.IO.Compression;
using System.Text.Json;

namespace OwlLuna.Windows;
public static class SelfTests
{
    public static void Run(string? destination)
    {
        var root=Path.GetFullPath(destination??Path.Combine(AppContext.BaseDirectory,"self-tests-"+Guid.NewGuid()));
        Directory.CreateDirectory(root);var results=new List<string>();
        void Check(string name,Action test){test();results.Add(name+": PASS");}
        Check("Portable ink / pressure / unknown field roundtrip",()=>{var b=new NoteBook();b.Pages[0].Strokes.Add(new(){Points=[new(){X=5,Y=8,Pressure=.85,TiltX=27}]});b.Extra=new(){["future"]=JsonDocument.Parse("{\"keep\":true}").RootElement.Clone()};var c=Json.Book(Json.Bytes(b));if(c.Pages[0].Strokes[0].Points[0].Pressure!=.85||!c.Extra!["future"].GetProperty("keep").GetBoolean())throw new Exception("roundtrip");});
        var local=new Library(Path.Combine(root,"local"));var book=new NoteBook{Title="Test"};local.Save(book,false);
        Check("Atomic save / previous revision",()=>{book.Title="Test 2";local.Save(book);if(!File.Exists(Path.Combine(local.BooksPath,book.Id+".owlbook.previous")))throw new Exception("previous");var reopened=new Library(local.Root);if(reopened.Books.Single().Title!="Test 2")throw new Exception("reopen");});
        Check("Corrupt main document recovers previous without overwrite",()=>{var path=Path.Combine(local.BooksPath,book.Id+".owlbook");File.WriteAllText(path,"broken");var recovered=new Library(local.Root);if(recovered.Books.Count!=1||File.ReadAllText(path)!="broken")throw new Exception("recovery");local.Save(book,false);});
        string backup="";
        Check("Backup readback / restore as copy",()=>{backup=local.Backup(Path.Combine(root,"backups"));var count=local.Restore(backup);if(count!=1||local.Books.Count!=2||local.Books[0].Id==local.Books[1].Id)throw new Exception("restore");});
        Check("Malicious ZIP path rejected",()=>{var path=Path.Combine(root,"malicious.owlbackup");using(var z=ZipFile.Open(path,ZipArchiveMode.Create)){using var s=z.CreateEntry("../escape.owlbook").Open();s.Write(Json.Bytes(book));}try{Library.ValidateBackup(path);throw new Exception("accepted unsafe path");}catch(InvalidDataException){}});
        Check("Future schema rejected instead of overwritten",()=>{var b=book.Clone();b.Schema=999;try{Json.Book(Json.Bytes(b));throw new Exception("accepted future schema");}catch(InvalidDataException){}});
        Check("Non-finite pressure rejected",()=>{var b=book.Clone();b.Pages[0].Strokes.Add(new(){Points=[new(){Pressure=2}]});try{Json.Book(Json.Bytes(b));throw new Exception("accepted pressure");}catch(InvalidDataException){}});
        Check("OAuth desktop configuration stored with Windows user encryption",()=>{var folder=Path.Combine(root,"oauth-test");Directory.CreateDirectory(folder);var configuration=Path.Combine(folder,"synthetic.json");File.WriteAllText(configuration,"{\"installed\":{\"client_id\":\"12345-test.apps.googleusercontent.com\",\"client_secret\":\"SYNTHETIC-TEST-VALUE\"}}");var drive=new GoogleDrive(folder);drive.Configure(configuration);if(!drive.Configured||System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(folder,"google-client.protected"))).Contains("SYNTHETIC-TEST-VALUE"))throw new Exception("OAuth protection");});
        Check("Two-way folder synchronization",()=>{var cloud=Path.Combine(root,"cloud");Directory.CreateDirectory(cloud);local.Settings.ICloudFolder=cloud;local.Sync();var other=new Library(Path.Combine(root,"other"));other.Settings.ICloudFolder=cloud;other.Sync();if(other.Books.Count!=2)throw new Exception("download");var b=other.Books.First(x=>x.Id==book.Id);b.Title="Remote";other.Save(b);other.Sync();local.Sync();if(local.Books.First(x=>x.Id==book.Id).Title!="Remote")throw new Exception("update");});
        Check("Concurrent changes retained as conflict copy",()=>{var other=new Library(Path.Combine(root,"other"));var b=other.Books.First(x=>x.Id==book.Id);b.Title="Remote fork";other.Save(b);var a=local.Books.First(x=>x.Id==book.Id);a.Title="Local fork";local.Save(a);other.Sync();local.Sync();if(!local.Books.Any(x=>x.Title.Contains("Konfliktkopie"))||!local.Books.Any(x=>x.Title=="Local fork"))throw new Exception("conflict lost");var n=local.Books.Count;local.Sync();if(local.Books.Count!=n)throw new Exception("duplicate conflict");});
        Check("PDF export contains pages and ink",()=>{var b=new NoteBook();b.Pages[0].Strokes.Add(new(){Points=[new(){X=10,Y=10},new(){X=50,Y=90}]});var path=Path.Combine(root,"export.pdf");PdfService.Export(b,path);using var p=PdfSharp.Pdf.IO.PdfReader.Open(path,PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import);if(p.PageCount!=1)throw new Exception("PDF pages");});
        Check("Native PDF render/import/annotate/export",()=>{var path=Path.Combine(root,"export.pdf");var imported=Task.Run(()=>PdfService.Import(path)).GetAwaiter().GetResult();if(imported.Pages.Count!=1||imported.Pages[0].BackgroundImage is null||imported.Pages[0].BackgroundPdf is null)throw new Exception("PDF import");if(Math.Abs(imported.Pages[0].Width-595)>.01||Math.Abs(imported.Pages[0].Height-842)>.01)throw new Exception("PDF point/DIP conversion");File.WriteAllText(Path.Combine(root,"pdf-import-dimensions.txt"),imported.Pages[0].Width+"x"+imported.Pages[0].Height);imported.Pages[0].Strokes.Add(new(){Points=[new(){X=100,Y=100},new(){X=200,Y=200}]});PdfService.Export(imported,Path.Combine(root,"annotated.pdf"));});
        File.WriteAllLines(Path.Combine(root,"results.txt"),results);File.WriteAllText(Path.Combine(root,"results.json"),JsonSerializer.Serialize(new{passed=results.Count,failed=0,tests=results},new JsonSerializerOptions{WriteIndented=true}));
    }
}
