param(
    [ValidateSet('StarMapEditor','ZylPresentationEditor')][string]$Editor = 'StarMapEditor',
    [ValidateRange(1024,65535)][int]$Port = 47862,
    [switch]$NoBrowser,
    [switch]$LoadFunctionsOnly
)

$ErrorActionPreference = 'Stop'
$ToolsRoot = Split-Path -Parent $PSScriptRoot
$Utf8 = [Text.UTF8Encoding]::new($false, $true)
$FileLimit = 2MB
$RequestLimit = 16MB
$Origin = "http://127.0.0.1:$Port"
$Grants = @{}

$StaticFiles = @(
    'index.html','editor.css','editor.js','editor-file-location.js',
    'default-campaign.js','editor-core.js','editor-file-store.js',
    'parameters.js','countdown-config.js','dialogue-model.js','dialogue-editor.js'
)

function Hash-Bytes([byte[]]$Bytes) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try { [Convert]::ToBase64String($sha.ComputeHash($Bytes)) }
    finally { $sha.Dispose() }
}

$ToolMarker = Hash-Bytes ($Utf8.GetBytes($ToolsRoot.ToUpperInvariant()))

function Read-Snapshot([string]$Path, [bool]$IncludeText) {
    if (![IO.File]::Exists($Path)) {
        return @{ version = 'missing'; text = ''; modified = 0 }
    }
    if ([IO.FileInfo]::new($Path).Length -gt $FileLimit) { throw '配置文件超过 2 MB。' }
    $bytes = [IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -gt $FileLimit) { throw '配置文件超过 2 MB。' }
    $result = @{
        version = Hash-Bytes $bytes
        modified = ([DateTimeOffset]([IO.File]::GetLastWriteTimeUtc($Path))).ToUnixTimeMilliseconds()
    }
    if ($IncludeText) { $result.text = $Utf8.GetString($bytes).TrimStart([char]0xfeff) }
    return $result
}

function Select-EditorFile([string]$Operation, [string]$Tool, [string]$SuggestedName) {
    Add-Type -AssemblyName System.Windows.Forms
    $dialog = if ($Operation -eq 'open') { New-Object Windows.Forms.OpenFileDialog } else { New-Object Windows.Forms.SaveFileDialog }
    $owner = New-Object Windows.Forms.Form
    try {
        # Keep an owner for the native dialog without displaying an extra form.
        $owner.ShowInTaskbar = $false
        $owner.TopMost = $true
        $dialog.Filter = 'JSON 配置 (*.json)|*.json'
        $dialog.DefaultExt = 'json'
        $dialog.AddExtension = $true
        $dialog.CheckPathExists = $true
        $dialog.RestoreDirectory = $true
        $folder = Join-Path $ToolsRoot '..\UnityProject\Assets\StreamingAssets\Navigation\zyl'
        if (Test-Path -LiteralPath $folder -PathType Container) { $dialog.InitialDirectory = [IO.Path]::GetFullPath($folder) }
        $dialog.Title = if ($Operation -eq 'open') { '打开配置' } else { '保存配置' }
        if ($Operation -eq 'open') { $dialog.CheckFileExists = $true }
        else {
            $dialog.OverwritePrompt = $true
            $dialog.FileName = [IO.Path]::GetFileName($SuggestedName)
        }
        if ($dialog.ShowDialog($owner) -eq [Windows.Forms.DialogResult]::OK) { return $dialog.FileName }
    } finally { $dialog.Dispose(); $owner.Dispose() }
}

function Get-Grant($InputObject) {
    if ($InputObject.id -isnot [string] -or !$Grants.ContainsKey($InputObject.id)) {
        throw '文件授权已失效。请先将草稿另存为新文件，或重新打开原配置。'
    }
    return $Grants[$InputObject.id]
}

function Write-Config([string]$Path, [string]$Expected, [string]$Content) {
    $bytes = $Utf8.GetBytes($Content)
    if ($bytes.Length -gt $FileLimit) { throw '配置文件超过 2 MB。' }
    if ($Content.TrimStart() -notmatch '^\{') { throw '配置必须是 JSON 对象。' }
    $null = ConvertFrom-Json -InputObject $Content
    $temporary = Join-Path ([IO.Path]::GetDirectoryName($Path)) ('.astra-' + [guid]::NewGuid().ToString('N') + '.tmp')
    try {
        [IO.File]::WriteAllBytes($temporary, $bytes)
        if ((Read-Snapshot $Path $false).version -cne $Expected) {
            throw '磁盘文件已经变更，已阻止覆盖。当前草稿可以另存为新文件。'
        }
        if ($Expected -ceq 'missing') { [IO.File]::Move($temporary, $Path) }
        else { [IO.File]::Replace($temporary, $Path, [NullString]::Value) }
        return @{version=(Hash-Bytes $bytes)}
    } finally {
        if ([IO.File]::Exists($temporary)) { [IO.File]::Delete($temporary) }
    }
}

function Send-Response($Response, [int]$Status, [string]$Type, [string]$Text) {
    $bytes = $Utf8.GetBytes($Text)
    $Response.StatusCode = $Status
    $Response.ContentType = $Type + '; charset=utf-8'
    $Response.Headers['Cache-Control'] = 'no-store'
    $Response.Headers['X-Content-Type-Options'] = 'nosniff'
    $Response.Headers['Referrer-Policy'] = 'no-referrer'
    $Response.Headers['Content-Security-Policy'] = "default-src 'self'; img-src 'self' data: blob:; style-src 'self' 'unsafe-inline'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'"
    $Response.ContentLength64 = $bytes.Length
    $Response.OutputStream.Write($bytes, 0, $bytes.Length)
}

