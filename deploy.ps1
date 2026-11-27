# Void Stunts - one-click deploy to a playable Safari link (Windows).
# Started by DEPLOY.bat. Safe to run again: it updates the same repository and rebuilds.
#
# What it does:
#   1. installs Git and the GitHub CLI if they are missing (via winget)
#   2. signs you in to GitHub in your browser
#   3. creates a public repository, stores your Unity login as encrypted repository secrets
#   4. turns on GitHub Pages, uploads the project, starts the cloud build
#   5. waits for the build and opens the link to play (plus a QR code for your iPhone)

$ErrorActionPreference = 'Continue'   # native tools report through exit codes; every step is checked
$ProgressPreference = 'SilentlyContinue'
Set-Location -LiteralPath $PSScriptRoot

function Say($t, $c = 'Gray') { Write-Host $t -ForegroundColor $c }
function Step($n, $t) { Write-Host ''; Write-Host ("[$n] " + $t) -ForegroundColor Yellow }
function Fail($t) { Write-Host ''; Write-Host ('STOPPED: ' + $t) -ForegroundColor Red; Write-Host ''; Read-Host 'Press Enter to close'; exit 1 }
function Ask($q, $def) { $a = Read-Host ($q + $(if ($def) { " [$def]" } else { '' })); if ([string]::IsNullOrWhiteSpace($a)) { return $def }; return $a.Trim() }
function YesNo($q) { $a = Read-Host ($q + ' (y/n)'); return $a -match '^(y|yes|j|ja)$' }
function RefreshPath { $env:Path = [Environment]::GetEnvironmentVariable('Path', 'Machine') + ';' + [Environment]::GetEnvironmentVariable('Path', 'User') }
function Have($cmd) { return [bool](Get-Command $cmd -ErrorAction SilentlyContinue) }
function SetSecret($name, $value) {
    # value goes through stdin (no trailing newline, no quoting problems with special characters)
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = (Get-Command gh).Source; $psi.Arguments = "secret set $name --repo $repo"
    $psi.UseShellExecute = $false; $psi.RedirectStandardInput = $true; $psi.CreateNoWindow = $true
    # raw UTF-8 bytes straight to the pipe: Windows PowerShell would otherwise re-encode non-ASCII letters (ae/oe/aa) and may add a BOM
    $p = [System.Diagnostics.Process]::Start($psi); $bytes = (New-Object System.Text.UTF8Encoding($false)).GetBytes($value)
    $p.StandardInput.BaseStream.Write($bytes, 0, $bytes.Length); $p.StandardInput.BaseStream.Flush(); $p.StandardInput.BaseStream.Close(); $p.WaitForExit()
    if ($p.ExitCode -ne 0) { Fail "Could not store the secret $name." }
}
function Run { param([string]$exe, [string[]]$a) & $exe @a; if ($LASTEXITCODE -ne 0) { Fail ("'$exe $($a -join ' ')' failed (exit $LASTEXITCODE).") } }

Clear-Host
Say '  VOID STUNTS  -  deploy to Safari' 'White'
Say '  ---------------------------------'
Say '  This takes about 5 minutes of your time, then about 45-60 minutes of cloud building.'
Say '  You can leave this window open and come back; it will open the game when it is ready.'

if (-not (Test-Path '.\Assets\Scripts\Core\Game.cs')) { Fail 'Run this from inside the VoidStunts folder (unzip the whole folder first).' }

# ------------------------------------------------------------------ 1. tools
Step 1 'Checking tools (Git, GitHub CLI)'
RefreshPath
$need = @()
if (-not (Have 'git')) { $need += @{ id = 'Git.Git'; name = 'Git' } }
if (-not (Have 'gh')) { $need += @{ id = 'GitHub.cli'; name = 'GitHub CLI' } }
if ($need.Count -gt 0) {
    if (-not (Have 'winget')) { Fail 'winget is missing. Install "App Installer" from the Microsoft Store, then run DEPLOY.bat again.' }
    foreach ($n in $need) {
        Say ("  installing " + $n.name + ' ...')
        winget install --id $n.id -e --source winget --accept-package-agreements --accept-source-agreements --silent | Out-Null
    }
    RefreshPath
    if (-not (Have 'git')) { $g = 'C:\Program Files\Git\cmd'; if (Test-Path $g) { $env:Path += ";$g" } }
    if (-not (Have 'gh')) { $g = 'C:\Program Files\GitHub CLI'; if (Test-Path $g) { $env:Path += ";$g" } }
    if (-not (Have 'git') -or -not (Have 'gh')) { Fail 'The tools installed but Windows has not picked them up yet. Close this window and double-click DEPLOY.bat again.' }
}
Say '  ok' 'Green'

