using Microsoft.Web.WebView2.Core;

namespace QuickLaunch
{
    public partial class LoginView : Form
    {
        public string? LoginEmail { get; set; }
        public CoreWebView2Cookie? NxLCookie { get; private set; }

        private CoreWebView2Frame? _loginFrame;

        public LoginView()
        {
            InitializeComponent();
        }

        private async void LoginView_Shown(object sender, EventArgs e)
        {
            NxLCookie = null;

            var storagePath = Environment.ProcessPath + ".WebView2";
            try
            {
                Directory.Delete(storagePath, true);
            }
            catch { }

            // Nexon goes cross origin every other request.
            var opts = new CoreWebView2EnvironmentOptions("--disable-web-security");
            var env = await CoreWebView2Environment.CreateAsync(null, null, opts);
            webView21.CoreWebView2InitializationCompleted += CoreWebInitialized;
            await webView21.EnsureCoreWebView2Async(env);
        }

        public async void CoreWebInitialized(object? sender, EventArgs e)
        {
            var core = webView21.CoreWebView2;

            core.Settings.UserAgent = NxlUtil.NxlUserAgent;
            core.FrameCreated += OnFrameCreated;
            core.WebMessageReceived += OnIpcCall;

            var sessionId = NxlUtil.GenSessionId();
            var deviceId = NxlUtil.GetDeviceId();

            // Set up fake Electron environment.
            await core.AddScriptToExecuteOnDocumentCreatedAsync(@"
                original_log = console.log; // sentry instrumentation bs will override it
                process = {env: Proxify({REACT_APP_VERSION: '" + NxlUtil.NxlFeVersion + @"'}, 'env')};
                settings = Proxify({hosts: Proxify({
                    accounts: 'https://www.nexon.com',
                    api: 'https://api.nexon.io/',
                    cdn: 'https://nxl.nxfs.nexon.com/',
                    remoteweb: 'https://nxl.nxfs.nexon.com/',
                    use_custom: false
                }, 'hosts')}, 'settings');
                application = Proxify({params: Proxify({
                        environment: 'live',
                        region: 'global',
                        device_id: '" + deviceId + @"',
                        locale: 'en_US',
                        default_locale: 'en_US',
                        user_data: {}
                    }, 'params'),
                    sessionId: '" + sessionId + @"'}, 'application');
                electron = {application: application, show_window: ()=>{}};
                win = {isMinimized: ()=>false, isMaximized: ()=>false, isVisible: ()=>true, on: ()=>{}};
                coreappIpc = Proxify({on: (s)=>{console.log(s)}, send: (s)=>{window.chrome.webview.postMessage(s)}}, 'coreappIpc');
                let mockSub = {sub: (callback)=>{}};
                nxlApi = {frontEnd: mockSub, appsInstaller: mockSub, appsInstallerEvents: mockSub, appsLauncherEvents: mockSub, localization: mockSub};
                
                function Proxify(o, n){ return new Proxy(o, {get(target,name){console.log=original_log; if (!target.hasOwnProperty(name)) console.log(n + ': Need ' + name); return target[name] }})}
            ");
            core.Navigate("https://nxl.nxfs.nexon.com/nxl/login?index_tag=live");
        }

        private void OnFrameCreated(object? sender, CoreWebView2FrameCreatedEventArgs e)
        {
            _loginFrame = e.Frame;
            e.Frame.NavigationCompleted += OnFrameNavigationCompleted;
        }

        private void OnFrameNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            _loginFrame?.ExecuteScriptAsync(@"const setit = ()=>{
                setTimeout(() => {
                    const input = document.querySelector('#id');
                    if (input) {
                        input.value = '" + LoginEmail + @"';
                        input.dispatchEvent(new Event('input'));
                        setTimeout(()=>{document.querySelector('#submitContinue').click()},100)
                    }
                    else setit();
                }, 100)
            }; setit()");
        }

        private async void OnIpcCall(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            var str = e.TryGetWebMessageAsString();

            if (str == "openMainWindow")
            {
                var cookies = await webView21.CoreWebView2.CookieManager.GetCookiesAsync("https://www.nexon.com");
                var tokenArr = cookies.Where(c => c.Name == "NxLSession").ToArray();
                if (tokenArr.Length > 0)
                {
                    NxLCookie = tokenArr[0];
                }
                Close();
            }
        }
    }
}
