param()

$ErrorActionPreference = 'Stop'
$browserPath = 'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe'
if (-not (Test-Path -LiteralPath $browserPath)) { throw 'Microsoft Edge is required for this check.' }
$temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')
$profilePath = [IO.Path]::GetFullPath((Join-Path $temporaryRoot ('ComunioLineupDrag-' + [Guid]::NewGuid().ToString('N'))))
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
    $null = Send-Cdp 'Emulation.setDeviceMetricsOverride' @{ width = 700; height = 700; deviceScaleFactor = 1; mobile = $true }
    $null = Send-Cdp 'Emulation.setTouchEmulationEnabled' @{ enabled = $true }
    $modulePath = Join-Path $PSScriptRoot '../../ComunioAdminTools.UI/wwwroot/lineupDrag.js'
    $moduleUrl = 'data:text/javascript;base64,' + [Convert]::ToBase64String([IO.File]::ReadAllBytes($modulePath))
    $null = Evaluate-Js @'
(() => {
    document.body.innerHTML = `<style>
        body { margin:0; font:14px Arial; } #root { display:flex; gap:20px; margin:80px 20px; }
        section { width:280px; min-height:500px; } li { display:block; height:80px; margin:10px; background:#eee; user-select:none; }
        ul { padding:0; margin:0; } .lineup-drop-target { outline:2px dashed green; }
    </style><div id="root"><section><ul>
        <li id="starter" data-lineup-player="starter" data-player-position="Striker" data-lineup-position="Striker" data-lineup-slot="0" tabindex="0">Starter</li>
        <li id="empty" data-lineup-slot="1" data-lineup-position="Striker" tabindex="0">Empty</li>
        <li id="defender-slot" data-lineup-slot="2" data-lineup-position="Defender" tabindex="0">Defender slot</li>
    </ul></section><section id="bench" data-lineup-bench tabindex="0"><ul>
        <li id="substitute" data-lineup-player="substitute" data-player-position="Striker" data-lineup-substitute="substitute" tabindex="0">Substitute</li>
        <li id="defender-sub" data-lineup-player="defender" data-player-position="Defender" data-lineup-substitute="defender" tabindex="0">Defender substitute</li>
    </ul></section></div>`;
    window.moves = [];
    window.bridge = { invokeMethodAsync: async (method, ...args) => { moves.push({method,args}); } };
    window.root = document.querySelector('#root');
    return true;
})()
'@
    $null = Evaluate-Js "(async()=>{window.dragModule=await import('$moduleUrl');dragModule.attach(root,bridge);return true;})()"

    function Center([string]$Selector) {
        return Evaluate-Js "(()=>{const r=document.querySelector('$Selector').getBoundingClientRect();return {x:r.x+r.width/2,y:r.y+r.height/2};})()"
    }

    function Mouse-Drag([string]$Source, [string]$Target, [bool]$Cancel = $false) {
        $from = Center $Source
        $to = Center $Target
        $null = Send-Cdp 'Input.dispatchMouseEvent' @{ type = 'mousePressed'; x = $from.x; y = $from.y; button = 'left'; clickCount = 1 }
        $null = Send-Cdp 'Input.dispatchMouseEvent' @{ type = 'mouseMoved'; x = $to.x; y = $to.y; button = 'left'; buttons = 1 }
        if ($Cancel) { $null = Evaluate-Js "root.dispatchEvent(new KeyboardEvent('keydown',{key:'Escape',bubbles:true}))" }
        $null = Send-Cdp 'Input.dispatchMouseEvent' @{ type = 'mouseReleased'; x = $to.x; y = $to.y; button = 'left'; clickCount = 1 }
    }

    function Check-Move([int]$Count, [string]$Player, $Slot, $Substitute) {
        $expected = ConvertTo-Json -InputObject @($Player, $Slot, $Substitute) -Compress
        Wait-Js "moves.length===$Count && JSON.stringify(moves.at(-1).args)===JSON.stringify($expected) && moves.at(-1).method==='MoveLineupPlayer'" 'Drag callback and destination'
    }

    Mouse-Drag '#substitute' '#empty'
    Check-Move 1 'substitute' 1 $null
    Mouse-Drag '#substitute' '#starter'
    Check-Move 2 'substitute' 0 $null
    Mouse-Drag '#starter' '#substitute'
    Check-Move 3 'starter' $null 'substitute'
    Mouse-Drag '#starter' '#bench'
    Check-Move 4 'starter' $null $null
    Mouse-Drag '#substitute' '#empty' $true
    if (-not (Evaluate-Js 'moves.length===4 && !document.querySelector(".drag-selected")')) { throw 'Escape failed to cancel mouse drag.' }

    $null = Evaluate-Js "document.querySelector('#substitute').dispatchEvent(new KeyboardEvent('keydown',{key:'Enter',bubbles:true}));document.querySelector('#empty').dispatchEvent(new KeyboardEvent('keydown',{key:' ',bubbles:true}));"
    Check-Move 5 'substitute' 1 $null
    $null = Evaluate-Js "document.querySelector('#substitute').dispatchEvent(new KeyboardEvent('keydown',{key:'Enter',bubbles:true}));root.dispatchEvent(new KeyboardEvent('keydown',{key:'Escape',bubbles:true}));"
    if (-not (Evaluate-Js 'moves.length===5 && !document.querySelector(".drag-selected")')) { throw 'Escape failed to cancel keyboard move.' }

    $from = Center '#substitute'
    $to = Center '#empty'
    $null = Send-Cdp 'Input.dispatchTouchEvent' @{ type = 'touchStart'; touchPoints = @(@{ x = $from.x; y = $from.y; id = 1 }) }
    $null = Evaluate-Js 'new Promise(resolve=>setTimeout(()=>resolve(true),280))'
    $null = Send-Cdp 'Input.dispatchTouchEvent' @{ type = 'touchMove'; touchPoints = @(@{ x = $to.x; y = $to.y; id = 1 }) }
    $null = Send-Cdp 'Input.dispatchTouchEvent' @{ type = 'touchEnd'; touchPoints = @() }
    Check-Move 6 'substitute' 1 $null

    $null = Evaluate-Js 'dragModule.detach(root)'
    Mouse-Drag '#substitute' '#empty'
    if (-not (Evaluate-Js 'moves.length===6')) { throw 'Disposed handlers still invoke callbacks.' }
    $null = Evaluate-Js 'dragModule.attach(root,bridge);dragModule.attach(root,bridge)'
    Mouse-Drag '#substitute' '#empty'
    Check-Move 7 'substitute' 1 $null
    Mouse-Drag '#substitute' '#defender-slot'
    Mouse-Drag '#starter' '#defender-sub'
    if (-not (Evaluate-Js 'moves.length===7')) { throw 'Positional restrictions did not reject incompatible swaps.' }
    $null = Evaluate-Js "document.querySelector('#empty').dataset.lineupPosition='Defender'"
    Mouse-Drag '#substitute' '#empty'
    if (-not (Evaluate-Js 'moves.length===7')) { throw 'The drop target ignored its updated formation role.' }
    if (-not (Evaluate-Js '!document.querySelector(".drag-selected") && !document.querySelector(".lineup-drop-target") && !document.querySelector("[aria-hidden=true]")')) { throw 'Drag visuals were not cleaned up.' }
    $helpPath = Join-Path $PSScriptRoot '../../ComunioAdminTools.UI/wwwroot/lineupHelp.js'
    $helpUrl = 'data:text/javascript;base64,' + [Convert]::ToBase64String([IO.File]::ReadAllBytes($helpPath))
    $null = Evaluate-Js "(async()=>{window.helpModule=await import('$helpUrl');return true;})()"
    $null = Evaluate-Js @'
