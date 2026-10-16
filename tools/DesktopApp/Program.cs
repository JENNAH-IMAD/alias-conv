using System.Diagnostics;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

ApplicationConfiguration.Initialize();
var root=Directory.GetParent(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar))!.FullName;
Application.Run(new DesktopWindow(root));

sealed class DesktopWindow : Form
{
    readonly WebView2 browser=new(){Dock=DockStyle.Fill};
    readonly Label status=new(){Text="Connexion à votre espace…",Dock=DockStyle.Bottom,Height=28,Padding=new Padding(16,5,0,0)};
    readonly string root;
    Uri? address;
    public DesktopWindow(string root)
    {
        this.root=root; Text="Alias · Relevés & comptabilité"; Size=new(1360,900);MinimumSize=new(850,600);StartPosition=FormStartPosition.CenterScreen;
        Font=new("Segoe UI",10);BackColor=Color.FromArgb(243,247,252);status.ForeColor=Color.FromArgb(70,92,122);
        var icon=Path.Combine(root,"Alias.ico");if(File.Exists(icon))Icon=new Icon(icon);
        var bar=new FlowLayoutPanel{Dock=DockStyle.Top,Height=60,Padding=new Padding(16,10,16,8),BackColor=Color.White,WrapContents=false};
        bar.Controls.Add(new Label{Text="Alias",AutoSize=true,Font=new("Segoe UI",20,FontStyle.Bold),ForeColor=Color.FromArgb(15,101,222),Margin=new Padding(0,0,24,0)});
        Add(bar,"Vue d’ensemble","/dashboard");Add(bar,"Convertir un PDF","/conversions?import=1");Add(bar,"Historique","/history");
        var refresh=new Button{Text="Actualiser",AutoSize=true,FlatStyle=FlatStyle.Flat,Padding=new Padding(8,4,8,4)};
        refresh.Click+=async(_,_)=>{if(browser.CoreWebView2!=null)browser.Reload();else await Connect();};bar.Controls.Add(refresh);
        Controls.Add(browser);Controls.Add(status);Controls.Add(bar);
        Shown+=async(_,_)=>await Connect();FormClosed+=(_,_)=>browser.Dispose();
    }
    void Add(Control parent,string label,string path)
    {
        var button=new Button{Text=label,AutoSize=true,FlatStyle=FlatStyle.Flat,BackColor=Color.White,ForeColor=Color.FromArgb(30,61,99),Padding=new Padding(10,5,10,5),Margin=new Padding(0,0,8,0)};
        button.FlatAppearance.BorderColor=Color.FromArgb(214,226,242);
        button.Click+=(_,_)=>{if(address!=null && browser.CoreWebView2!=null)browser.CoreWebView2.Navigate(new Uri(address,path).ToString());};parent.Controls.Add(button);
    }
    async Task Connect()
    {
        try
        {
            var pointer=Path.Combine(root,"Desktop.json");
            using var settings=JsonDocument.Parse(File.ReadAllText(pointer));
            address=new Uri(settings.RootElement.GetProperty("Url").GetString()!);
            if(!address.IsLoopback || address.Scheme!="http")throw new InvalidOperationException("Adresse desktop locale invalide.");
            status.Text="Connexion aux services locaux…";
            using var http=new HttpClient{Timeout=TimeSpan.FromSeconds(3)};var ready=false;
            for(var i=0;i<15;i++)
            {
                try{using var response=await http.GetAsync(new Uri(address,"/api/auth/session"));if(response.IsSuccessStatusCode){ready=true;break;}}catch(HttpRequestException){}catch(TaskCanceledException){}
                await Task.Delay(1000);
            }
            if(!ready)throw new InvalidOperationException("Services locaux indisponibles. Vérifiez les services AliasDesktopPostgreSQL, AliasDesktopBackend et AliasDesktopFrontend dans Windows, puis cliquez sur Actualiser.");
            var profile=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Alias Desktop","Browser");
            var environment=await CoreWebView2Environment.CreateAsync(userDataFolder:profile);
            await browser.EnsureCoreWebView2Async(environment);
            browser.CoreWebView2.Settings.IsStatusBarEnabled=false;
            browser.CoreWebView2.Settings.AreDevToolsEnabled=false;
            browser.CoreWebView2.NewWindowRequested+=(_,e)=>{e.Handled=true;if(Uri.TryCreate(e.Uri,UriKind.Absolute,out var uri) && uri.Scheme is "http" or "https")Process.Start(new ProcessStartInfo(uri.ToString()){UseShellExecute=true});};
            browser.CoreWebView2.PermissionRequested+=(_,e)=>{e.State=CoreWebView2PermissionState.Deny;};
            browser.CoreWebView2.Navigate(new Uri(address,"/dashboard").ToString());status.Text="Application locale · PDF, conversions et exports";
        }
        catch(WebView2RuntimeNotFoundException){status.Text="Microsoft WebView2 est nécessaire. Relancez l’installation de WebView2, puis ouvrez Alias.";MessageBox.Show(this,status.Text,"Alias",MessageBoxButtons.OK,MessageBoxIcon.Information);}
        catch(Exception e){status.Text=e.Message;MessageBox.Show(this,e.Message,"Alias",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
    }
}
