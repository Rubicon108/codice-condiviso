using System.Diagnostics;
using System.IO.Ports;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace CodiceCondiviso;

internal static class Program
{
    [STAThread] static void Main() { ApplicationConfiguration.Initialize(); Application.Run(new MainForm()); }
}

public sealed class MainForm : Form
{
    readonly RichTextBox code = new() { Multiline=true, AcceptsTab=true, ScrollBars=RichTextBoxScrollBars.Both, WordWrap=false, Font=new Font("Consolas",11), BackColor=Color.FromArgb(11,17,16), ForeColor=Color.FromArgb(232,241,235), BorderStyle=BorderStyle.None, HideSelection=false, Dock=DockStyle.Fill, Text="// Sketch Arduino\r\nvoid setup() {\r\n  Serial.begin(9600);\r\n}\r\n\r\nvoid loop() {\r\n}\r\n" };
    readonly TextBox output = new() { Multiline=true, ReadOnly=true, ScrollBars=ScrollBars.Vertical, Dock=DockStyle.Fill, Font=new Font("Consolas",9), BackColor=Color.FromArgb(19,28,25), ForeColor=Color.FromArgb(185,201,192), BorderStyle=BorderStyle.None };
    readonly TextBox serialInput = new() { Width=220, BackColor=Color.FromArgb(19,28,25), ForeColor=Color.FromArgb(232,241,235), BorderStyle=BorderStyle.FixedSingle };
    readonly ComboBox ports = new() { Width=100, DropDownStyle=ComboBoxStyle.DropDownList, BackColor=Color.FromArgb(19,28,25), ForeColor=Color.FromArgb(232,241,235), FlatStyle=FlatStyle.Flat };
    readonly Label status = new() { AutoSize=true, Text="Backend preconfigurato · scheda non collegata" };
    readonly HttpClient http = new();
    readonly System.Windows.Forms.Timer poll = new() { Interval=2000 };
    readonly System.Windows.Forms.Timer debounce = new() { Interval=650 };
    SerialPort? serial;
    string? pin;
    long version;
    string baseUrl="https://lvncebxkpxaqwhuxpxoh.supabase.co", anonKey="sb_publishable_Hyk9ELWuz6hkKtQg0t-itw_Qm79HLhb";
    bool remoteBusy=false;
    public MainForm()
    {
        Text="Codice Condiviso · Arduino"; Width=1180; Height=780; MinimumSize=new Size(900,620); BackColor=Color.FromArgb(11,17,16); ForeColor=Color.FromArgb(232,241,235); Font=new Font("Segoe UI",9F);
        var top=new FlowLayoutPanel { Dock=DockStyle.Top, Height=78, Padding=new Padding(10,8,10,6), BackColor=Color.FromArgb(19,28,25), WrapContents=true };
        Add(top,"Apri locale",OpenLocal); Add(top,"Salva locale",SaveLocal); Add(top,"Apri workspace",Join); Add(top,"Nuovo workspace",Create); Add(top,"Configura backend",Configure);
        Add(top,"Installa core Uno",InstallCore); Add(top,"Compila",Compile); Add(top,"Carica",Upload);
        var portLabel=new Label { Text="PORTA", AutoSize=true, Padding=new Padding(6,8,0,0), ForeColor=Color.FromArgb(145,163,154), Font=new Font("Segoe UI",8F,FontStyle.Bold) }; top.Controls.Add(portLabel);
        top.Controls.Add(ports); Add(top,"↻",RefreshPorts);
        status.Margin=new Padding(10,9,2,0);status.ForeColor=Color.FromArgb(181,243,106);top.Controls.Add(status);
        var split=new SplitContainer { Dock=DockStyle.Fill, Orientation=Orientation.Horizontal, SplitterDistance=470, BackColor=Color.FromArgb(43,59,52), BorderStyle=BorderStyle.None, SplitterWidth=5 };
        split.Panel1.BackColor=Color.FromArgb(11,17,16); split.Panel1.Controls.Add(code);
        var bottom=new TableLayoutPanel { Dock=DockStyle.Fill, ColumnCount=1, RowCount=2 };
        bottom.RowStyles.Add(new RowStyle(SizeType.Percent,75)); bottom.RowStyles.Add(new RowStyle(SizeType.Percent,25));
        bottom.Controls.Add(output,0,0);
        var serialRow=new FlowLayoutPanel { Dock=DockStyle.Fill, Padding=new Padding(10), WrapContents=false, BackColor=Color.FromArgb(19,28,25) };
        serialRow.Controls.Add(new Label{Text="MONITOR SERIALE  ·  9600 BAUD",AutoSize=true,Padding=new Padding(0,6,8,0),ForeColor=Color.FromArgb(181,243,106),Font=new Font("Segoe UI",8F,FontStyle.Bold)});
        serialRow.Controls.Add(serialInput); var send=new Button{Text="Invia",AutoSize=true};StyleButton(send,true);send.Click+=async(_,_)=>await SendSerial();serialRow.Controls.Add(send);
        var connect=new Button{Text="Connetti seriale",AutoSize=true};StyleButton(connect,false);connect.Click+=(_,_)=>ConnectSerial(this,EventArgs.Empty);serialRow.Controls.Add(connect);
        bottom.Controls.Add(serialRow,0,1);split.Panel2.Controls.Add(bottom);Controls.Add(split); Controls.Add(top);
        ports.DataSource=SerialPort.GetPortNames(); poll.Tick+=async(_,_)=>await PollRemote();poll.Start(); debounce.Tick+=(_,_)=>{debounce.Stop();SaveWorkspace();}; code.TextChanged+=(_,_)=>{if(pin is not null&&!remoteBusy){debounce.Stop();debounce.Start();}}; LoadConfig();
        FormClosing+=(_,_)=>{try{serial?.Close();}catch{}};
        Log("Pronto. Compilazione e upload usano Arduino CLI installato nell'app o nel PATH.");
    }
    void LoadConfig(){try{var f=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"CodiceCondiviso","backend.json");if(File.Exists(f)){using var d=JsonDocument.Parse(File.ReadAllText(f));baseUrl=d.RootElement.GetProperty("url").GetString()??"";anonKey=d.RootElement.GetProperty("key").GetString()??"";status.Text="Backend configurato";}}catch{}}
    void StyleButton(Button b,bool primary){b.FlatStyle=FlatStyle.Flat;b.FlatAppearance.BorderColor=primary?Color.FromArgb(181,243,106):Color.FromArgb(43,59,52);b.FlatAppearance.MouseOverBackColor=primary?Color.FromArgb(196,255,130):Color.FromArgb(35,49,42);b.BackColor=primary?Color.FromArgb(181,243,106):Color.FromArgb(26,39,33);b.ForeColor=primary?Color.FromArgb(11,17,16):Color.FromArgb(222,233,225);b.Font=new Font("Segoe UI",8.5F,FontStyle.Bold);b.Padding=new Padding(8,2,8,2);}
    void Add(FlowLayoutPanel p,string label,EventHandler action){var b=new Button{Text=label,AutoSize=true,Height=31,Margin=new Padding(4)};StyleButton(b,label=="Compila"||label=="Carica");b.Click+=action;p.Controls.Add(b);}
    void Log(string s){if(output.InvokeRequired){output.BeginInvoke((Action)(()=>Log(s)));return;} output.AppendText($"[{DateTime.Now:HH:mm:ss}] {s}{Environment.NewLine}"); HighlightError(s);}
    async void Configure(object? s,EventArgs e)
    {
        var u=Prompt("URL progetto Supabase (es. https://xxxx.supabase.co)",baseUrl); if(u is null)return;
        var k=Prompt("Chiave publishable Supabase (sb_publishable_...)",anonKey); if(k is null)return;
        baseUrl=u.TrimEnd('/');anonKey=k.Trim(); var dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"CodiceCondiviso");Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(Path.Combine(dir,"backend.json"),JsonSerializer.Serialize(new {url=baseUrl,key=anonKey}));status.Text="Backend configurato";
    }
    static string? Prompt(string title,string initial){using var f=new Form{Text=title,Width=540,Height=145,StartPosition=FormStartPosition.CenterParent};var t=new TextBox{Text=initial,Dock=DockStyle.Top};var b=new Button{Text="OK",DialogResult=DialogResult.OK,Dock=DockStyle.Bottom};f.Controls.Add(t);f.Controls.Add(b);f.AcceptButton=b;return f.ShowDialog()==DialogResult.OK?t.Text:null;}
    async Task<JsonElement> Rpc(string name,object args)
    {
        if(string.IsNullOrWhiteSpace(baseUrl)||string.IsNullOrWhiteSpace(anonKey))throw new Exception("Verifica la configurazione Supabase (URL e chiave publishable).");
        using var req=new HttpRequestMessage(HttpMethod.Post,$"{baseUrl}/rest/v1/rpc/{name}");req.Headers.Add("apikey",anonKey);req.Content=JsonContent.Create(args);
        using var res=await http.SendAsync(req);var text=await res.Content.ReadAsStringAsync();if(!res.IsSuccessStatusCode)throw new Exception(text);using var doc=JsonDocument.Parse(text);return doc.RootElement.Clone();
    }
    async void Join(object? s,EventArgs e){var p=Prompt("Codice numerico workspace","");if(p is null)return;try{var r=await Rpc("read_workspace",new{p_pin=p.Trim()});if(r.ValueKind==JsonValueKind.Null)throw new Exception("Workspace non trovato.");pin=p.Trim();LoadRemote(r);status.Text="Workspace online · "+pin;}catch(Exception ex){Log("Accesso non riuscito: "+ex.Message);}}
    async void Create(object? s,EventArgs e){var p=Prompt("Scegli codice numerico (6–12 cifre)","");if(p is null)return;try{var r=await Rpc("create_workspace",new{p_pin=p.Trim(),p_title="Sketch condiviso",p_source=code.Text});pin=p.Trim();LoadRemote(r);status.Text="Workspace creato · "+pin;Log("Workspace online creato.");}catch(Exception ex){Log("Creazione non riuscita: "+ex.Message);}}
    void LoadRemote(JsonElement r){remoteBusy=true;version=r.GetProperty("version").GetInt64();code.Text=r.GetProperty("source").GetString()??"";remoteBusy=false;poll.Enabled=true;}
    async Task PollRemote(){if(pin is null||remoteBusy)return;try{var r=await Rpc("read_workspace",new{p_pin=pin});if(r.ValueKind==JsonValueKind.Null)return;var v=r.GetProperty("version").GetInt64();var src=r.GetProperty("source").GetString()??"";if(v>version&& !code.Focused){remoteBusy=true;code.Text=src;version=v;remoteBusy=false;status.Text="Aggiornato dal workspace";}}catch{}}
    async void SaveWorkspace(){if(pin is null||remoteBusy)return;try{remoteBusy=true;var r=await Rpc("save_workspace",new{p_pin=pin,p_source=code.Text,p_expected_version=version});version=r.GetProperty("version").GetInt64();status.Text="Workspace sincronizzato";}catch(Exception ex){status.Text="Conflitto di modifica · ricarica workspace";Log("Salvataggio remoto: "+ex.Message);}finally{remoteBusy=false;}}
    void OpenLocal(object? s,EventArgs e){using var d=new OpenFileDialog{Filter="Sketch Arduino (*.ino;*.cpp;*.h)|*.ino;*.cpp;*.h|Tutti i file (*.*)|*.*"};if(d.ShowDialog()!=DialogResult.OK)return;code.Text=File.ReadAllText(d.FileName);Tag=d.FileName;Log("Aperto "+d.FileName);}
    void SaveLocal(object? s,EventArgs e){var path=Tag as string;using var d=new SaveFileDialog{Filter="Sketch Arduino (*.ino)|*.ino",FileName=path is null?"sketch.ino":Path.GetFileName(path)};if(path is null){if(d.ShowDialog()!=DialogResult.OK)return;path=d.FileName;Tag=path;}File.WriteAllText(path,code.Text);Log("Salvato "+path);}
    void HighlightError(string text){var m=Regex.Match(text,@"\.ino:(\d+)(?::\d+)?:\s*error:",RegexOptions.IgnoreCase);if(!m.Success)return;if(!int.TryParse(m.Groups[1].Value,out var n)||n<1||n>code.Lines.Length)return;int start=0;for(int i=0;i<n-1;i++)start+=code.Lines[i].Length+1;code.Select(start,code.Lines[n-1].Length);code.SelectionBackColor=Color.DarkRed;code.SelectionColor=Color.White;}
    async void Compile(object? s,EventArgs e)=>await RunCli(false);
    async void Upload(object? s,EventArgs e)=>await RunCli(true);
    async Task RunCli(bool upload)
    {
        var cli=Path.Combine(AppContext.BaseDirectory,"arduino-cli.exe");if(!File.Exists(cli))cli="arduino-cli";
        code.SelectAll();code.SelectionBackColor=code.BackColor;code.SelectionColor=code.ForeColor;code.Select(code.TextLength,0); var dir=Path.Combine(Path.GetTempPath(),"CodiceCondiviso",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);var file=Path.Combine(dir,Path.GetFileName(dir)+".ino");await File.WriteAllTextAsync(file,code.Text);
        var args=upload?$"compile --fqbn arduino:avr:uno --upload --port {ports.Text} \"{dir}\"":$"compile --fqbn arduino:avr:uno \"{dir}\"";
        await ProcessCli(cli,args);
    }
    async Task ProcessCli(string exe,string args){try{var p=new Process{StartInfo=new ProcessStartInfo(exe,args){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true}};p.OutputDataReceived+=(_,e)=>{if(e.Data is not null)Log(e.Data);};p.ErrorDataReceived+=(_,e)=>{if(e.Data is not null)Log(e.Data);};p.Start();p.BeginOutputReadLine();p.BeginErrorReadLine();await p.WaitForExitAsync();Log(p.ExitCode==0?"Operazione completata.":"Arduino CLI terminato con errori; controlla le righe qui sopra.");}catch(Exception e){Log("Arduino CLI non avviato: "+e.Message);}}
    async void InstallCore(object? s,EventArgs e){await ProcessCli(Path.Combine(AppContext.BaseDirectory,"arduino-cli.exe"),"core update-index");await ProcessCli(Path.Combine(AppContext.BaseDirectory,"arduino-cli.exe"),"core install arduino:avr");}
    void RefreshPorts(object? s,EventArgs e){var selected=ports.Text;ports.DataSource=SerialPort.GetPortNames();if(ports.Items.Contains(selected))ports.SelectedItem=selected;}
    void ConnectSerial(object? s,EventArgs e){try{serial?.Close();serial=new SerialPort(ports.Text,9600){NewLine="\n",ReadTimeout=1000};serial.DataReceived+=(_,_)=>{try{var line=serial.ReadLine();Log("[seriale] "+line.TrimEnd());}catch{}};serial.Open();status.Text="Seriale connessa · 9600 baud";}catch(Exception ex){Log("Connessione seriale fallita: "+ex.Message);}}
    async Task SendSerial(){try{if(serial?.IsOpen!=true)throw new Exception("Connetti prima la porta seriale.");serial.WriteLine(serialInput.Text);serialInput.Clear();await Task.CompletedTask;}catch(Exception e){Log("Invio seriale: "+e.Message);}}
}