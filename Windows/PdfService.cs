using System.IO;
using System.Windows.Media.Imaging;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using Windows.Storage;
using Windows.Storage.Streams;

namespace OwlLuna.Windows;
public static class PdfService
{
    public static async Task<NoteBook> Import(string file)
    {
        var original=File.ReadAllBytes(file); if(original.Length>100*1024*1024)throw new InvalidDataException("PDF größer als 100 MB. Bitte in kleinere Teile aufteilen.");
        var storage=await StorageFile.GetFileFromPathAsync(Path.GetFullPath(file));
        var pdf=await global::Windows.Data.Pdf.PdfDocument.LoadFromFileAsync(storage);
        using var vectorSource=PdfReader.Open(new MemoryStream(original),PdfDocumentOpenMode.Import);
        if(pdf.IsPasswordProtected)throw new InvalidDataException("Bitte das PDF zuerst entsperren und erneut importieren.");
        var book=new NoteBook{Title=Path.GetFileNameWithoutExtension(file),Pages=[]};
        for(uint index=0;index<pdf.PageCount;index++)
        {
            using var page=pdf.GetPage(index); using var output=new InMemoryRandomAccessStream();
            var vectorPage=vectorSource.Pages[(int)index];
            double width=vectorPage.Width.Point,height=vectorPage.Height.Point;
            if(Math.Abs(vectorPage.Rotate)%180==90)(width,height)=(height,width);
            await page.RenderToStreamAsync(output,new global::Windows.Data.Pdf.PdfPageRenderOptions{DestinationWidth=(uint)Math.Ceiling(width*1.5)});
            output.Seek(0);using var reader=new DataReader(output.GetInputStreamAt(0));await reader.LoadAsync((uint)output.Size);var bytes=new byte[(int)output.Size];reader.ReadBytes(bytes);
            book.Pages.Add(new NotePage{Paper="blank",Width=width,Height=height,BackgroundImage=Convert.ToBase64String(bytes),BackgroundPdf=Convert.ToBase64String(original),PdfIndex=(int)index});
        }
        return book;
    }
    public static void Export(NoteBook book,string target)
    {
        using var document=new PdfDocument();document.Info.Title=book.Title;document.Info.Creator="OwlLuna Windows";
        foreach(var page in book.Pages)
        {
            PdfPage result;
            if(page.BackgroundPdf is not null)
            {
                using var source=PdfReader.Open(new MemoryStream(Convert.FromBase64String(page.BackgroundPdf)),PdfDocumentOpenMode.Import);
                if(page.PdfIndex<0||page.PdfIndex>=source.PageCount)throw new InvalidDataException("Ungültige PDF-Seite.");
                result=document.AddPage(source.Pages[page.PdfIndex]);
            }
            else {result=document.AddPage();result.Width=XUnit.FromPoint(page.Width);result.Height=XUnit.FromPoint(page.Height);}
            using var gfx=XGraphics.FromPdfPage(result,XGraphicsPdfPageOptions.Append);
            if(page.BackgroundPdf is null)
            {
                gfx.DrawRectangle(new XSolidBrush(Color(page.PaperColor)),0,0,page.Width,page.Height);
                if(page.BackgroundImage is not null){using var image=XImage.FromStream(new MemoryStream(Convert.FromBase64String(page.BackgroundImage)));gfx.DrawImage(image,0,0,page.Width,page.Height);}
                else Paper(gfx,page);
            }
            foreach(var item in page.Items.Where(i=>i.Kind!="tape"))DrawItem(item);
            foreach(var stroke in page.Strokes)
            {
                var color=Color(stroke.Color);if(stroke.Tool=="marker")color=XColor.FromArgb(85,color.R,color.G,color.B);
                if(stroke.Tool=="marker"&&stroke.Points.Count>1){gfx.DrawLines(new XPen(color,stroke.Width){LineCap=XLineCap.Round,LineJoin=XLineJoin.Round},stroke.Points.Select(p=>new XPoint(p.X,p.Y)).ToArray());continue;}
                for(int i=0;i<stroke.Points.Count;i++)
                {
                    var p=stroke.Points[i];var previous=stroke.Points[Math.Max(0,i-1)];
                    var width=stroke.Width*(stroke.Tool=="marker"?1:Math.Max(.2,(p.Pressure+previous.Pressure)));
                    var pen=new XPen(color,width){LineCap=XLineCap.Round,LineJoin=XLineJoin.Round};
                    if(stroke.Points.Count==1)gfx.DrawEllipse(new XSolidBrush(color),p.X-width/2,p.Y-width/2,width,width);
                    else if(i>0)gfx.DrawLine(pen,previous.X,previous.Y,p.X,p.Y);
                }
            }
            foreach(var item in page.Items.Where(i=>i.Kind=="tape"))DrawItem(item);
            void DrawItem(PageItem item)
            {
                var state=gfx.Save();gfx.TranslateTransform(item.X+item.Width/2,item.Y+item.Height/2);gfx.RotateTransform(item.Rotation);gfx.TranslateTransform(-item.Width/2,-item.Height/2);
                if(item.Kind=="image"&&item.Image is not null){using var image=XImage.FromStream(new MemoryStream(Convert.FromBase64String(item.Image)));gfx.DrawImage(image,0,0,item.Width,item.Height);}
                else if(item.Kind=="tape")gfx.DrawRectangle(new XSolidBrush(Color(item.Color)),0,0,item.Width,item.Height);
                else {var style=(item.Bold?XFontStyleEx.Bold:XFontStyleEx.Regular)|(item.Italic?XFontStyleEx.Italic:XFontStyleEx.Regular);var font=new XFont(item.FontFamily,item.FontSize,style);var formatter=new PdfSharp.Drawing.Layout.XTextFormatter(gfx);formatter.DrawString(item.Text,font,new XSolidBrush(Color(item.Color)),new XRect(0,0,item.Width,item.Height),XStringFormats.TopLeft);}
                gfx.Restore(state);
            }
        }
        using var stream=new MemoryStream();document.Save(stream,false);Json.Atomic(target,stream.ToArray());
    }
    static XColor Color(string value)
    {
        var color=(System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(value);
        return XColor.FromArgb(color.A,color.R,color.G,color.B);
    }
    static void Paper(XGraphics gfx,NotePage page)
    {
        var pen=new XPen(XColor.FromArgb(190,205,210),.5);
        void Line(double a,double b,double c,double d)=>gfx.DrawLine(pen,a,b,c,d);
        if(page.Paper is "grid" or "ruled" or "cornell")for(double y=36;y<page.Height;y+=page.Paper=="grid"?16:24)Line(0,y,page.Width,y);
        if(page.Paper=="grid")for(double x=16;x<page.Width;x+=16)Line(x,0,x,page.Height);
        if(page.Paper=="cornell"){Line(145,0,145,page.Height-130);Line(0,page.Height-130,page.Width,page.Height-130);}
        if(page.Paper=="dotted")for(double y=20;y<page.Height;y+=20)for(double x=20;x<page.Width;x+=20)gfx.DrawEllipse(XBrushes.LightGray,x,y,1.5,1.5);
        if(page.Paper=="music")for(double y=55;y<page.Height-60;y+=100)for(int i=0;i<5;i++)Line(30,y+i*9,page.Width-30,y+i*9);
    }
}
public static class Recognition
{
    public static async Task<string> Read(NotePage page)
    {
        var recognizer=new global::Windows.UI.Input.Inking.InkRecognizerContainer();
        if(recognizer.GetRecognizers().Count==0)return "Keine Windows-Handschrifterkennung installiert. Sprachpaket in Windows hinzufügen.";
        var container=new global::Windows.UI.Input.Inking.InkStrokeContainer();var builder=new global::Windows.UI.Input.Inking.InkStrokeBuilder();
        foreach(var stroke in page.Strokes.Where(s=>s.Tool!="marker"&&s.Points.Count>0))container.AddStroke(builder.CreateStroke(stroke.Points.Select(p=>new global::Windows.Foundation.Point(p.X,p.Y))));
        var results=await recognizer.RecognizeAsync(container,global::Windows.UI.Input.Inking.InkRecognitionTarget.All);
        var text=string.Join(" ",results.Select(r=>r.GetTextCandidates().FirstOrDefault()??""));
        if(text.Length==0)return "Keine Handschrift erkannt.";
        System.Windows.Clipboard.SetText(text);return "Erkannter Text wurde in die Zwischenablage kopiert: "+text;
    }
}
