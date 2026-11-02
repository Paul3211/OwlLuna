using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Path = System.IO.Path;

namespace OwlLuna.Windows;
public partial class MainWindow
{
    NoteBook? recordingOwner;
    string? recordingFile;
    readonly MediaPlayer audioPlayer = new();
    [DllImport("winmm.dll",CharSet=CharSet.Unicode)] static extern int mciSendString(string command,StringBuilder? result,int length,IntPtr window);
    void MoreMenu(object s,RoutedEventArgs e)
    {
        var m=new ContextMenu();
        AddMenu(m,"Seitenübersicht",()=>{if(book is null)return;var selected=ChoosePage(book.Pages.Select((p,i)=>$"{(p.Bookmark?"★ ":"")}Seite {i+1}  {p.Title}").ToList());if(selected>=0){Save();pageIndex=selected;ShowPage();}});
        AddMenu(m,"Auswahl kopieren",CopySelection);AddMenu(m,"Auswahl einfügen",PasteSelection);
        AddMenu(m,"Auswahl drehen",()=>EditSelected(i=>i.Rotation=(i.Rotation+90)%360));
        AddMenu(m,"Auswahl: Schriftgröße",()=>{var v=Ask("Schriftgröße","Punkte, z. B. 18","18");if(double.TryParse(v,out var size)&&size>=6&&size<=200)EditSelected(i=>i.FontSize=size);});
        AddMenu(m,"Auswahl: fett",()=>EditSelected(i=>i.Bold=!i.Bold));AddMenu(m,"Auswahl: kursiv",()=>EditSelected(i=>i.Italic=!i.Italic));
        AddMenu(m,"Auswahl: Bild zuschneiden",CropImage);
        AddMenu(m,"Lernband hinzufügen",()=>{if(Current is null)return;Checkpoint();Current.Items.Add(new(){Kind="tape",Width=280,Height=55,Color="#A87F38",Y=180});ShowPage();tool="select";SetPen();Changed();});
        AddMenu(m,"Lineal einfügen",()=>{if(Current is null)return;Checkpoint();Current.Items.Add(new(){Text="0    1    2    3    4    5    6    7    8    9    10\n┴────┴────┴────┴────┴────┴────┴────┴────┴────┴",Width=420,Height=55,FontSize=13,Y=300,Color="#69746F"});ShowPage();tool="select";SetPen();Changed();});
        AddMenu(m,"Karteikarte erstellen",NewCard);AddMenu(m,"Karteikarten lernen",ReviewCards);
        AddMenu(m,recordingOwner is null?"Audioaufnahme starten":"Audioaufnahme stoppen und speichern",RecordAudio);
        AddMenu(m,"Audioaufnahme abspielen",PlayAudio);AddMenu(m,"Audio stoppen",()=>audioPlayer.Stop());
        AddMenu(m,"Audio lokal transkribieren",async()=>{if(book is null||book.Recordings.Count==0)return;var index=ChoosePage(book.Recordings.Select(r=>r.Name).ToList());if(index<0)return;var owner=book;StateText.Text="Lokale Spracherkennung läuft …";var text=await LocalServices.Transcribe(Convert.FromBase64String(owner.Recordings[index].Wave));owner.Recordings[index].Transcript=text;library.Save(owner);if(text.Length>0)Clipboard.SetText(text);StateText.Text="Transkript gespeichert und kopiert";});
        AddMenu(m,"PDFs / Bilder durchsuchbar machen",async()=>{if(book is null)return;Save();var owner=book;int i=0;foreach(var page in owner.Pages){var found=new List<string>();if(page.BackgroundImage is not null)found.Add(await LocalServices.ReadImage(Convert.FromBase64String(page.BackgroundImage)));foreach(var item in page.Items.Where(x=>x.Kind=="image"&&x.Image is not null))found.Add(await LocalServices.ReadImage(Convert.FromBase64String(item.Image!)));page.IndexText=string.Join("\n",found);StateText.Text=$"Lokale Texterkennung · {++i}/{owner.Pages.Count}";}library.Save(owner);RefreshBooks(owner.Id);StateText.Text="PDF- und Bildtext ist jetzt durchsuchbar";});
        AddMenu(m,"Heutige Termine aus Kalenderdatei (.ics)",()=>{if(Current is null)return;var picker=new Microsoft.Win32.OpenFileDialog{Filter="Kalenderdateien|*.ics"};if(picker.ShowDialog()!=true)return;var events=LocalServices.CalendarForToday(picker.FileName);Checkpoint();Current.Items.Add(new(){Text=DateTime.Today.ToString("D")+"\n"+string.Join("\n",events.Select(x=>"• "+x)),Height=260,Width=400});ShowPage();Changed();});
        AddMenu(m,"Heutiges Tagebuch",DailyJournal);
        AddMenu(m,"Präsentationsmodus",Present);
        AddMenu(m,"Linkshändige Seitenleiste",()=>{library.Settings.LeftHanded=!library.Settings.LeftHanded;FlowDirection=library.Settings.LeftHanded?FlowDirection.RightToLeft:FlowDirection.LeftToRight;PageGrid.FlowDirection=FlowDirection.LeftToRight;library.SaveSettings();});
        m.IsOpen=true;
    }
    void EditSelected(Action<PageItem> edit)
    {
        var selected=Ink.GetSelectedElements().Where(elements.ContainsKey).Select(x=>elements[x]).ToList();
        if(selected.Count==0){StateText.Text="Zuerst Text oder Bild mit dem Lasso auswählen.";return;}
        Checkpoint();foreach(var item in selected)edit(item);ShowPage();Changed();
    }
    void CropImage()
    {
        var item=Ink.GetSelectedElements().Where(elements.ContainsKey).Select(x=>elements[x]).FirstOrDefault(i=>i.Kind=="image"&&i.Image is not null);if(item is null)return;
        var text=Ask("Bild zuschneiden","Links, Oben, Breite, Höhe in Prozent (mit Semikolon)","10;10;80;80");if(text is null)return;
        var fields=text.Split(';');if(fields.Length!=4||!fields.All(f=>double.TryParse(f,out _)))return;var v=fields.Select(double.Parse).ToArray();if(v.Any(x=>x<0)||v[2]<=0||v[3]<=0||v[0]+v[2]>100||v[1]+v[3]>100)return;
        Checkpoint();var source=Bitmap(Convert.FromBase64String(item.Image!));var crop=new CroppedBitmap(source,new Int32Rect((int)(source.PixelWidth*v[0]/100),(int)(source.PixelHeight*v[1]/100),Math.Max(1,(int)(source.PixelWidth*v[2]/100)),Math.Max(1,(int)(source.PixelHeight*v[3]/100))));var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(crop));using var output=new MemoryStream();encoder.Save(output);item.Image=Convert.ToBase64String(output.ToArray());item.Height=item.Width*crop.PixelHeight/crop.PixelWidth;ShowPage();Changed();
    }
    void CopySelection()
    {
        Capture();var strokeIds=Ink.GetSelectedStrokes().Select(FromStroke).Select(s=>s.Id).ToHashSet();var selected=Ink.GetSelectedElements().Where(elements.ContainsKey).Select(x=>elements[x]).ToList();
        var content=new NotePage{Strokes=Current!.Strokes.Where(s=>strokeIds.Contains(s.Id)).ToList(),Items=selected};Clipboard.SetData("OwlLuna.Selection.v1",Encoding.UTF8.GetString(Json.Bytes(content)));StateText.Text="Auswahl kopiert · kann auf anderer Seite eingefügt werden";
    }
    void PasteSelection()
    {
        if(Current is null||Clipboard.GetData("OwlLuna.Selection.v1") is not string content)return;
        var page=Json.Read<NotePage>(Encoding.UTF8.GetBytes(content));Checkpoint();
        foreach(var s in page.Strokes){s.Id=Guid.NewGuid().ToString();foreach(var p in s.Points){p.X+=15;p.Y+=15;}Current.Strokes.Add(s);}
        foreach(var item in page.Items){item.Id=Guid.NewGuid().ToString();item.X+=15;item.Y+=15;Current.Items.Add(item);}ShowPage();Changed();
    }
    void NewCard()
    {
        if(book is null)return;var question=Ask("Karteikarte","Frage","");if(string.IsNullOrWhiteSpace(question))return;var answer=Ask("Karteikarte","Antwort","");if(answer is null)return;Checkpoint();book.Cards.Add(new(){Question=question,Answer=answer});Changed();Save();
    }
    void ReviewCards()
    {
        if(book is null)return;var card=book.Cards.OrderBy(c=>c.Due).FirstOrDefault();if(card is null){MessageBox.Show("Dieses Heft hat noch keine Karteikarten.");return;}
        var w=new Window{Owner=this,Title="Lernen",Width=580,Height=400,WindowStartupLocation=WindowStartupLocation.CenterOwner};var panel=new StackPanel{Margin=new Thickness(28)};panel.Children.Add(new TextBlock{Text=card.Question,FontSize=22,TextWrapping=TextWrapping.Wrap});var answer=new TextBlock{Text=card.Answer,FontSize=18,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,20,0,20),Visibility=Visibility.Collapsed};panel.Children.Add(answer);var reveal=new Button{Content="Antwort zeigen"};reveal.Click+=(_,_)=>answer.Visibility=Visibility.Visible;panel.Children.Add(reveal);
        foreach(var known in new[]{false,true}){var button=new Button{Content=known?"Gewusst · später wiederholen":"Noch einmal"};button.Click+=(_,_)=>{Checkpoint();card.Level=known?Math.Min(card.Level+1,8):0;card.Due=DateTimeOffset.UtcNow.AddDays(known?Math.Pow(2,card.Level):0);Changed();Save();w.Close();};panel.Children.Add(button);}w.Content=panel;w.ShowDialog();
    }
    void RecordAudio()
    {
        if(recordingOwner is null)
        {
            if(book is null)return;recordingFile=Path.Combine(library.Root,"recording-"+Guid.NewGuid()+".wav");
            var error=mciSendString("open new type waveaudio alias owlluna_record",null,0,IntPtr.Zero);if(error==0)error=mciSendString("record owlluna_record",null,0,IntPtr.Zero);
            if(error!=0){mciSendString("close owlluna_record",null,0,IntPtr.Zero);throw new IOException("Audioaufnahme nicht verfügbar. Mikrofon und Windows-Zugriffsrechte prüfen.");}
            recordingOwner=book;StateText.Text="● Aufnahme läuft · Mehr → Audioaufnahme stoppen";
        }
        else
        {
            mciSendString("stop owlluna_record",null,0,IntPtr.Zero);var error=mciSendString("save owlluna_record \""+recordingFile+"\"",null,0,IntPtr.Zero);mciSendString("close owlluna_record",null,0,IntPtr.Zero);
            if(error!=0||!File.Exists(recordingFile))throw new IOException("Audio konnte nicht gespeichert werden. Aufnahme wurde gestoppt.");
            var data=File.ReadAllBytes(recordingFile);if(data.Length>100*1024*1024)throw new IOException("Aufnahme größer als 100 MB. Die WAV-Datei bleibt im App-Speicher erhalten.");
            recordingOwner.Recordings.Add(new(){Name="Aufnahme "+DateTime.Now.ToString("g"),Wave=Convert.ToBase64String(data)});library.Save(recordingOwner);recordingOwner=null;File.Delete(recordingFile);recordingFile=null;StateText.Text="Audioaufnahme gespeichert";
        }
    }
    void PlayAudio()
    {
        if(book is null||book.Recordings.Count==0){MessageBox.Show("Keine Aufnahmen in diesem Heft.");return;}var i=ChoosePage(book.Recordings.Select(r=>r.Name).ToList());if(i<0)return;var file=Path.Combine(library.Root,"playback.wav");audioPlayer.Close();Json.Atomic(file,Convert.FromBase64String(book.Recordings[i].Wave));audioPlayer.Open(new Uri(file));audioPlayer.Play();
    }
    void DailyJournal()
    {
        var title="Tagebuch · "+DateTime.Today.ToString("yyyy-MM-dd");var daily=library.Books.FirstOrDefault(b=>b.Title==title&&!b.Deleted);if(daily is null)CreateBook(title);else{Save();Open(daily);RefreshBooks(daily.Id);}
    }
    void Present()
    {
        if(Current is null)return;Save();var render=new RenderTargetBitmap((int)Current.Width*2,(int)Current.Height*2,144,144,PixelFormats.Pbgra32);render.Render(PageGrid);var w=new Window{Owner=this,Title="OwlLuna Präsentation · Escape zum Schließen",WindowStyle=WindowStyle.None,WindowState=WindowState.Maximized,Background=Brushes.Black,Content=new Image{Source=render,Stretch=Stretch.Uniform}};w.KeyDown+=(_,e)=>{if(e.Key==Key.Escape)w.Close();};w.ShowDialog();
    }
    static int ChoosePage(List<string> labels)
    {
        var w=new Window{Title="Auswählen",Width=450,Height=450,WindowStartupLocation=WindowStartupLocation.CenterOwner};var list=new ListBox{ItemsSource=labels};var panel=new DockPanel{Margin=new Thickness(12)};var open=new Button{Content="Öffnen"};DockPanel.SetDock(open,Dock.Bottom);panel.Children.Add(open);panel.Children.Add(list);open.Click+=(_,_)=>{if(list.SelectedIndex>=0)w.DialogResult=true;};w.Content=panel;return w.ShowDialog()==true?list.SelectedIndex:-1;
    }
}
