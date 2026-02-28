rem Windows用（1つのEXEファイルが出力される）
dotnet publish -c Release -r win-x64 --self-contained=true -p:PublishSingleFile=true 