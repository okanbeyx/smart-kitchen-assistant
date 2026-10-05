#requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('Configure', 'Up', 'Down', 'Status', 'Migrate', 'Seed', 'Verify', 'Reset')]
    [string] $Action,
    [string] $ConfirmDatabase
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$sqlDirectory = Join-Path $repoRoot 'infra/development/sqlserver'
$envFile = Join-Path $sqlDirectory '.env'
$apiProject = Join-Path $repoRoot 'backend/src/SmartKitchenAssistant.Api'
$toolProject = Join-Path $repoRoot 'backend/tools/SmartKitchenAssistant.DevDb'
$databaseName = 'SmartKitchenAssistantDevelopment'

function Stop-Development([string] $Message) {
    throw [System.ApplicationException]::new($Message)
}

function Invoke-CapturedProcess {
    param([string] $File, [string[]] $Arguments, [string] $InputText, [switch] $SafeToolOutput)
    $start = [System.Diagnostics.ProcessStartInfo]::new()
    $start.FileName = $File
    $start.WorkingDirectory = $repoRoot
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardInput = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $start
    try {
        [void] $process.Start()
        $outputTask = $process.StandardOutput.ReadToEndAsync()
        $errorTask = $process.StandardError.ReadToEndAsync()
        if ($InputText) { $process.StandardInput.Write($InputText) }
        $process.StandardInput.Close()
        $process.WaitForExit()
        $output = $outputTask.GetAwaiter().GetResult()
        $errors = $errorTask.GetAwaiter().GetResult()
        if ($process.ExitCode -ne 0) {
            if ($SafeToolOutput) { Write-Host ($output + $errors).Trim() }
            Stop-Development 'Command failed; raw configuration/Docker output is suppressed to protect local secrets.'
        }
        return $output
    }
    finally { $process.Dispose() }
}

function Assert-LocalDocker {
    if ($env:DOCKER_HOST) { Stop-Development 'Unset DOCKER_HOST; only a local Docker context is supported.' }
    $context = (Invoke-CapturedProcess docker @('context', 'show')).Trim()
    $endpointJson = Invoke-CapturedProcess docker @('context', 'inspect', $context, '--format', '{{json .Endpoints.docker.Host}}')
    $endpoint = $endpointJson | ConvertFrom-Json
    if ($endpoint -notmatch '^(npipe:////\./pipe/|unix:///)') {
        Stop-Development 'Up/Down require a local named-pipe or Unix-socket Docker endpoint.'
    }
}

function Assert-LocalPasswordFile {
    if (-not (Test-Path -LiteralPath $envFile -PathType Leaf)) { Stop-Development 'Local .env missing. Run Configure explicitly first.' }
    $content = [System.IO.File]::ReadAllText($envFile)
    if ($content -notmatch "\AMSSQL_SA_PASSWORD='[A-Za-z0-9!@#%^&*()_+=.,:;?/-]{16,128}'\r?\n?\z") {
        Stop-Development 'Local .env is not in the documented Configure format. It was not changed.'
    }
}