# ------------------------------------------------------------------ 2. GitHub sign-in
Step 2 'GitHub account'
gh auth status 2>$null | Out-Null
$signedIn = ($LASTEXITCODE -eq 0)
if ($signedIn) {
    $scopes = (gh auth status 2>&1 | Out-String)
    if ($scopes -notmatch 'workflow') { Say '  adding permission to upload build workflows ...'; gh auth refresh -h github.com -s workflow }
} else {
    Say '  No GitHub account yet? Create one free at github.com first (the sign-in page has a "Create an account" link).'
    Say '  A browser window will open. Copy the one-time code shown here, paste it there, and approve.'
    Read-Host '  Press Enter to continue'
    gh auth login --hostname github.com --git-protocol https --web --scopes 'workflow'
    if ($LASTEXITCODE -ne 0) { Fail 'GitHub sign-in did not complete.' }
}
gh auth setup-git | Out-Null
$login = (gh api user --jq .login).Trim()
$uid = (gh api user --jq .id).Trim()
Say ("  signed in as $login") 'Green'

# ------------------------------------------------------------------ 3. repository
Step 3 'Repository'
$repoName = Ask '  Repository name' 'void-stunts'
$repo = "$login/$repoName"
gh repo view $repo --json name 2>$null | Out-Null
if ($LASTEXITCODE -eq 0) { Say "  using existing $repo" }
else {
    Say '  creating a PUBLIC repository (public = free unlimited cloud builds and free Pages hosting)'
    Run 'gh' @('repo', 'create', $repo, '--public', '--description', 'Void Stunts - jet car stunt game')
}

