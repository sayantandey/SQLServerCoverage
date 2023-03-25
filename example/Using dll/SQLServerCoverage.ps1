function Get-CoverTSql{
    param(
            [string]$coverDllPath
            ,[string]$connectionString
            ,[string]$databaseName
            ,[string]$query
        )

    if(!(Test-Path $coverDllPath)){
        Write-Error "SQLServerCoverage.dll path was not found ($coverDllPath)"
        return
    }

    Unblock-File -Path $coverDllPath
    
    Add-Type -Path $coverDllPath
    
    $coverage = new-object SQLServerCoverage.CodeCoverage ($connectionString, $databaseName)
	$coverage.Cover($query)
  
}

function Get-CoverExe{
    param(
            [string]$coverDllPath
            ,[string]$connectionString
            ,[string]$databaseName
            ,[string]$exeName
            ,[string]$args
        )

    if(!(Test-Path $coverDllPath)){
        Write-Error "SQLServerCoverage.dll path was not found ($coverDllPath)"
        return
    }

    Unblock-File -Path $coverDllPath
    
    Add-Type -Path $coverDllPath
    
    $coverage = new-object SQLServerCoverage.CodeCoverage ($connectionString, $databaseName)
	$coverage.CoverExe($exeName, $args)
  
}

  
function Export-OpenXml{
    param(        
        [SQLServerCoverage.CoverageResult] $result
        ,[string]$outputPath
    )

    $xmlPath = Join-Path -Path $outputPath -ChildPath "Coverage.opencoverxml"
    $result.OpenCoverXml() | Out-File $xmlPath
    $result.SaveSourceFiles($outputPath)    
}

function Start-ReportGenerator{
    param(        
        [string]$outputPath
        ,[string]$reportGeneratorPath
    )
    
    $xmlPath = Join-Path -Path $outputPath -ChildPath "Coverage.opencoverxml"
    $sourcePath = $outputPath
    
    if(!(Test-Path $sourcePath)){
        Write-Error "Cannot find source path to convert into html report. Path = $sourcePath"
    }
        
    $outputPath = Join-Path $outputPath -ChildPath "out"
    New-Item -Type Directory -Force -Path $outputPath | out-Null
        
    $report = "-reports:$xmlPath"
    $targetDir = "-targetDir:$outputPath" 
    $args = $report, $targetDir
    Start-Process -FilePath $reportGeneratorPath -ArgumentList $args -WorkingDirectory $sourcePath -Wait
    Write-Verbose "Coverage Report Written to: $outputPath"
}

function Export-Html{
    param(        
        [SQLServerCoverage.CoverageResult] $result
        ,[string]$outputPath
    )

    $xmlPath = Join-Path -Path $outputPath -ChildPath "Coverage.html"
    $result.Html() | Out-File $xmlPath
    $result.SaveSourceFiles($outputPath)    
}

#EXAMPLE:
<#
    #Use the Redgate DLM suite to deploy a nuget package and run the tSQLt tests
    $results = Get-CoverRedgateCITest "path\to\SQLServerCoverage.dll" "server=.;integrated security=sspi;initial catalog=tSQLt_Example" "tSQLt_Example"
    Export-DlmDatabaseTestResults $results[0] -OutputFile c:\temp\junit.xml -force
    Export-OpenXml $results[1] "c:\output\path\for\xml\results"
    Start-ReportGenerator "c:\output\path\for\xml\results" "c:\path\to\reportgenerator.exe"
    
    #Cover whatever sql query you would like to run, this example runs tSQLt.RunAll
    $result = Get-CoverTSql "path\to\SQLServerCoverage.dll" "server=.;integrated security=sspi;initial catalog=tSQLt_Example" "tSQLt_Example" "tSQLt.RunAll"
    #Output the results as a basic html file
    Export-Html $result "path\to\write\Log"

    $result = Get-CoverTSql "path\to\SQLServerCoverage.dll" "server=.;integrated security=sspi;initial catalog=tSQLt_Example" "tSQLt_Example" "tSQLt.RunAll"
    Export-OpenXml $result "output\path\for\xml\results"
    Start-ReportGenerator "output\path\for\xml\results" "path\to\reportgenerator.exe"


#>

# Set-ExecutionPolicy RemoteSigned
# mkdir Log
# launch docker sql server
# docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=yourStrong(!)Password" -p 1433:1433 -d mcr.microsoft.com/mssql/server:2019-latest
# deploy DatabaseWithTests project to sql server
# call .\example\SQLServerCoverage.ps1 from powershell
#EXAMPLE2: uncomment
<#
    $result = Get-CoverTSql "src\SQLServerCoverage\SQLServerCoverage\bin\Debug\SQLServerCoverage.dll" "Server=localhost;Database=master;User ID=sa;Password=yourStrong(!)Password" "DatabaseWithTests" "tSQLt.RunAll"
    #Output the results as a basic html file
    Export-Html $result "Log"
#>



