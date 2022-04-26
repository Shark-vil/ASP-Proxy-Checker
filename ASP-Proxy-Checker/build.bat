::dotnet build  -r osx.10.12-x64 -p:PublishSingleFile=true --self-contained true


dotnet publish --configuration Release -r win-x64 /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true --self-contained false
dotnet publish --configuration Release -r linux-x64 /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true --self-contained false