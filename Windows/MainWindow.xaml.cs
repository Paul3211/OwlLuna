using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using Path = System.IO.Path;

namespace OwlLuna.Windows;
public partial class MainWindow : Window
{
    readonly Library library;
    readonly GoogleDrive drive;
    readonly DispatcherTimer autosave = new() { Interval = TimeSpan.FromMilliseconds(850) };
    readonly DispatcherTimer cloudTimer = new() { Interval = TimeSpan.FromSeconds(40) };
    NoteBook? book;
    int pageIndex;
    string tool = "pen";
    bool loading, dirty, cloudBusy;
    readonly Stack<byte[]> undo = new(), redo = new();
    readonly Dictionary<UIElement, PageItem> elements = [];
    static readonly Guid StrokeId = new("f0bf5857-84d1-4f66-bf1f-d025586de39a");
    static readonly Guid StrokeTool = new("40e3dc99-1e8d-4f13-9c3f-5af4df6766df");
    NotePage? Current => book?.Pages.ElementAtOrDefault(pageIndex);
    string ColorHex => (InkColor?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "#243B53";
    public MainWindow()
    {
        var args = Environment.GetCommandLineArgs(); var dataIndex = Array.IndexOf(args, "--data-root");
        var root = dataIndex >= 0 && dataIndex + 1 < args.Length ? args[dataIndex + 1] : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OwlLunaWindows");
        library = new(root); drive = new(root);
        InitializeComponent();
        autosave.Tick += (_, _) => { autosave.Stop(); Save(); };
        cloudTimer.Tick += async (_, _) => { if (cloudBusy) return; cloudBusy = true; try { Save(); if (drive.Connected && dirty == false && library.Fingerprint != library.Settings.LastBackupFingerprint && DateTimeOffset.UtcNow - library.Settings.LastBackup > TimeSpan.FromMinutes(15)) await Backup(true); } catch (Exception ex) { StateText.Text = "Lokal gespeichert · Cloud: " + ex.Message; } finally { cloudBusy = false; } };
        cloudTimer.Start();
        Ink.Strokes.StrokesChanged += (_, _) => { if (!loading) Changed(); };
        Ink.Focusable = true; Ink.PreviewMouseDown += (_, ev) => { if (ev.OriginalSource is not TextBox) Ink.Focus(); if (tool is "eraser" or "select") Checkpoint(); };
        Ink.PreviewStylusDown += (_, _) => { if (tool is "eraser" or "select") Checkpoint(); };
        Closing += (_, e) => { try { Save(); } catch (Exception ex) { e.Cancel = true; MessageBox.Show("Speichern fehlgeschlagen; die App bleibt geöffnet.\n" + ex.Message); } };
        Closing += (_, e) => { if (recordingOwner is not null) { e.Cancel = true; MessageBox.Show("Bitte die Audioaufnahme zuerst stoppen.", "Aufnahme läuft"); } };
        SetPen(); RefreshBooks();
        if (library.Books.Count == 0) CreateBook("Mein erstes Heft");
        else BooksList.SelectedIndex = 0;
        if (library.Warnings.Count > 0) StateText.Text = string.Join(" · ", library.Warnings.Take(2));
        ApplyAppearance();
    }
    void RefreshBooks(string? select = null)
    {
        if (BooksList is null || Search is null || Scope is null) return;
        var search = Search.Text.Trim(); var scope = Scope.SelectedIndex;
        var filtered = library.Books.Where(b => b.Deleted == (scope == 2) && (scope != 1 || b.Favorite) && (search.Length == 0 || (b.Title + " " + b.Folder + " " + b.Tags + " " + string.Join(" ", b.Pages.Select(p=>p.IndexText)) + " " + string.Join(" ", b.Recordings.Select(r=>r.Transcript)) + " " + string.Join(" ", b.Pages.SelectMany(p => p.Items).Select(i => i.Text))).Contains(search, StringComparison.CurrentCultureIgnoreCase))).OrderByDescending(b => b.ModifiedAt).ToList();
        loading = true; BooksList.ItemsSource = filtered; BooksList.SelectedItem = filtered.FirstOrDefault(b => b.Id == select); loading = false;
        if (select is not null && BooksList.SelectedItem is NoteBook chosen && !ReferenceEquals(chosen, book)) Open(chosen);
    }
    void SearchChanged(object sender, TextChangedEventArgs e) => RefreshBooks(book?.Id);
    void ScopeChanged(object sender, SelectionChangedEventArgs e) => RefreshBooks(book?.Id);
    void BookSelected(object sender, SelectionChangedEventArgs e) { if (!loading && BooksList.SelectedItem is NoteBook b) { Save(); Open(b); } }
    void Open(NoteBook value) { book = value; pageIndex = 0; undo.Clear(); redo.Clear(); ShowPage(); Title = value.Title + " · OwlLuna"; }
    void CreateBook(string title) { Save(); var next = new NoteBook { Title = title }; library.Save(next, false); RefreshBooks(next.Id); Open(next); }
    void NewBook(object s, RoutedEventArgs e) { var name = Ask("Neues Heft", "Wie soll dein Heft heißen?", "Neues Heft"); if (name is not null) CreateBook(name); }
    void Favorite(object s, RoutedEventArgs e) { if (book is null) return; Checkpoint(); book.Favorite = !book.Favorite; Changed(); Save(); RefreshBooks(book.Id); }
    void BookProperties(object s, RoutedEventArgs e)
    {
        if (book is null) return; var title = Ask("Heft", "Titel", book.Title); if (title is null) return;
        var folder = Ask("Ordner", "Ordnerpfad (z. B. Schule / Mathematik)", book.Folder); if (folder is null) return;
        var tags = Ask("Tags", "Tags, mit Komma getrennt", book.Tags); if (tags is null) return;
        Checkpoint(); book.Title = title; book.Folder = folder; book.Tags = tags; Changed(); Save(); RefreshBooks(book.Id); Title = title + " · OwlLuna";
    }
    void TrashBook(object s, RoutedEventArgs e) { if (book is null) return; Checkpoint(); book.Deleted = !book.Deleted; Changed(); Save(); RefreshBooks(); if (BooksList.Items.Count > 0) BooksList.SelectedIndex = 0; }
    void Checkpoint() { if (book is null || loading) return; Capture(); undo.Push(Json.Bytes(book)); if (undo.Count > 60) { var recent = undo.Take(60).Reverse().ToArray(); undo.Clear(); foreach (var b in recent) undo.Push(b); } redo.Clear(); }
    void Changed() { if (loading || book is null) return; dirty = true; StateText.Text = "Änderungen werden gespeichert …"; autosave.Stop(); autosave.Start(); }
    void Save() { if (!dirty || book is null) return; Capture(); library.Save(book); dirty = false; StateText.Text = "✓ Lokal gespeichert · " + DateTime.Now.ToString("HH:mm:ss"); }
    void Capture()
    {
        var page = Current; if (page is null || loading) return;
        var old = page.Strokes.ToDictionary(s => s.Id);
        page.Strokes = Ink.Strokes.Select(s => {
            var value = FromStroke(s);
            if (old.TryGetValue(value.Id, out var previous)) {
                value.Extra = previous.Extra;
                if (value.Points.Count == previous.Points.Count) for (int i = 0; i < value.Points.Count; i++) {
                    value.Points[i].Time = previous.Points[i].Time;
                    if (!s.StylusPoints.Description.HasProperty(StylusPointProperties.XTiltOrientation)) value.Points[i].TiltX = previous.Points[i].TiltX;
                    if (!s.StylusPoints.Description.HasProperty(StylusPointProperties.YTiltOrientation)) value.Points[i].TiltY = previous.Points[i].TiltY;
                }
            }
            return value;
        }).ToList();
        page.Items = elements.Select(pair => { var model = pair.Value; var el = (FrameworkElement)pair.Key; model.X = InkCanvas.GetLeft(el); model.Y = InkCanvas.GetTop(el); if (!double.IsFinite(model.X)) model.X = 0; if (!double.IsFinite(model.Y)) model.Y = 0; model.Width = el.ActualWidth > 0 ? el.ActualWidth : el.Width; model.Height = el.ActualHeight > 0 ? el.ActualHeight : el.Height; if (el is TextBox text) model.Text = text.Text; return model; }).ToList();
    }
    static InkStroke FromStroke(Stroke s)
    {
        var id = s.ContainsPropertyData(StrokeId) ? s.GetPropertyData(StrokeId).ToString()! : Guid.NewGuid().ToString();
        if (!s.ContainsPropertyData(StrokeId)) s.AddPropertyData(StrokeId, id);
        return new InkStroke { Id = id, Tool = s.ContainsPropertyData(StrokeTool) ? s.GetPropertyData(StrokeTool).ToString()! : s.DrawingAttributes.IsHighlighter ? "marker" : "pen", Color = s.DrawingAttributes.Color.ToString(), Width = s.DrawingAttributes.Width,
            Points = s.StylusPoints.Select((p, i) => new InkPoint { X = p.X, Y = p.Y, Pressure = p.PressureFactor, Time = i * .008, TiltX = p.Description.HasProperty(StylusPointProperties.XTiltOrientation) ? p.GetPropertyValue(StylusPointProperties.XTiltOrientation) : 0, TiltY = p.Description.HasProperty(StylusPointProperties.YTiltOrientation) ? p.GetPropertyValue(StylusPointProperties.YTiltOrientation) : 0 }).ToList() };
    }
    static Stroke ToStroke(InkStroke model)
    {
        var points = new StylusPointCollection(model.Points.Select(p => new StylusPoint(p.X, p.Y, (float)Math.Clamp(p.Pressure, 0, 1))));
        if (points.Count == 0) points.Add(new StylusPoint(0, 0));
        var stroke = new Stroke(points) { DrawingAttributes = new() { Color = (Color)ColorConverter.ConvertFromString(model.Color), Width = model.Width, Height = model.Width, IsHighlighter = model.Tool == "marker", IgnorePressure = model.Tool == "marker", FitToCurve = true } };
        stroke.AddPropertyData(StrokeId, model.Id); stroke.AddPropertyData(StrokeTool, model.Tool); return stroke;
    }
    void ShowPage()
    {
        var page = Current; if (page is null) return; loading = true;
        try
        {
            PageGrid.Width = Ink.Width = page.Width; PageGrid.Height = Ink.Height = page.Height;
            Ink.Strokes = new StrokeCollection(page.Strokes.Where(s => s.Points.Count > 0).Select(ToStroke));
            Ink.Strokes.StrokesChanged += (_, _) => { if (!loading) Changed(); };
            Ink.Children.Clear(); elements.Clear();
            foreach (var item in page.Items)
            {
                FrameworkElement element;
                if (item.Kind == "image" && item.Image is not null) element = new Image { Source = Bitmap(Convert.FromBase64String(item.Image)), Stretch = Stretch.Uniform };
                else if (item.Kind == "tape") {
                    var tape = new Button { Content = "Tippen zum Aufdecken", Background = Brush(item.Color), BorderThickness = new Thickness(0), Foreground = Brushes.White };
                    tape.Click += (_, _) => { tape.Opacity = tape.Opacity < .5 ? 1 : .15; tape.Content = tape.Opacity < .5 ? "" : "Tippen zum Aufdecken"; };
                    element = tape;
                }
                else element = new TextBox { Text = item.Text, FontSize = item.FontSize, FontFamily = new FontFamily(item.FontFamily), FontWeight = item.Bold ? FontWeights.Bold : FontWeights.Normal, FontStyle = item.Italic ? FontStyles.Italic : FontStyles.Normal, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = Brush(item.Color) };
                element.Width = item.Width; element.Height = item.Height; element.RenderTransform = new RotateTransform(item.Rotation, item.Width / 2, item.Height / 2);
                InkCanvas.SetLeft(element, item.X); InkCanvas.SetTop(element, item.Y); elements[element] = item; Ink.Children.Add(element);
                if (element is TextBox text) { text.GotKeyboardFocus += (_, _) => Checkpoint(); text.TextChanged += (_, _) => { if (!loading) Changed(); }; }
            }
            foreach (ComboBoxItem option in Paper.Items) if (option.Tag?.ToString() == page.Paper) Paper.SelectedItem = option;
            DrawPaper(page); PageNumber.Text = $"{pageIndex + 1} / {book!.Pages.Count}"; PageInfo.Text = book.Title + "  ·  " + (page.Bookmark ? "★  " : "") + "A4 / " + Math.Round(page.Width) + " × " + Math.Round(page.Height);
            ApplyZoom(); SetPen();
        }
        finally { loading = false; }
    }
    static SolidColorBrush Brush(string hex) => new((Color)ColorConverter.ConvertFromString(hex));
    internal static BitmapImage Bitmap(byte[] data) { var image = new BitmapImage(); using var stream = new MemoryStream(data); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.StreamSource = stream; image.EndInit(); image.Freeze(); return image; }
    void DrawPaper(NotePage page)
    {
        PaperCanvas.Children.Clear(); PaperCanvas.Background = Brush(page.PaperColor);
        if (page.BackgroundImage is not null) { PaperCanvas.Children.Add(new Image { Source = Bitmap(Convert.FromBase64String(page.BackgroundImage)), Width = page.Width, Height = page.Height, Stretch = Stretch.Fill }); return; }
        void Line(double x1, double y1, double x2, double y2) => PaperCanvas.Children.Add(new Line { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Stroke = Brush("#C7D8D7"), StrokeThickness = .55 });
        if (page.Paper is "grid" or "ruled" or "cornell") for (double y = 36; y < page.Height; y += page.Paper == "grid" ? 16 : 24) Line(0, y, page.Width, y);
        if (page.Paper == "grid") for (double x = 16; x < page.Width; x += 16) Line(x, 0, x, page.Height);
        if (page.Paper == "cornell") { Line(145, 0, 145, page.Height - 130); Line(0, page.Height - 130, page.Width, page.Height - 130); }
        if (page.Paper == "music") for (double y = 55; y < page.Height - 60; y += 100) for (int i = 0; i < 5; i++) Line(30, y + i * 9, page.Width - 30, y + i * 9);
        if (page.Paper == "dotted") for (double y = 20; y < page.Height; y += 20) for (double x = 20; x < page.Width; x += 20) { var dot = new Ellipse { Width = 1.5, Height = 1.5, Fill = Brush("#B9CAC5") }; Canvas.SetLeft(dot, x); Canvas.SetTop(dot, y); PaperCanvas.Children.Add(dot); }
    }
    void SetPen()
    {
        if (Ink is null || PenWidth is null) return;
        Ink.EditingMode = tool switch { "eraser" => InkCanvasEditingMode.EraseByPoint, "select" => InkCanvasEditingMode.Select, "hand" => InkCanvasEditingMode.None, _ => InkCanvasEditingMode.Ink };
        Ink.IsHitTestVisible = tool != "hand"; Ink.EditingModeInverted = InkCanvasEditingMode.EraseByPoint;
        Ink.DefaultDrawingAttributes = new() { Color = (Color)ColorConverter.ConvertFromString(tool == "marker" && ColorHex == "#243B53" ? "#F3D54C" : ColorHex), Width = PenWidth.Value * (tool == "marker" ? 4 : 1), Height = PenWidth.Value * (tool == "marker" ? 4 : tool == "fountain" ? .55 : 1), IgnorePressure = tool == "marker", IsHighlighter = tool == "marker", FitToCurve = true };
        Ink.EraserShape = new EllipseStylusShape(Math.Max(10, PenWidth.Value * 3), Math.Max(10, PenWidth.Value * 3));
        foreach (var el in elements.Keys) el.IsHitTestVisible = tool == "select";
    }
    void ToolClick(object s, RoutedEventArgs e) { tool = ((Button)s).Tag.ToString()!; SetPen(); StateText.Text = "Werkzeug: " + ((Button)s).Content; }
    void PenChanged(object s, RoutedEventArgs e) => SetPen();
    void CanvasTouchDown(object s, TouchEventArgs e) { if (PencilOnly.IsChecked == true && tool != "hand") e.Handled = true; }
    void StrokeCollected(object s, InkCanvasStrokeCollectedEventArgs e) { if (book is null) return; var after = Ink.Strokes; after.Remove(e.Stroke); Checkpoint(); after.Add(e.Stroke); e.Stroke.AddPropertyData(StrokeTool, tool); Changed(); }
    void StrokeErased(object s, RoutedEventArgs e) => Changed();
    void SelectionChanged(object s, EventArgs e) => Changed();
    void Undo(object s, RoutedEventArgs e) => RestoreUndo(undo, redo);
    void Redo(object s, RoutedEventArgs e) => RestoreUndo(redo, undo);
    void RestoreUndo(Stack<byte[]> from, Stack<byte[]> to) { if (book is null || from.Count == 0) return; Capture(); to.Push(Json.Bytes(book)); var old = book; book = Json.Book(from.Pop()); book.Revision = old.Revision; book.Ancestors = old.Ancestors; library.Books.Remove(old); library.Books.Add(book); pageIndex = Math.Min(pageIndex, book.Pages.Count - 1); ShowPage(); Changed(); Save(); RefreshBooks(book.Id); }
    void PreviousPage(object s, RoutedEventArgs e) { if (book is null || pageIndex == 0) return; Save(); pageIndex--; ShowPage(); }
    void NextPage(object s, RoutedEventArgs e) { if (book is null || pageIndex >= book.Pages.Count - 1) return; Save(); pageIndex++; ShowPage(); }
    void AddPage(object s, RoutedEventArgs e) { if (book is null) return; Checkpoint(); var page = new NotePage { Paper = Current?.Paper ?? "grid" }; book.Pages.Insert(pageIndex + 1, page); pageIndex++; ShowPage(); Changed(); }
    void PaperChanged(object s, SelectionChangedEventArgs e) { if (loading || Current is null) return; Checkpoint(); Current.Paper = (Paper.SelectedItem as ComboBoxItem)?.Tag.ToString() ?? "blank"; Current.BackgroundImage = null; Current.BackgroundPdf = null; DrawPaper(Current); Changed(); }
    void PageMenu(object s, RoutedEventArgs e)
    {
        if (book is null) return; var menu = new ContextMenu();
        AddMenu(menu, "Seite duplizieren", () => { Checkpoint(); var clone = Json.Read<NotePage>(Json.Bytes(Current!)); clone.Id = Guid.NewGuid().ToString(); book.Pages.Insert(++pageIndex, clone); ShowPage(); Changed(); });
        AddMenu(menu, "Seite löschen", () => { Checkpoint(); book.Pages.RemoveAt(pageIndex); if (book.Pages.Count == 0) book.Pages.Add(new()); pageIndex = Math.Min(pageIndex, book.Pages.Count - 1); ShowPage(); Changed(); });
        AddMenu(menu, "Seite nach vorn", () => MovePage(-1)); AddMenu(menu, "Seite nach hinten", () => MovePage(1));
        AddMenu(menu, "Lesezeichen umschalten", () => { Checkpoint(); Current!.Bookmark = !Current.Bookmark; Changed(); ShowPage(); });
        AddMenu(menu, "Papierfarbe", () => { var value = Ask("Papierfarbe", "Farbe als #RRGGBB", Current!.PaperColor); if (value is null) return; _ = Brush(value); Checkpoint(); Current.PaperColor = value; DrawPaper(Current); Changed(); });
        AddMenu(menu, "Seitengröße", () => { var value = Ask("Seitengröße", "A4, A5, Letter oder Board", "A4"); if (value is null) return; Checkpoint(); (Current!.Width, Current.Height) = value.ToLowerInvariant() switch { "a5" => (420,595), "letter" => (612,792), "board" => (2000,2000), _ => (595,842) }; ShowPage(); Changed(); });
        AddMenu(menu, "Handschrift als Text", async () => { Save(); StateText.Text = await Recognition.Read(Current!); });
        menu.IsOpen = true;
    }
    void MovePage(int offset) { if (book is null || pageIndex + offset < 0 || pageIndex + offset >= book.Pages.Count) return; Checkpoint(); var p = Current!; book.Pages.RemoveAt(pageIndex); pageIndex += offset; book.Pages.Insert(pageIndex, p); ShowPage(); Changed(); }
    static void AddMenu(ContextMenu menu, string label, Action action) { var item = new MenuItem { Header = label }; item.Click += (_, _) => action(); menu.Items.Add(item); }
    void AddText(object s, RoutedEventArgs e) { if (Current is null) return; var text = Ask("Textfeld", "Text", "Dein Text"); if (text is null) return; Checkpoint(); Current.Items.Add(new() { Text = text, Color = ColorHex }); ShowPage(); tool = "select"; SetPen(); Changed(); }
    void AddImage(object s, RoutedEventArgs e) { if (Current is null) return; var picker = new OpenFileDialog { Filter = "Bilder|*.png;*.jpg;*.jpeg;*.bmp" }; if (picker.ShowDialog() != true) return; Checkpoint(); var image = Bitmap(File.ReadAllBytes(picker.FileName)); Current.Items.Add(new() { Kind = "image", Image = Convert.ToBase64String(File.ReadAllBytes(picker.FileName)), Width = Math.Min(300, image.PixelWidth), Height = Math.Min(300, image.PixelWidth) * image.PixelHeight / image.PixelWidth }); ShowPage(); tool = "select"; SetPen(); Changed(); }
    void ShapeMenu(object s, RoutedEventArgs e) { var m = new ContextMenu(); foreach (var label in new[] { "Linie", "Pfeil", "Rechteck", "Kreis", "Dreieck" }) AddMenu(m, label, () => Shape(label)); m.IsOpen = true; }
    void Shape(string shape)
    {
        if (Current is null) return; Checkpoint(); var points = shape switch { "Rechteck" => new[] {new Point(100,100), new Point(300,100), new Point(300,240), new Point(100,240), new Point(100,100)}, "Dreieck" => [new Point(200,100),new Point(300,250),new Point(100,250),new Point(200,100)], "Kreis" => Enumerable.Range(0,65).Select(i=>new Point(200+90*Math.Cos(i*Math.PI/32),200+90*Math.Sin(i*Math.PI/32))).ToArray(), "Pfeil" => [new Point(100,150),new Point(300,150),new Point(275,130),new Point(300,150),new Point(275,170)], _ => [new Point(100,150),new Point(300,150)] };
        Ink.Strokes.Add(new Stroke(new StylusPointCollection(points)) { DrawingAttributes = Ink.DefaultDrawingAttributes.Clone() }); Changed();
    }
    void DeleteSelection(object s, RoutedEventArgs e) { if (book is null) return; Checkpoint(); foreach (var stroke in Ink.GetSelectedStrokes().ToArray()) Ink.Strokes.Remove(stroke); foreach (var element in Ink.GetSelectedElements().ToArray()) { elements.Remove(element); Ink.Children.Remove(element); } Changed(); }
    void ZoomOut(object s, RoutedEventArgs e) { library.Settings.Zoom = Math.Max(.25, library.Settings.Zoom / 1.15); ApplyZoom(); }
    void ZoomIn(object s, RoutedEventArgs e) { library.Settings.Zoom = Math.Min(5, library.Settings.Zoom * 1.15); ApplyZoom(); }
    void FitPage(object s, RoutedEventArgs e) { if (Current is null) return; library.Settings.Zoom = Math.Clamp((PageScroll.ActualWidth - 90) / Current.Width, .25, 5); ApplyZoom(); }
    void ApplyZoom() { PageGrid.LayoutTransform = new ScaleTransform(library.Settings.Zoom, library.Settings.Zoom); library.SaveSettings(); }
    void Appearance(object s, RoutedEventArgs e) { library.Settings.Dark = !library.Settings.Dark; ApplyAppearance(); library.SaveSettings(); }
    void ApplyAppearance() { PageScroll.Background = Brush(library.Settings.Dark ? "#26332F" : "#DDE3DC"); Background = Brush(library.Settings.Dark ? "#26332F" : "#EFF0E9"); }
    void KeyPressed(object s, KeyEventArgs e)
    {
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) { if (e.Key == Key.S) { Save(); e.Handled = true; } if (Keyboard.FocusedElement is TextBox) return; if (e.Key == Key.Z) { Undo(s,e); e.Handled=true; } if (e.Key == Key.Y) { Redo(s,e); e.Handled=true; } if (e.Key == Key.N) { NewBook(s,e); e.Handled=true; } }
        if (e.Key == Key.Delete && Keyboard.FocusedElement is not TextBox) { DeleteSelection(s,e); e.Handled=true; }
        if (e.Key == Key.Escape) { tool="pen"; SetPen(); }
    }
    async void ImportFile(object s, RoutedEventArgs e) { var picker = new OpenFileDialog { Filter = "Notizen, PDF und Backups|*.owlbook;*.pdf;*.owlbackup", Multiselect = true }; if (picker.ShowDialog() == true) foreach (var file in picker.FileNames) await Import(file); }
    async void FilesDropped(object s, DragEventArgs e) { if (e.Data.GetData(DataFormats.FileDrop) is string[] files) foreach (var file in files) await Import(file); }
    async Task Import(string file)
    {
        Save(); StateText.Text = "Import läuft …";
        try { NoteBook? imported = null; switch (Path.GetExtension(file).ToLowerInvariant()) { case ".owlbook": imported = library.Import(File.ReadAllBytes(file)); break; case ".owlbackup": library.Restore(file); break; case ".pdf": imported = await PdfService.Import(file); library.Save(imported, false); break; default: throw new InvalidDataException("Dateityp nicht unterstützt."); } RefreshBooks(imported?.Id); if (imported is not null) Open(imported); StateText.Text = "Import abgeschlossen"; }
        catch(Exception ex) { MessageBox.Show(ex.Message,"Import"); StateText.Text="Import fehlgeschlagen · lokale Hefte unverändert"; }
    }
    void ExportMenu(object s, RoutedEventArgs e)
    {
        if (book is null) return; Save(); var menu = new ContextMenu();
        AddMenu(menu, "Heft (.owlbook, bearbeitbar)", () => { var p = new SaveFileDialog { FileName = SafeName(book.Title)+".owlbook", Filter="OwlLuna-Heft|*.owlbook" }; if(p.ShowDialog()==true) Json.Atomic(p.FileName,Json.Bytes(book)); });
        AddMenu(menu, "PDF (alle Seiten)", () => { var p = new SaveFileDialog { FileName=SafeName(book.Title)+".pdf", Filter="PDF|*.pdf" }; if(p.ShowDialog()==true) { PdfService.Export(book,p.FileName); StateText.Text="PDF exportiert"; } });
        AddMenu(menu, "Aktuelle Seite als PNG", () => { var p = new SaveFileDialog { FileName="Seite.png", Filter="PNG|*.png" }; if(p.ShowDialog()==true) { var bitmap=new RenderTargetBitmap((int)Current!.Width*2,(int)Current.Height*2,144,144,PixelFormats.Pbgra32); bitmap.Render(PageGrid); var png=new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); using var stream=File.Create(p.FileName); png.Save(stream); } });
        AddMenu(menu,"Drucken",()=>{var dialog=new PrintDialog(); if(dialog.ShowDialog()==true) dialog.PrintVisual(PageGrid,book.Title);});
        AddMenu(menu,"Text / Markdown",()=>{var p=new SaveFileDialog{FileName=SafeName(book.Title)+".md",Filter="Markdown|*.md"};if(p.ShowDialog()==true)File.WriteAllText(p.FileName,"# "+book.Title+"\n\n"+string.Join("\n\n",book.Pages.Select((page,i)=>"## Seite "+(i+1)+"\n\n"+string.Join("\n",page.Items.Where(x=>x.Kind=="text").Select(x=>x.Text)))));});
        menu.IsOpen=true;
    }
    static string SafeName(string name) => string.Concat(name.Select(c=>Path.GetInvalidFileNameChars().Contains(c)?'_':c));
    async Task Backup(bool google)
    {
        Save(); var folder=library.Settings.LocalBackupFolder ?? Path.Combine(library.Root,"Backups"); var fingerprint=library.Fingerprint; var path=library.Backup(folder); StateText.Text="Backup geprüft · " + Path.GetFileName(path);
        if(google) { await drive.UploadBackup(path,library.Settings); library.Settings.LastBackup=DateTimeOffset.UtcNow; library.Settings.LastBackupFingerprint=fingerprint; library.SaveSettings(); StateText.Text="✓ Backup zu Google Drive hochgeladen"; }
    }
    void CloudSettings(object s,RoutedEventArgs e)
    {
        var dialog=new Window { Owner=this,Title="Cloud & Backup",Width=640,Height=640,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=Brush("#F9F8F3") };
        var panel=new StackPanel{Margin=new Thickness(24)}; var status=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(5,12,5,12)};
        panel.Children.Add(new TextBlock{Text="Deine Notizen bleiben zuerst auf diesem PC.",FontSize=20,TextWrapping=TextWrapping.Wrap});
        void Button(string label,Func<Task> action) { var b=new Button{Content=label,HorizontalContentAlignment=HorizontalAlignment.Left}; b.Click+=async (_,_)=>{b.IsEnabled=false; try{await action();}catch(Exception ex){status.Text=ex.Message;}finally{b.IsEnabled=true;}};panel.Children.Add(b); }
        Button("Google: OAuth-Desktop-Konfiguration importieren",()=>{var p=new OpenFileDialog{Filter="Google OAuth-Konfiguration|*.json"};if(p.ShowDialog()==true){drive.Configure(p.FileName);library.Settings.DriveFolderId=null;library.SaveSettings();status.Text="Google-Client konfiguriert. Jetzt anmelden.";}return Task.CompletedTask;});
        Button(drive.Connected?"Google: erneut anmelden":"Mit Google anmelden",async()=>{await drive.SignIn();library.Settings.DriveFolderId=null;library.SaveSettings();status.Text="Mit Google Drive verbunden. Zugriff nur auf von OwlLuna erstellte Dateien.";});
        Button("Jetzt geprüftes Backup zu Google Drive hochladen",async()=>{await Backup(true);status.Text=StateText.Text;});
        Button("Google-Backup wiederherstellen …",async()=>{var files=await drive.ListBackups();if(files.Count==0){status.Text="Keine OwlLuna-Backups gefunden.";return;}var chosen=Choose("Google-Backups",files.Select(f=>f.Name).ToList());if(chosen<0)return;var path=Path.Combine(library.Root,"download-"+Guid.NewGuid()+".owlbackup");try{await drive.Download(files[chosen].Id,path);var count=library.Restore(path);RefreshBooks();status.Text=$"{count} Hefte als Kopien wiederhergestellt.";}finally{if(File.Exists(path))File.Delete(path);}});
        Button("Google auf diesem PC abmelden",()=>{drive.SignOut();status.Text="Lokale Anmeldung entfernt. Hefte und Cloud-Backups bleiben erhalten.";return Task.CompletedTask;});
        Button("Lokales Backup speichern …",async()=>{var p=new OpenFolderDialog{Title="Backup-Ordner"};if(p.ShowDialog()==true){library.Settings.LocalBackupFolder=p.FolderName;library.SaveSettings();await Backup(false);status.Text=StateText.Text;}});
        panel.Children.Add(status);dialog.Content=new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};dialog.ShowDialog();
    }
    internal static string? Ask(string title,string label,string value)
    {
        var window=new Window{Title=title,Width=440,Height=245,WindowStartupLocation=WindowStartupLocation.CenterOwner,ResizeMode=ResizeMode.NoResize};var panel=new StackPanel{Margin=new Thickness(20)};panel.Children.Add(new TextBlock{Text=label,TextWrapping=TextWrapping.Wrap});var input=new TextBox{Text=value,AcceptsReturn=true,Height=85};panel.Children.Add(input);var ok=new Button{Content="Übernehmen",IsDefault=true};ok.Click+=(_,_)=>window.DialogResult=true;panel.Children.Add(ok);window.Content=panel;window.Loaded+=(_,_)=>{input.Focus();input.SelectAll();};return window.ShowDialog()==true?input.Text:null;
    }
    static int Choose(string title,List<string> choices)
    {
        var w=new Window{Title=title,Width=620,Height=450,WindowStartupLocation=WindowStartupLocation.CenterOwner};var dock=new DockPanel{Margin=new Thickness(15)};var b=new Button{Content="Als Kopie wiederherstellen"};DockPanel.SetDock(b,Dock.Bottom);dock.Children.Add(b);var list=new ListBox{ItemsSource=choices};dock.Children.Add(list);b.Click+=(_,_)=>{if(list.SelectedIndex>=0)w.DialogResult=true;};w.Content=dock;return w.ShowDialog()==true?list.SelectedIndex:-1;
    }
}