function Invoke-Api([string]$Route, $Data) {
    switch ($Route) {
        '/api/pick' {
            if ($Data.action -notin @('open','save') -or $Data.editor -notin @('StarMapEditor','ZylPresentationEditor')) { throw '文件选择请求无效。' }
            $path = Select-EditorFile $Data.action $Data.editor $Data.name
            if (!$path) { return @{cancelled=$true} }
            $path = [IO.Path]::GetFullPath($path)
            if ([IO.Path]::GetExtension($path) -ine '.json') { throw '请选择 JSON 配置文件。' }
            $id = [guid]::NewGuid().ToString('N')
            $Grants[$id] = @{path=$path}
            return @{id=$id; path=$path; name=[IO.Path]::GetFileName($path); version=(Read-Snapshot $path $false).version}
        }
        '/api/read' {
            $grant = Get-Grant $Data
            return Read-Snapshot $grant.path $true
        }
        '/api/write' {
            $grant = Get-Grant $Data
            if ($Data.version -isnot [string] -or $Data.text -isnot [string]) { throw '缺少原文件版本或保存内容。' }
            return Write-Config $grant.path $Data.version $Data.text
        }
        default { throw '未知操作。' }
    }
}

function Open-EditorPage {
    $url = "$Origin/$Editor/index.html"
    $edge = Join-Path ${env:ProgramFiles(x86)} 'Microsoft\Edge\Application\msedge.exe'
    if (Test-Path -LiteralPath $edge -PathType Leaf) { Start-Process -FilePath $edge -ArgumentList $url }
    else { Start-Process $url }
}

function Start-EditorService {
    $listener = New-Object Net.HttpListener
    $listener.Prefixes.Add("$Origin/")
    try { $listener.Start() }
    catch {
        $existing = $null
        try { $existing = Invoke-RestMethod -Uri "$Origin/health" -TimeoutSec 3 } catch {}
        if ($existing.project -ceq $ToolMarker -and !$NoBrowser) { Open-EditorPage; return }
        throw "本机端口 $Port 无法启动。请关闭正在占用该端口的程序后重试。"
    }

    try {
        Write-Host "编辑器地址：$Origin/$Editor/index.html"
        Write-Host '编辑时请保留此窗口。关闭窗口将停止本地文件服务。'
        if (!$NoBrowser) { Open-EditorPage }
        while ($listener.IsListening) {
            $context = $listener.GetContext()
            $request = $context.Request
            $response = $context.Response
            try {
                if ($request.Headers['Host'] -cne "127.0.0.1:$Port") {
                    Send-Response $response 403 'text/plain' '无效的本机地址。'
                    continue
                }
                $route = $request.Url.AbsolutePath
                if ($request.HttpMethod -eq 'POST' -and $route.StartsWith('/api/')) {
                    if ($request.Headers['Origin'] -cne $Origin -or $request.Headers['X-Astra-Editor'] -cne '1') {
                        Send-Response $response 403 'text/plain' '请求来源无效。'
                        continue
                    }
                    if ($request.ContentLength64 -le 0 -or $request.ContentLength64 -gt $RequestLimit) { throw '请求大小无效。' }
                    $reader = [IO.StreamReader]::new($request.InputStream, $Utf8)
                    try { $data = ConvertFrom-Json -InputObject $reader.ReadToEnd() } finally { $reader.Dispose() }
                    $result = Invoke-Api $route $data
                    Send-Response $response 200 'application/json' (ConvertTo-Json -InputObject $result -Compress)
                } elseif ($request.HttpMethod -eq 'GET' -and $route -ceq '/health') {
                    Send-Response $response 200 'application/json' (ConvertTo-Json -InputObject @{project=$ToolMarker} -Compress)
                } elseif ($request.HttpMethod -eq 'GET' -and $route -ceq '/bridge.js') {
                    Send-Response $response 200 'text/javascript' ([IO.File]::ReadAllText((Join-Path $PSScriptRoot 'file-bridge.js')))
                } elseif ($request.HttpMethod -eq 'GET' -and $route -cmatch '^/(StarMapEditor|ZylPresentationEditor)/([a-z0-9.-]+)$' -and $Matches[2] -cin $StaticFiles) {
                    $file = Join-Path (Join-Path $ToolsRoot $Matches[1]) $Matches[2]
                    $content = [IO.File]::ReadAllText($file)
                    $type = switch ([IO.Path]::GetExtension($file)) { '.js' {'text/javascript'} '.css' {'text/css'} default {'text/html'} }
                    if ($type -eq 'text/html') {
                        $content = $content.Replace('<body>', '<script src="/bridge.js"></script><body>')
                    }
                    Send-Response $response 200 $type $content
                } else {
                    Send-Response $response 404 'text/plain' '未找到页面。'
                }
            } catch {
                try { Send-Response $response 400 'application/json' (ConvertTo-Json -InputObject @{error=$_.Exception.Message} -Compress) } catch {}
            } finally { $response.Close() }
        }
    } finally { $listener.Close() }
}

if (!$LoadFunctionsOnly) { Start-EditorService }
