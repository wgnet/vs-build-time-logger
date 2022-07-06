
##################################################
# Variables
##################################################
# Script input parameters
param (
    [string]$Target,
    [string]$SettingsPath,
    [string]$ExtensionUrl,
    [string]$ExtensionPath
)
# Path of log file
$Logfile = "$env:APPDATA\VSBuildLoggerTemp\install.log"

# Store the script name for use in help menu
$ScriptName = $MyInvocation.MyCommand.Name

# Amount of time to wait before killing visual studio when applying settings
$SecondsToSleep = 20

# If downloading an extension - what name to save it as
$DownloadedExtensionName = "extension.vsix"

##################################################
# Helper Functions
##################################################
function CreateLogFile() {
    # Create log file if it doesn't exist
    if (!(Test-Path $Logfile))
    {
        New-Item -path $Logfile -type "file" -Force
    }
}

function InstallNuget
{
    try {
        if (Get-PackageProvider -Name Nuget) {
            Write-Host "Nuget Package Provider already installed"
        }
        else {
            LogWrite "Attempting to install Nuget Package Provider"
            Install-PackageProvider -Name Nuget -Scope CurrentUser -Confirm:$false -Force

            if (!$?) {
                throw $error[0].Exception
            }

            LogWrite "Nuget Package Provider installed successfully"
        }
    } 
    catch {
        $ErrorMsg = "ERROR: Could not install Nuget module."
        Write-Host 
        Write-Host $ErrorMsg -ForegroundColor Red
        Write-Host $_.Exception.Message -ForegroundColor Red

        LogWrite $ErrorMsg
        LogWrite "ERROR: Exception Message: $_"
    }

}

function LogWrite
{
   Param ([string]$logstring)

   $Stamp = (Get-Date).toString("yyyy/MM/dd HH:mm:ss")
   $Line = "[$Stamp] $logstring"

   Add-content $Logfile -value $Line
}

function Write-ScriptUsage {
    Write-Host "----------------------------------Script Usage-----------------------------------"
    Write-Host
    Write-Host "$ScriptName -Target [target] (Optional)-ExtensionUrl/-ExtensionPath [url/path] (Optional)-SettingsPath [path-to-vssettings-file] "
    Write-Host
    Write-Host "If an ExtensionUrl or ExtensionPath is supplied, attempts to install the extension"
    Write-Host
    Write-Host "If a settings path is provided, attempts to import the settings after any extension installation steps have been performed"
    Write-Host
    Write-Host "Valid target values are:"
    Write-Host "    all - installs for all versions of Visual Studio installed 2017+"
    Write-Host "    latest - only installs for the latest compatible version of Visual Studio installed"
    Write-Host
    Write-Host "If no ExtensionUrl/ExtensionPath paramataer or SettingsPath paramater is supplied, the script does nothing"
    Write-Host
}

# Process paramaters supplied to the script
function CheckParameters() {
    $ValidParameters = $true

    # Check if target is set
    if ([string]::IsNullOrEmpty($Target)) {
        Write-Host
        Write-Host "ERROR: Target must be provided" -ForegroundColor Red
        $ValidParameters = $false
    }
    # If it's not empty, make sure it's a valid value
    elseif (-not ($Target -eq 'all' -Or $Target -eq 'latest')) {
        Write-Host "ERROR: Invalid value for Target" -ForegroundColor Red
        $ValidParameters = $false
    }

    # If both Extention URL and Path are set be mad
    if ((-not [string]::IsNullOrEmpty($ExtensionUrl)) -And (-not [string]::IsNullOrEmpty($ExtensionPath))) {
        Write-Host "ERROR: Please only specify either an  Extention URL or an Extension Path - not both."
        $ValidParameters = $false
    }

    # Exit if our parameters were bad
    if (-not $ValidParameters) {
        Write-Host
        Write-ScriptUsage
        exit
    }
}

# Scans for Visual Studio installations and returns an array with the paths based on supplied target
function GetVisualStudioPaths($target) {
    try {
        # Checks if the Visual Studio PowerShell module is installed, and if it isn't, installs it
        # Module found here: https://github.com/microsoft/vssetup.powershell
        # This allows us to query installed Visual Studio versions
        if (-not (Get-Module -ListAvailable -Name VSSetup)) {
            Write-Host "Attempting to install VSSetup PowerShell Module for querying VS Install information..." -ForegroundColor Magenta
            Install-Module VSSetup -Scope CurrentUser -Force
        }
    }
    catch {
        $ErrorMsg = "ERROR: Could not install VSSetup module. Please make sure you have internet connectivity"
        Write-Host 
        Write-Host $ErrorMsg -ForegroundColor Red
        
        LogWrite $ErrorMsg
        LogWrite $Error[0]
        exit
    }

    if ($target -eq 'all') {
        return Get-VSSetupInstance | Select-VSSetupInstance -Version '[15.0,17.0)'
    }
    elseif ($target -eq 'latest') {
        return Get-VSSetupInstance | Select-VSSetupInstance -Version '[16.0,17.0)'
    }

    return @()
}

# Attempts to download an extension from the supplied url
function DownloadExtension($url) {
    try {
        Invoke-WebRequest -Uri $url -OutFile $DownloadedExtensionName
    }
    catch {
        $ErrorMsg = "ERROR: Could not download extension from provided extension url"
        Write-Host
        Write-Host $ErrorMsg  -ForegroundColor Red
        Write-Host $Error[0] -ForegroundColor Red
        
        LogWrite $ErrorMsg
        LogWrite $Error[0]
        exit
    }
}