# ------------------------------------------------------------------ 4. Unity login -> secrets
Step 4 'Unity account (the cloud build uses a free Unity Personal licence)'
$secretList = (gh secret list --repo $repo 2>$null | Out-String)
$haveUnity = ($secretList -match 'UNITY_USERNAME') -and ($secretList -match 'UNITY_PASSWORD')
$setUnity = $true
if ($haveUnity) { $setUnity = YesNo '  Unity login is already stored. Replace it?' }
if ($setUnity) {
    Say '  You need a Unity ID with 2-step verification OFF (the cloud machine cannot type a code).'
    Say '  If you do not have one yet, create it now - it is free.'
    if (YesNo '  Open id.unity.com in your browser?') { Start-Process 'https://id.unity.com' ; Read-Host '  Press Enter when your account is ready' }
    $uEmail = Ask '  Unity email' ''
    if (-not $uEmail) { Fail 'No Unity email given.' }
    $sec = Read-Host '  Unity password (hidden)' -AsSecureString
    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($sec)
    $uPass = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr); [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
    if (-not $uPass) { Fail 'No Unity password given.' }
    $sec2 = Read-Host '  Type the password again (hidden)' -AsSecureString
    $b2 = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($sec2); $uPass2 = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($b2); [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($b2)
    if ($uPass -cne $uPass2) { Fail 'The two passwords did not match. Run DEPLOY.bat again.' }
    $uPass2 = $null
    if ($uPass -ne $uPass.Trim()) { Say '  Note: the password starts or ends with a space. That is kept exactly as typed.' 'DarkYellow' }
    Say ("  Email: " + $uEmail + "   Password: " + $uPass.Length + " characters") 'White'
    if (-not (YesNo '  Is that right? (You must be able to sign in at id.unity.com with exactly this email + password - not with Google/Apple)')) { Fail 'Cancelled. Run DEPLOY.bat again.' }
    SetSecret 'UNITY_USERNAME' $uEmail
    SetSecret 'UNITY_PASSWORD' $uPass
    $uPass = $null
    Say '  stored as encrypted GitHub secrets (never written to disk or uploaded with the code)' 'Green'
}

# ------------------------------------------------------------------ 5. Pages
Step 5 'GitHub Pages (free hosting for the browser build)'
gh api "repos/$repo/pages" 2>$null | Out-Null
if ($LASTEXITCODE -eq 0) { gh api -X PUT "repos/$repo/pages" -f build_type=workflow 2>$null | Out-Null }
else { gh api -X POST "repos/$repo/pages" -f build_type=workflow 2>$null | Out-Null }
gh api "repos/$repo/pages" 2>$null | Out-Null
if ($LASTEXITCODE -ne 0) { Fail "Could not turn on Pages. Open https://github.com/$repo/settings/pages and set Source to 'GitHub Actions', then run DEPLOY.bat again." }
Say '  ok' 'Green'

# ------------------------------------------------------------------ 6. upload
Step 6 'Uploading the project'
if (-not (Test-Path '.git')) { Run 'git' @('init', '-q', '-b', 'main') }
git config user.name 2>$null | Out-Null; if ($LASTEXITCODE -ne 0) { git config user.name $login }
git config user.email 2>$null | Out-Null; if ($LASTEXITCODE -ne 0) { git config user.email "$uid+$login@users.noreply.github.com" }
git config core.autocrlf false
$remoteUrl = "https://github.com/$repo.git"
git remote get-url origin 2>$null | Out-Null
if ($LASTEXITCODE -eq 0) { git remote set-url origin $remoteUrl } else { git remote add origin $remoteUrl }
Run 'git' @('add', '-A')
git diff --cached --quiet
$changed = ($LASTEXITCODE -ne 0)
if ($changed) { Run 'git' @('commit', '-q', '-m', ('Void Stunts deploy ' + (Get-Date -Format 'yyyy-MM-dd HH:mm'))) }
git fetch -q origin main 2>$null
if ($LASTEXITCODE -eq 0) { git branch --set-upstream-to=origin/main main 2>$null | Out-Null }
$startedAt = (Get-Date).ToUniversalTime()
git push -u origin main
if ($LASTEXITCODE -ne 0) {
    Say '  The repository on GitHub has a different history (for example, an older upload from another folder).'
    if (YesNo '  Replace what is on GitHub with this folder?') { git push -u --force origin main; if ($LASTEXITCODE -ne 0) { Fail 'Upload failed.' } }
    else { Fail 'Upload cancelled. Run again and choose a new repository name.' }
}
Say '  ok' 'Green'

# ------------------------------------------------------------------ 7. build
Step 7 'Cloud build'
$runId = $null
for ($i = 0; $i -lt 20 -and -not $runId; $i++) {
    Start-Sleep -Seconds 6
    $runs = (gh run list --repo $repo --workflow web-preview.yml --limit 1 --json databaseId,createdAt | Out-String) | ConvertFrom-Json
    if ($runs -and $runs.Count -gt 0 -and ([datetime]$runs[0].createdAt).ToUniversalTime() -ge $startedAt.AddSeconds(-30)) { $runId = $runs[0].databaseId }
    if (-not $runId -and $i -eq 3) { gh workflow run web-preview.yml --repo $repo --ref main 2>$null | Out-Null }
}
if (-not $runId) { Fail "The build did not start. Open https://github.com/$repo/actions and press 'Run workflow' on 'Web preview'." }
Say "  build started: https://github.com/$repo/actions/runs/$runId"
Say '  The first build installs Unity in the cloud and imports everything: about 45-60 minutes.'
Say '  Later builds reuse a cache and are faster. Progress below refreshes every 30 seconds.'
gh run watch $runId --repo $repo --interval 30 --exit-status
$ok = ($LASTEXITCODE -eq 0)

# ------------------------------------------------------------------ 8. result
$url = "https://$($login.ToLower()).github.io/$repoName/"
if ($ok) {
    Write-Host ''
    Say '  READY' 'Green'
    Say "  Play: $url" 'White'
    Say '  On your iPhone: open the link in Safari, tap Share, then "Add to Home Screen" for full-screen play.'
    Say '  (Browser version: no sound or vibration. The TestFlight app will have both.)'
    Set-Content -Path '.\PLAY-LINK.txt' -Value $url
    Start-Process $url
    $qr = 'https://api.qrserver.com/v1/create-qr-code/?size=360x360&data=' + [uri]::EscapeDataString($url)
    if (YesNo '  Show a QR code to scan with your iPhone camera?') { Start-Process $qr }
} else {
    gh run view $runId --repo $repo --log-failed > '.\build-error-log.txt' 2>$null
    Write-Host ''
    Say '  The cloud build failed.' 'Red'
    Say '  The error log was saved as build-error-log.txt in this folder.'
    Say '  Send that file to Claude in the chat; once fixed, just double-click DEPLOY.bat again.'
    Say '  (If the log mentions licence activation: check 2-step verification is OFF on your Unity account,'
    Say '   log in once at unity.com, then run DEPLOY.bat again and choose to replace the Unity login.)'
    Start-Process 'notepad.exe' '.\build-error-log.txt'
}
Write-Host ''
Read-Host 'Press Enter to close'
