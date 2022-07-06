# escape=`

# Use the latest Windows Server Core image with .NET Framework 4.8.
FROM mcr.microsoft.com/dotnet/framework/sdk:4.8-windowsservercore-ltsc2019

# Restore the default Windows shell for correct batch processing.
SHELL ["cmd", "/S", "/C"]

RUN `
    # Download the Build Tools bootstrapper.
    curl -SL --output vs_buildtools.exe https://aka.ms/vs/16/release/vs_buildtools.exe `
    `
    # Install Build Tools with the Microsoft.VisualStudio.Workload.VisualStudioExtensionBuildTools workload, excluding workloads and components with known issues.
    && (start /w vs_buildtools.exe --quiet --wait --norestart --nocache modify `
    --installPath "%ProgramFiles(x86)%\Microsoft Visual Studio\2019\BuildTools" `
    --add Microsoft.VisualStudio.Workload.VisualStudioExtensionBuildTools `
    --remove Microsoft.VisualStudio.Component.Windows10SDK.10240 `
    --remove Microsoft.VisualStudio.Component.Windows10SDK.10586 `
    --remove Microsoft.VisualStudio.Component.Windows10SDK.14393 `
    --remove Microsoft.VisualStudio.Component.Windows81SDK `
    || IF "%ERRORLEVEL%"=="3010" EXIT 0) `
    `
    # Cleanup
    && del /q vs_buildtools.exe

# Switch to powershell to install chocolatey
SHELL ["powershell", "-command"]

# Install chocolatey (may be preferable to host the chocolatey install script on our own git instance?)
RUN Set-ExecutionPolicy Bypass -Scope Process -Force; `
    [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol `
    -bor 3072; iex ((New-Object System.Net.WebClient).DownloadString('https://chocolatey.org/install.ps1'))

# Use chocolatey to install git
RUN choco install git.install -y

# Add the microsoft nuget repository so we can install dependencies for Visual Studio projects
RUN Install-PackageProvider -Name Nuget -Confirm:$false -Force -ErrorAction SilentlyContinue

# Define the entry point for the docker container.
# This entry point starts the developer command prompt and launches the PowerShell shell.
ENTRYPOINT ["C:\\Program Files (x86)\\Microsoft Visual Studio\\2019\\BuildTools\\Common7\\Tools\\VsDevCmd.bat", "&&", "powershell.exe", "-NoLogo", "-ExecutionPolicy", "Bypass"]

