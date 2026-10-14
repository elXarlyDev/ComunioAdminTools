param([string]$AppUrl = 'http://localhost:5198')

$ErrorActionPreference = 'Stop'
$browserPath = 'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe'
if (-not (Test-Path -LiteralPath $browserPath)) { throw 'Microsoft Edge is required for this check.' }
$temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')
$profilePath = [IO.Path]::GetFullPath((Join-Path $temporaryRoot ('ComunioLocalization-' + [Guid]::NewGuid().ToString('N'))))
$listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
$listener.Start()
$debugPort = $listener.LocalEndpoint.Port
$listener.Stop()
$browserProcess = $null
$socket = $null
$requestId = 0

function Send-Cdp([string]$Method, [hashtable]$Parameters) {
    $script:requestId++
    $id = $script:requestId
    $bytes = [Text.Encoding]::UTF8.GetBytes((@{ id = $id; method = $Method; params = $Parameters } | ConvertTo-Json -Depth 20 -Compress))
    $null = $socket.SendAsync([ArraySegment[byte]]::new($bytes), [Net.WebSockets.WebSocketMessageType]::Text, $true, [Threading.CancellationToken]::None).GetAwaiter().GetResult()
    $timeout = [Threading.CancellationTokenSource]::new(15000)
    try {
        do {
            $messageStream = [IO.MemoryStream]::new()
            try {
                do {
                    $buffer = New-Object byte[] 65536
                    $received = $socket.ReceiveAsync([ArraySegment[byte]]::new($buffer), $timeout.Token).GetAwaiter().GetResult()
                    $messageStream.Write($buffer, 0, $received.Count)
                } while (-not $received.EndOfMessage)
                $message = [Text.Encoding]::UTF8.GetString($messageStream.ToArray()) | ConvertFrom-Json
            } finally { $messageStream.Dispose() }
        } while ($message.id -ne $id)
        if ($message.error) { throw ($message.error | ConvertTo-Json -Compress) }
        return $message.result
    } finally { $timeout.Dispose() }
}

function Evaluate-Js([string]$Expression) {
    $result = Send-Cdp 'Runtime.evaluate' @{ expression = $Expression; returnByValue = $true; awaitPromise = $true }
    if ($result.exceptionDetails) { throw ($result.exceptionDetails | ConvertTo-Json -Depth 10 -Compress) }
    return $result.result.value
}

function Wait-Js([string]$Expression, [string]$Description) {
    $elapsed = [Diagnostics.Stopwatch]::StartNew()
    while ($elapsed.Elapsed.TotalSeconds -lt 40) {
        if (Evaluate-Js $Expression) { return }
        Start-Sleep -Milliseconds 150
    }
    $diagnostic = Evaluate-Js "JSON.stringify({language:document.documentElement.lang,preference:localStorage.getItem('comunio.language'),screen:document.querySelector('main')?.innerText,errorUi:document.querySelector('#blazor-error-ui')?.outerHTML})"
    throw "Browser check failed: $Description; $diagnostic"
}