function InstallExtension($vsInstall, $extensionFile) {
    # Make sure we have valid VSIXInstallerPath
    $VSIXInstallerExePath = $vsInstall.InstallationPath + '\Common7\IDE\VSIXInstaller.exe'
    if (-not (Test-Path $VSIXInstallerExePath)) {
        throw "Could not find VSIXInstaller.exe at: $VSIXInstallerExePath - is it installed?"
    }

    try {
        Write-Host
        Write-Host "Installing $extensionFile for" $vsInstall.DisplayName -ForegroundColor Cyan
        Write-Host "    Installing..."
        $InstallCommand = "/quiet $extensionFile"
        $Process = Start-Process -FilePath $VSIXInstallerExePath -ArgumentList $InstallCommand -PassThru -WindowStyle hidden
        $Process.WaitForExit()
        Write-Host "    Done!"
    }
    catch {
        $ErrorMsg = "ERROR: Failed to install $extensionFile"
        Write-Host
        Write-Host $ErrorMsg -ForegroundColor Red
        Write-Host $Error[0] -ForegroundColor Red

        LogWrite $ErrorMsg
        LogWrite $Error[0]
        exit
    }

}

function ModifySettingsVersion($vsInstall, $settingsFile) {
    # Get the major version from the vs install information
    $majVersion = $vsInstall.InstallationVersion.Major
    $version = "$majVersion.0"

    # Write the major version to the settings xml file
    $file = Get-Content $settingsFile
    $xml=[XML]$File
    $nodes = $xml.SelectNodes("/UserSettings/ApplicationIdentity");
    foreach($node in $nodes) {
        $node.SetAttribute("version", $version);
    }
    $xml.OuterXml | Out-File $settingsFile
}

function ApplySettings($vsInstall, $settingsFile) {
    # Make sure the settings file is targetting the correct version of visual studio
    ModifySettingsVersion $vsInstall $settingsFile

    $DevEnvExePath = $vsInstall.InstallationPath + '\Common7\IDE\devenv.exe'
    # Make sure we have valid devenv.exe path
    if (-not (Test-Path $DevEnvExePath)) {
        throw "Could not find devenv.exe at: $DevEnvExePath - is it installed?"
    }    
    try {
        Write-Host
        Write-Host "Applying $settingsFile for" $vsInstall.DisplayName  -ForegroundColor Cyan
        Write-Host "    Installing..."
        $ImportCommand = "/Command `"Tools.ImportandExportSettings /import:$settingsFile`""
        $Process = Start-Process -FilePath $DevEnvExePath -ArgumentList $ImportCommand -Passthru -WindowStyle hidden
        Start-Sleep -Seconds $SecondsToSleep # hack: couldnt find a way to exit when done
        $Process.Kill()

        Write-Host "    Done!"
    }
    catch {
        $ErrorMsg = "ERROR: Failed to apply $settingsFile"
        Write-Host
        Write-Host $ErrorMsg -ForegroundColor Red
        Write-Host $Error[0] -ForegroundColor Red

        LogWrite $ErrorMsg
        LogWrite $Error[0]
        exit
    }
}

 
##################################################
# Script Start
##################################################
# Make sure the log file exists
CreateLogFile

# Ensure the user has Nuget installed
InstallNuget

# Check supplied script parameters are valid
CheckParameters
 
# Figure out whether we need to install or download an extension
$InstallExtension = $false
$ExtenstionFile
if (-not [string]::IsNullOrEmpty($ExtensionUrl)) {
    DownloadExtension $ExtensionUrl
    $ExtenstionFile = $DownloadedExtensionName 
    $InstallExtension = $true
}
elseif (-not [string]::IsNullOrEmpty($ExtensionPath)) {
    if (Test-Path $ExtensionPath) {
        $ExtenstionFile = $ExtensionPath
        $InstallExtension = $true
    }
    else {
        $ErrorMsg = "ERROR: Could not find $ExtensionPath"
        Write-Host
        Write-Host $ErrorMsg -ForegroundColor Red

        LogWrite $ErrorMsg
        LogWrite $Error[0]
        exit
    }
}

# Figure out if we need to apply an optional settings file
$ApplySettings = $false
if (-not [string]::IsNullOrEmpty($SettingsPath)) {

    if (-not (Test-Path $SettingsPath)) {
        Write-Host
        Write-Host "ERROR: Could not find supplied settings file: $SettingsPath" -ForegroundColor Red
        exit
    }
   
    $ApplySettings = $true
}


if ($ApplySettings -Or $InstallExtension) {
    # Get an array of the visual studio paths to configure the extension for
    $VisualStudioPaths = GetVisualStudioPaths($Target)

    # Exit if we found no installations
    if ($VisualStudioPaths.Count -eq 0) {
        Write-Host
        Write-Host "ERROR: No Visual Studio Installations found!" -ForegroundColor Red
        exit
    }

    # Report Found Visual Studio installations
    Write-Host
    Write-Host "Visual Studio installations found:" -ForegroundColor Cyan
    $VisualStudioPaths | ForEach-Object { Write-Host "  " $_.DisplayName "at" $_.InstallationPath }

    # Attempt to perform the requested actions on each installation
    foreach ($vsInstall in $VisualStudioPaths) {
        if ($InstallExtension) {
            InstallExtension $vsInstall $ExtenstionFile
        }

        if ($ApplySettings) {
            ApplySettings $vsInstall $SettingsPath
        }
    }
}
else {
    Write-Host "No extension or settings file provided... doing nothing"
}
