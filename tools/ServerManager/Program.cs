using System.Diagnostics;
using System.ServiceProcess;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using System.Data.Common;

ApplicationConfiguration.Initialize();
var root = args.Contains("--root") ? args[Array.IndexOf(args,"--root") + 1] : Directory.GetParent(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar))!.FullName;
if(!args.Contains("--root") && !File.Exists(Path.Combine(root,"services","AliasBackend.xml")))root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Alias Serveur");
var form = new ManagerForm(root);
if(args.Contains("--preview")) { form.Show(); form.Refresh(); using var bitmap = new Bitmap(form.Width,form.Height); form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size)); bitmap.Save(args[Array.IndexOf(args,"--preview")+1]); form.Close(); return; }
Application.Run(form);

sealed class ManagerForm : Form
{
 readonly string root;
 readonly Label status=new(){AutoSize=true}, address=new(){AutoSize=true}, feedback=new(){AutoSize=true};
 readonly NumericUpDown front=new(){Minimum=1024,Maximum=65535,Value=3006,Width=170}, back=new(){Minimum=1024,Maximum=65535,Value=5086,Width=170};
 readonly TextBox url=new(){Width=680};
 readonly List<Button> actions=[];
 readonly System.Windows.Forms.Timer timer=new(){Interval=3000};
 bool busy;
 string data="";
 public ManagerForm(string root)
 {
  this.root=Path.GetFullPath(root); Text="Alias — Gestion du serveur"; Size=new(870,700); MinimumSize=new(800,650); StartPosition=FormStartPosition.CenterScreen;
  Font=new("Segoe UI",10); BackColor=Color.FromArgb(242,246,252);
  var icon=Path.Combine(root,"Alias.ico"); if(File.Exists(icon)) Icon=new Icon(icon);
  var layout=new FlowLayoutPanel(){Dock=DockStyle.Fill,Padding=new Padding(20),FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true}; Controls.Add(layout);
  layout.Controls.Add(new Label(){Text="Gestion du serveur Alias",Font=new("Segoe UI",21,FontStyle.Bold),AutoSize=true,ForeColor=Color.FromArgb(18,48,79)});
  layout.Controls.Add(new Label(){Text="Services, accès réseau et base de données",AutoSize=true,Margin=new Padding(0,4,0,14)});
  layout.Controls.Add(status); layout.Controls.Add(address);
  var services=new FlowLayoutPanel(){AutoSize=true,Margin=new Padding(0,10,0,12)};
  AddButton(services,"Démarrer",()=>Run("Start")); AddButton(services,"Redémarrer",()=>ConfirmRun("Restart")); AddButton(services,"Arrêter",()=>ConfirmRun("Stop"));
  AddButton(services,"Ouvrir Alias",()=>Open(GetSettings()["PublicUrl"]!.ToString())); layout.Controls.Add(services);
  layout.Controls.Add(new Label(){Text="Ports et adresse des postes",Font=new("Segoe UI",13,FontStyle.Bold),AutoSize=true});
  var ports=new FlowLayoutPanel(){AutoSize=true}; ports.Controls.Add(new Label(){Text="Frontend",AutoSize=true,Padding=new Padding(0,5,0,0)});ports.Controls.Add(front);ports.Controls.Add(new Label(){Text="Backend",AutoSize=true,Padding=new Padding(20,5,0,0)});ports.Controls.Add(back); layout.Controls.Add(ports);
  layout.Controls.Add(new Label(){Text="Adresse publique HTTP (nom ou IP du serveur + port frontend)",AutoSize=true});layout.Controls.Add(url);
  var config=new FlowLayoutPanel(){AutoSize=true,Margin=new Padding(0,6,0,12)};
  AddButton(config,"Appliquer les ports",()=>ConfirmRun("Ports")); AddButton(config,"Copier l’adresse",()=>{Clipboard.SetText(GetSettings()["PublicUrl"]!.ToString());feedback.Text="Adresse copiée.";});layout.Controls.Add(config);
  layout.Controls.Add(new Label(){Text="Base de données et administration",Font=new("Segoe UI",13,FontStyle.Bold),AutoSize=true});
  var database=new FlowLayoutPanel(){AutoSize=true};
  AddButton(database,"Ouvrir pgAdmin",OpenPgAdmin);AddButton(database,"Connexion PostgreSQL",ShowDatabase);AddButton(database,"Configuration OCR",()=>Open(GetSettings()["PublicUrl"]!.ToString().TrimEnd('/')+"/settings"));layout.Controls.Add(database);
  var logs=new FlowLayoutPanel(){AutoSize=true};AddButton(logs,"Voir les journaux",()=>Open(Path.Combine(data,"logs")));AddButton(logs,"Actualiser",()=>{LoadConfiguration();RefreshServices();}); layout.Controls.Add(logs);
  feedback.MaximumSize=new Size(740,0); feedback.ForeColor=Color.FromArgb(40,80,115);feedback.Margin=new Padding(0,16,0,0);layout.Controls.Add(feedback);
  var footer=new Panel(){Dock=DockStyle.Bottom,Height=58,Padding=new Padding(26,8,26,8),BackColor=Color.White};
  feedback.AutoSize=false;feedback.MaximumSize=Size.Empty;feedback.Dock=DockStyle.Fill;feedback.Margin=Padding.Empty;
  footer.Controls.Add(feedback);Controls.Add(footer);
  front.ValueChanged+=(_,_)=>{if(Uri.TryCreate(url.Text,UriKind.Absolute,out var current)){var builder=new UriBuilder(current){Port=(int)front.Value};url.Text=builder.Uri.GetLeftPart(UriPartial.Authority);}};
  LoadConfiguration();RefreshServices();timer.Tick+=(_,_)=>RefreshServices();timer.Start();FormClosed+=(_,_)=>timer.Dispose();
  FormClosing+=(_,e)=>{if(busy && e.CloseReason==CloseReason.UserClosing){e.Cancel=true;MessageBox.Show(this,"Attendez la fin de l’action en cours avant de fermer cette fenêtre.","Alias");}};
 }
 JsonObject GetSettings()=>JsonNode.Parse(File.ReadAllText(Path.Combine(data,"Server.json")))!.AsObject();
 void LoadConfiguration()
 {
  try{var xml=XDocument.Load(Path.Combine(root,"services","AliasBackend.xml"));var path=xml.Root!.Elements("env").Single(e=>(string?)e.Attribute("name")=="ALIAS_CONFIG_FILE").Attribute("value")!.Value;data=Path.GetDirectoryName(path)!;var settings=GetSettings();front.Value=(int)settings["FrontendPort"]!;back.Value=(int)settings["BackendPort"]!;url.Text=settings["PublicUrl"]!.ToString();address.Text="Accès des postes : "+url.Text;}
  catch(Exception e){feedback.Text="Serveur non configuré : "+e.Message;}
 }
 void RefreshServices()
 {
  var entries=new List<string>();foreach(var name in new[]{"AliasPostgreSQL","AliasBackend","AliasFrontend"}){try{using var service=new ServiceController(name);entries.Add(name.Replace("Alias","")+" : "+(service.Status==ServiceControllerStatus.Running?"Démarré":service.Status.ToString()));}catch{entries.Add(name.Replace("Alias","")+" : non installé");}}status.Text=string.Join("     |     ",entries);
 }
 void AddButton(Control parent,string text,Action action)
 {
  var button=new Button(){Text=text,AutoSize=true,Padding=new Padding(10,4,10,4),FlatStyle=FlatStyle.Flat,BackColor=Color.White,ForeColor=Color.FromArgb(17,78,130),Margin=new Padding(0,5,10,5)};
  button.Click+=(_,_)=>{try{action();}catch(Exception e){MessageBox.Show(this,e.Message,"Alias",MessageBoxButtons.OK,MessageBoxIcon.Error);}};actions.Add(button);parent.Controls.Add(button);
 }
 void ConfirmRun(string action)
 {
  var text=action=="Ports"?"Appliquer ces ports ? L’accès des utilisateurs sera interrompu. Les raccourcis des postes devront utiliser la nouvelle adresse.":"Cette action interrompt l’accès des utilisateurs. Continuer ?";
  if(MessageBox.Show(this,text,"Gestion du serveur",MessageBoxButtons.YesNo,MessageBoxIcon.Question)==DialogResult.Yes)Run(action);
 }
 async void Run(string action)
 {
  if(busy)return;busy=true;actions.ForEach(b=>b.Enabled=false);feedback.Text="Action en cours…";
  try{
   var start=new ProcessStartInfo("powershell.exe"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
   foreach(var argument in new[]{"-NoProfile","-ExecutionPolicy","Bypass","-File",Path.Combine(AppContext.BaseDirectory,"Server-Control.ps1"),"-Action",action,"-AppRoot",root})start.ArgumentList.Add(argument);
   if(action=="Ports"){foreach(var argument in new[]{"-FrontendPort",front.Value.ToString(),"-BackendPort",back.Value.ToString(),"-PublicUrl",url.Text.Trim()})start.ArgumentList.Add(argument);}
   using var process=Process.Start(start)!;var output=process.StandardOutput.ReadToEndAsync();var errors=process.StandardError.ReadToEndAsync();await process.WaitForExitAsync();
   if(process.ExitCode!=0)throw new Exception(await errors);feedback.Text=(await output).Trim();LoadConfiguration();
  }catch(Exception e){feedback.Text="Échec : "+e.Message;}finally{busy=false;actions.ForEach(b=>b.Enabled=true);RefreshServices();}
 }
 static void Open(string target)=>Process.Start(new ProcessStartInfo(target){UseShellExecute=true});
 void OpenPgAdmin()
 {
  var folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"PostgreSQL");
  var paths=Directory.Exists(folder)?Directory.GetDirectories(folder).Select(p=>Path.Combine(p,"pgAdmin 4","runtime","pgAdmin4.exe")):[];
  var executable=paths.FirstOrDefault(File.Exists);if(executable is null){MessageBox.Show(this,"pgAdmin n’est pas installé. Le bouton Connexion PostgreSQL fournit les informations de connexion.","Alias");return;}Open(executable);
 }
 void ShowDatabase()
 {
  string host,user,password,database,port;
  if(GetSettings()["DatabaseMode"]?.ToString()=="0") {var info=JsonNode.Parse(File.ReadAllText(Path.Combine(data,"Postgres.Admin.json")))!;host="127.0.0.1";user="alias_admin";password=info["AdminPassword"]!.ToString();port=info["Port"]!.ToString();database="alias";}
  else {var info=JsonNode.Parse(File.ReadAllText(Path.Combine(data,"Alias.Runtime.json")))!;var connection=new DbConnectionStringBuilder(){ConnectionString=info["ConnectionStrings"]!["Default"]!.ToString()};string Value(string key,string fallback="")=>connection.TryGetValue(key,out var value)?value.ToString()!:fallback;host=Value("Host");user=Value("Username");password=Value("Password");port=Value("Port","5432");database=Value("Database");}
  using var dialog=new Form(){Text="Connexion PostgreSQL — administrateur",Size=new(520,330),StartPosition=FormStartPosition.CenterParent,Font=Font};
  var panel=new FlowLayoutPanel(){Dock=DockStyle.Fill,Padding=new Padding(20),FlowDirection=FlowDirection.TopDown,WrapContents=false};dialog.Controls.Add(panel);
  panel.Controls.Add(new Label(){Text=$"Hôte : {host}\nPort : {port}\nBase : {database}\nUtilisateur : {user}\nBase de maintenance pgAdmin : {database}",AutoSize=true});
  var secret=new TextBox(){Text=password,UseSystemPasswordChar=true,ReadOnly=true,Width=460};panel.Controls.Add(secret);
  var reveal=new CheckBox(){Text="Afficher le mot de passe",AutoSize=true};reveal.CheckedChanged+=(_,_)=>secret.UseSystemPasswordChar=!reveal.Checked;panel.Controls.Add(reveal);
  var copy=new Button(){Text="Copier le mot de passe",AutoSize=true};copy.Click+=(_,_)=>Clipboard.SetText(password);panel.Controls.Add(copy);dialog.ShowDialog(this);
 }
}
