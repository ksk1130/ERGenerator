rem Windows用ビルド（net10.0版：ランタイム含む, net4.8版：ランタイムなし）
echo Building net10.0 version with self-contained runtime...
dotnet publish -c Release -f net10.0 -r win-x64 --self-contained=true -p:PublishSingleFile=true -o bin\Release\publish\net10.0

echo Building net4.8 version without runtime (framework-dependent)...
dotnet publish -c Release -f net48 -o bin\Release\publish\net48

echo Done!