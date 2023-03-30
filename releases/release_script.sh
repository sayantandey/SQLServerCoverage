platforms=("win10-x64" "ubuntu.20.04-x64" "linux-x64" "osx-x64")
for ver in ${platforms[@]}; do
  rm -rf $(dirname "$0")/$ver/SQLServerCoverage
  dotnet publish $(dirname "$0")/../src/SQLServerCoverageCore/SQLServerCoverageCore.csproj -c Release -r $ver -o $(dirname "$0")/$ver/SQLServerCoverage --sc true 
  echo ">>  Generated distribution for $ver"
done