try {
    if ($env:DOTNET_ENVIRONMENT -cne 'Development' -or $env:ASPNETCORE_ENVIRONMENT -cne 'Development') {
        Stop-Development 'Set DOTNET_ENVIRONMENT and ASPNETCORE_ENVIRONMENT explicitly to Development in this shell.'
    }
    $overrideNames = @('ConnectionStrings__SmartKitchen', 'ConnectionStrings:SmartKitchen',
        'SQLCONNSTR_SmartKitchen', 'SQLAZURECONNSTR_SmartKitchen', 'CUSTOMCONNSTR_SmartKitchen', 'MSSQL_SA_PASSWORD')
    foreach ($name in [Environment]::GetEnvironmentVariables().Keys) {
        if ($name -in $overrideNames) { Stop-Development 'Remove the connection-string/password environment override from this shell.' }
    }
    if ($Action -eq 'Reset' -and $ConfirmDatabase -cne $databaseName) {
        Stop-Development 'Reset requires -ConfirmDatabase SmartKitchenAssistantDevelopment and deletes ALL data in that database.'
    }
    if ($Action -ne 'Reset' -and $PSBoundParameters.ContainsKey('ConfirmDatabase')) {
        Stop-Development 'ConfirmDatabase is accepted only for Reset.'
    }

    switch ($Action) {
        'Configure' {
            if (Test-Path -LiteralPath $envFile) {
                Stop-Development 'Local .env already exists. Configure does not overwrite credentials or rotate a persisted SQL password.'
            }
            $secretsJson = Invoke-CapturedProcess dotnet @('user-secrets', 'list', '--json', '--project', $apiProject)
            # Secret Manager encloses JSON output in //BEGIN / //END comments.
            $secrets = $secretsJson | ConvertFrom-Json -AsHashtable
            if ($secrets.ContainsKey('ConnectionStrings:SmartKitchen')) {
                Stop-Development 'SmartKitchen connection already exists in User Secrets. Review local configuration; Configure will not replace it.'
            }
            $securePassword = Read-Host 'New LOCAL SQL SA password (16-128 chars; upper/lower/digit/symbol; no quotes, dollar sign or backslash)' -AsSecureString
            $password = [System.Net.NetworkCredential]::new('', $securePassword).Password
            if ($password -cnotmatch '\A[A-Za-z0-9!@#%^&*()_+=.,:;?/-]{16,128}\z' -or
                $password -cnotmatch '[A-Z]' -or $password -cnotmatch '[a-z]' -or
                $password -notmatch '[0-9]' -or $password -notmatch '[^A-Za-z0-9]') {
                Stop-Development 'Password does not meet the documented local password rules. Nothing was configured.'
            }
            $connection = [System.Data.Common.DbConnectionStringBuilder]::new()
            $connection['Server'] = 'tcp:127.0.0.1,14330'
            $connection['Database'] = $databaseName
            $connection['User ID'] = 'sa'
            $connection['Password'] = $password
            $connection['Encrypt'] = 'True'
            $connection['TrustServerCertificate'] = 'True'
            $connection['Persist Security Info'] = 'False'
            $payload = @{ 'ConnectionStrings:SmartKitchen' = $connection.ConnectionString } | ConvertTo-Json -Compress
            [void] (Invoke-CapturedProcess dotnet @('user-secrets', 'set', '--project', $apiProject) ($payload + "`n"))
            # CreateNew prevents replacing another configuration created during the prompt.
            $stream = [System.IO.File]::Open($envFile, [System.IO.FileMode]::CreateNew, [System.IO.FileAccess]::Write)
            try {
                $bytes = [System.Text.UTF8Encoding]::new($false).GetBytes("MSSQL_SA_PASSWORD='$password'`n")
                $stream.Write($bytes, 0, $bytes.Length)
            }
            finally { $stream.Dispose() }
            $password = $null
            $payload = $null
            $connection.Clear()
            $securePassword.Dispose()
            Write-Host 'Local .env and API User Secrets configured. No Docker or SQL operation was performed.'
        }
        { $_ -in 'Up', 'Down' } {
            Assert-LocalDocker
            Assert-LocalPasswordFile
            $composeArgs = @('compose', '--project-name', 'smart-kitchen-assistant-development',
                '--env-file', $envFile, '--file', (Join-Path $sqlDirectory 'compose.yaml'))
            if ($Action -eq 'Up') {
                $composeArgs += @('up', '--detach', '--wait', '--wait-timeout', '180', 'sqlserver')
            }
            else { $composeArgs += 'down' }
            [void] (Invoke-CapturedProcess docker $composeArgs)
            Write-Host "$Action completed. The named SQL data volume is preserved."
        }
        default {
            $toolArgs = @('run', '--no-build', '--no-launch-profile', '--project', $toolProject, '--', $Action.ToLowerInvariant())
            if ($Action -eq 'Reset') {
                Write-Host 'Reset will delete ALL data, including unrelated developer data, from SmartKitchenAssistantDevelopment.'
                $toolArgs += $ConfirmDatabase
            }
            Write-Host (Invoke-CapturedProcess dotnet $toolArgs -SafeToolOutput).Trim()
        }
    }
}
catch {
    # Only controlled messages are printed; never render PowerShell's invocation/locals dump.
    $message = if ($_.Exception -is [System.ApplicationException]) {
        $_.Exception.Message
    }
    else { 'Development command failed. Check prerequisites and local configuration; secret-bearing details are suppressed.' }
    Write-Host $message -ForegroundColor Red
    exit 1
}
