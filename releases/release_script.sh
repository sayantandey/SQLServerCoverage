platforms=("win10-x64" "ubuntu.20.04-x64" "linux-x64" "osx-x64")
for ver in ${platforms[@]}; do
  dotnet publish ../src/SQLServerCoverageCore/SQLServerCoverageCore.csproj -c Release -r $ver -o $ver/SQLServerCoverage --sc true 
  echo ">>  Generated distribution for $ver"
done