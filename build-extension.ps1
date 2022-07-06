# Run msbuild, restoring missing nuget packages, ensuring that the build is clean, bulding for release, and disabling deploying to a non-existant
# experimental instance of Visual Studio (build fails otherwise)
msbuild .\BuildTimeLogger.sln -restore /t:Clean,Build -p:Configuration=Release /p:DeployExtension=false 


