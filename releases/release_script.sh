platforms=("win-x64" "win-x86" "linux-x64" "osx-x64")
for ver in ${platforms[@]}; do
  dotnet publish ../src/SQLServerCoverageCore/SQLServerCoverageCore.csproj -c Release -r $ver -o $ver --sc true -p:PublishSingleFile=true
  echo "Genertaed distribution for $ver"
done