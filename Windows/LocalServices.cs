using System.IO;
using System.Text;
using System.Speech.Recognition;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace OwlLuna.Windows;
public static class LocalServices
{
    public static async Task<string> ReadImage(byte[] image)
    {
        var engine=OcrEngine.TryCreateFromUserProfileLanguages()??throw new InvalidOperationException("Kein Windows-OCR-Sprachpaket installiert.");
        using var stream=new InMemoryRandomAccessStream();
        using(var writer=new DataWriter(stream.GetOutputStreamAt(0))){writer.WriteBytes(image);await writer.StoreAsync();await writer.FlushAsync();writer.DetachStream();}
        stream.Seek(0);var decoder=await BitmapDecoder.CreateAsync(stream);
        var max=OcrEngine.MaxImageDimension;var scale=Math.Min(1,Math.Min((double)max/decoder.PixelWidth,(double)max/decoder.PixelHeight));
        var transform=new BitmapTransform{ScaledWidth=(uint)Math.Max(1,decoder.PixelWidth*scale),ScaledHeight=(uint)Math.Max(1,decoder.PixelHeight*scale)};
        using var bitmap=await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8,BitmapAlphaMode.Premultiplied,transform,ExifOrientationMode.RespectExifOrientation,ColorManagementMode.DoNotColorManage);
        var result=await engine.RecognizeAsync(bitmap);return result.Text;
    }
    public static Task<string> Transcribe(byte[] wave)=>Task.Run(()=>{
        var installed=SpeechRecognitionEngine.InstalledRecognizers();var info=installed.FirstOrDefault(r=>r.Culture.TwoLetterISOLanguageName==System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName)??installed.FirstOrDefault()??throw new InvalidOperationException("Kein lokales Windows-Spracherkennungspaket installiert.");
        using var engine=new SpeechRecognitionEngine(info);using var stream=new MemoryStream(wave);engine.SetInputToWaveStream(stream);engine.LoadGrammar(new DictationGrammar());
        var words=new List<string>();for(int i=0;i<20000;i++){var result=engine.Recognize(TimeSpan.FromSeconds(30));if(result is null)break;words.Add(result.Text);}return string.Join(" ",words);
    });
    public static List<string> CalendarForToday(string path)
    {
        var lines=File.ReadAllText(path).Replace("\r\n ","").Replace("\r\n\t","").Split('\n').Select(x=>x.TrimEnd('\r'));
        var result=new List<string>();string? title=null,start=null;
        foreach(var line in lines)
        {
            if(line=="BEGIN:VEVENT"){title=null;start=null;}
            if(line.StartsWith("SUMMARY:"))title=line[8..].Replace("\\n"," ").Replace("\\,",",").Replace("\\;",";");
            if(line.StartsWith("DTSTART"))start=line[(line.IndexOf(':')+1)..];
            if(line=="END:VEVENT"&&title is not null&&start?.StartsWith(DateTime.Today.ToString("yyyyMMdd"))==true)result.Add(title);
        }
        return result;
    }
}