try {
    $browserProcess = Start-Process -FilePath $browserPath -ArgumentList @('--headless', '--disable-gpu', '--no-first-run', '--no-default-browser-check', "--remote-debugging-port=$debugPort", ('--user-data-dir="' + $profilePath + '"'), 'about:blank') -WindowStyle Hidden -PassThru
    $page = $null
    for ($attempt = 0; $attempt -lt 50; $attempt++) {
        try {
            $pages = Invoke-RestMethod -Uri "http://127.0.0.1:$debugPort/json/list"
            $page = $pages | Where-Object type -eq 'page' | Select-Object -First 1
            if ($page) { break }
        } catch { }
        Start-Sleep -Milliseconds 100
    }
    if (-not $page) { throw 'Browser debugging endpoint did not start.' }
    $socket = [Net.WebSockets.ClientWebSocket]::new()
    $null = $socket.ConnectAsync([uri]$page.webSocketDebuggerUrl, [Threading.CancellationToken]::None).GetAwaiter().GetResult()
    $null = Send-Cdp 'Page.enable' @{}
    $null = Send-Cdp 'Emulation.setDeviceMetricsOverride' @{ width = 390; height = 844; deviceScaleFactor = 1; mobile = $true }
    # Keep authentication checks isolated from real Comunio accounts and payments.
    $null = Send-Cdp 'Page.addScriptToEvaluateOnNewDocument' @{ source = @'
const originalFetch = window.fetch.bind(window);
window.fetch = async (input, options) => {
    const url = new URL(typeof input === 'string' ? input : input.url, location.href);
    if (url.origin !== 'https://api.comunio.es') return originalFetch(input, options);
    if (url.pathname === '/login') {
        return new Response(JSON.stringify({access_token:'test',refresh_token:'refresh',expires_in:1800}),
            {status: window.rejectTestLogin ? 401 : 200, headers:{'Content-Type':'application/json'}});
    }
    return new Response(JSON.stringify({user:{id:'admin',name:'Test Admin',isAdmin:true},community:{id:'test'}}),
        {status:200,headers:{'Content-Type':'application/json'}});
};
'@ }
    $null = Send-Cdp 'Page.navigate' @{ url = $AppUrl }
    Wait-Js "document.querySelector('#app-language') !== null" 'Language selector is ready'
    Wait-Js "document.querySelector('.login-screen h1')?.textContent === 'Comunio' && document.querySelector('.brand p')?.textContent === 'Admin Tools' && !document.querySelector('.actions') && !document.querySelector('.sign-out')" 'Initial screen only shows login'
    $null = Evaluate-Js "(()=>{const s=document.querySelector('#app-language');s.value='ca-ES-valencia';s.dispatchEvent(new Event('change',{bubbles:true}));})()"
    Wait-Js "document.documentElement.lang === 'ca-ES-valencia' && document.querySelector('.credential span')?.textContent === 'Usuari'" 'Valencian UI and document language'
    Wait-Js "document.querySelector('.login-screen h1')?.textContent === 'Comunio' && document.querySelector('.brand p')?.textContent === 'Admin Tools'" 'Brand is never translated'
    $null = Evaluate-Js "document.querySelector('.action-button.points').click()"
    Wait-Js "document.querySelector('.results-card')?.textContent.includes('Introdu\u00efx l\u2019usuari')" 'Backend validation translated into Valencian'
    $null = Evaluate-Js "(()=>{const s=document.querySelector('#app-language');s.value='es-ES';s.dispatchEvent(new Event('change',{bubbles:true}));})()"
    Wait-Js "document.querySelector('.results-card')?.textContent.includes('Introduce usuario y contrase\u00f1a') && document.documentElement.lang === 'es-ES'" 'Open error dialog switches into Spanish'
    $null = Evaluate-Js "document.querySelector('.results-card button').click()"
    $null = Evaluate-Js "(()=>{document.querySelector('input[autocomplete=username]').value='user';document.querySelector('input[autocomplete=username]').dispatchEvent(new Event('change',{bubbles:true}));document.querySelector('input[type=password]').value='password';document.querySelector('input[type=password]').dispatchEvent(new Event('change',{bubbles:true}));window.rejectTestLogin=true;})()"
    $null = Evaluate-Js "document.querySelector('.login-screen button[type=submit]').click()"
    Wait-Js "!!document.querySelector('.results-card') && !!document.querySelector('.login-screen') && !document.querySelector('.actions')" 'Rejected login stays on login screen'
    $null = Evaluate-Js "document.querySelector('.results-card button').click()"
    $null = Evaluate-Js "(()=>{window.rejectTestLogin=false;const p=document.querySelector('input[type=password]');p.value='password';p.dispatchEvent(new Event('change',{bubbles:true}));})()"
    $null = Evaluate-Js "document.querySelector('.login-screen button[type=submit]').click()"
    Wait-Js "!!document.querySelector('.actions') && !!document.querySelector('.sign-out') && !document.querySelector('.login-screen') && !document.querySelector('#app-language')" 'Successful login opens operations screen'
    Wait-Js "document.querySelectorAll('.action-panel button').length === 5 && document.querySelector('.session-user')?.textContent === 'Test Admin'" 'All five operations and authenticated profile name are present'
    Wait-Js "document.querySelector('.sign-out')?.disabled === false && !document.querySelector('.overlay')" 'Login has finished before logout'
    Wait-Js "(()=>{const h=document.querySelector('.session-header');const r=h.getBoundingClientRect();return r.left===0 && Math.abs(r.width-document.documentElement.clientWidth)<1 && r.height>=52 && r.height<=64 && getComputedStyle(h).backgroundColor==='rgb(11, 77, 44)';})()" 'Mobile header spans the screen with Comunio background and button height'
    $null = Evaluate-Js "document.querySelector('.sign-out').click()"
    Wait-Js "!!document.querySelector('.login-screen') && !!document.querySelector('#app-language') && !document.querySelector('.actions') && document.querySelector('input[type=password]')?.value === ''" 'Logout returns to login and clears credentials'
    Wait-Js "localStorage.getItem('comunio.language') === 'es-ES' && document.querySelector('#blazor-error-ui .reload').textContent === 'Recargar'" 'Preference and fatal error UI are translated'
    $null = Send-Cdp 'Page.reload' @{}
    Wait-Js "document.querySelector('#app-language')?.value === 'es-ES' && document.querySelector('.credential span')?.textContent === 'Usuario'" 'Language persists after reload'
    $null = Evaluate-Js "(()=>{const s=document.querySelector('#app-language');s.value='ca-ES-valencia';s.dispatchEvent(new Event('change',{bubbles:true}));})()"
    Wait-Js "document.querySelector('.credential span')?.textContent === 'Usuari'" 'Switch back to Valencian'
    $null = Send-Cdp 'Page.reload' @{}
    Wait-Js "document.querySelector('#app-language')?.value === 'ca-ES-valencia' && document.querySelector('.credential span')?.textContent === 'Usuari'" 'Valencian persists after reload'
    $null = Evaluate-Js "(()=>{const s=document.querySelector('#app-language');s.value='es-ES';s.dispatchEvent(new Event('change',{bubbles:true}));})()"
    Wait-Js "document.querySelector('.credential span')?.textContent === 'Usuario'" 'Application survives repeated switches'
    Write-Output 'Browser checks passed: login success and failure, logout, fixed brand, five operations, both languages, preference and reload.'
} finally {
    if ($socket -and $socket.State -eq [Net.WebSockets.WebSocketState]::Open) {
        try { $null = Send-Cdp 'Browser.close' @{} } catch { }
    }
    if ($socket) { $socket.Dispose() }
    if ($browserProcess -and -not $browserProcess.HasExited) { Stop-Process -Id $browserProcess.Id -Force -ErrorAction SilentlyContinue }
    if (Test-Path -LiteralPath $profilePath) {
        if (-not $profilePath.StartsWith($temporaryRoot + '\', [StringComparison]::OrdinalIgnoreCase) -or -not [IO.Path]::GetFileName($profilePath).StartsWith('ComunioLocalization-')) { throw 'Unsafe temporary profile path.' }
        Remove-Item -LiteralPath $profilePath -Recurse -Force -ErrorAction SilentlyContinue
    }
}