(() => {
    document.body.insertAdjacentHTML('beforeend', '<button id="info">Info</button><dialog id="help"><h2>Lineup help</h2><form method="dialog"><button id="close-help" type="submit" autofocus>Close</button></form></dialog>');
    window.infoButton = document.querySelector('#info');
    window.helpDialog = document.querySelector('#help');
    infoButton.addEventListener('click', () => helpModule.open(helpDialog));
    infoButton.focus();
    infoButton.click();
    helpModule.open(helpDialog);
    return true;
})()
'@
    Wait-Js 'helpDialog.open && helpDialog.matches(":modal") && document.activeElement.id==="close-help"' 'Help opens as a modal and focuses its close button'
    $null = Send-Cdp 'Input.dispatchKeyEvent' @{ type = 'keyDown'; key = 'Escape'; code = 'Escape'; windowsVirtualKeyCode = 27 }
    $null = Send-Cdp 'Input.dispatchKeyEvent' @{ type = 'keyUp'; key = 'Escape'; code = 'Escape'; windowsVirtualKeyCode = 27 }
    Wait-Js '!helpDialog.open && document.activeElement===infoButton' 'Escape closes help and restores focus'
    $null = Evaluate-Js 'infoButton.click();document.querySelector("#close-help").click()'
    Wait-Js '!helpDialog.open && document.activeElement===infoButton' 'Close button closes help and restores focus'
    Write-Output 'Lineup browser checks passed: drag restrictions, mouse/touch/keyboard, help modal, Escape, close and restored focus; no network or real accounts.'
} finally {
    if ($socket -and $socket.State -eq [Net.WebSockets.WebSocketState]::Open) {
        try { $null = Send-Cdp 'Browser.close' @{} } catch { }
    }
    if ($socket) { $socket.Dispose() }
    if ($browserProcess -and -not $browserProcess.HasExited) { Stop-Process -Id $browserProcess.Id -Force -ErrorAction SilentlyContinue }
    if (Test-Path -LiteralPath $profilePath) {
        if (-not $profilePath.StartsWith($temporaryRoot + '\', [StringComparison]::OrdinalIgnoreCase) -or -not [IO.Path]::GetFileName($profilePath).StartsWith('ComunioLineupDrag-')) { throw 'Unsafe temporary profile path.' }
        Remove-Item -LiteralPath $profilePath -Recurse -Force -ErrorAction SilentlyContinue
    }
}
