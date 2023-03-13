# SQLServerCoverage 

##### Code coverage for SQL Server T-SQL (From [SQLCover](https://github.com/GoEddie/SQLCover)).

**SQLServerCoverage** is a tool for checking code coverage (both line and branch) of tests executed in SQL sever 2008 and above.

> This project is based on [SQLCover](https://github.com/GoEddie/SQLCover) with additional features, bug fix and maintenances planned ahead.

## Functionalities/Fixes added:

* Branch Coverage
* CLI tools for different platforms 
* Detailed documentation for setup

### [Html Report Sample [Using Report Generator]](https://raw.githack.com/sayantandey/SQLServerCoverage/main/example/Test%20Example/index.html)

____

# Index

 - [Download](#download)
 - [Build](#build)
 - [Installation](#installation)
 - [Usage](#usage)
   - [1. CLI](#1-cli)
   - [2. Cover T-SQL Script](#2-cover-t-sql-script)
   - [3. Cover anything else](#3-cover-anything-else)
   - [4. Redgate DLM Automation Suite](#4-redgate-dlm-automation-suite)
 - [Tidying up](#tidying-up)

## Download

Download the latest release from the release package. 



**Note:** If you are unable to find a release compatible for your system, consider building it from the codebase using dotnet tool . 

Read the [build](#build ) section for building the tool:.

## Build 

##### Follow these steps to build the tool for your environment 

* From the project root directory

  ```
  dotnet publish src/SQLServerCoverageCore/SQLServerCoverageCore.csproj  -c Release  -r <RUNTIME_IDENTIFIER> -o "releases/<RUNTIME_IDENTIFIER>" --self-contained true  -p:PublishSingleFile=true
  ```

  > For `RUNTIME_IDENTIFIER` put the os version for your system. 
  >
  > [Check this source to choose a runtime](https://learn.microsoft.com/en-us/dotnet/core/rid-catalog) 

* Once finished, fetch the binary from `/releases/<RUNTIME_IDENTIFIER>` directory.


## Installation

1. Put the tool in a directory of your preference.
2. Use the path to that directory as your environment variable 

___

## Usage

### 1. CLI

```bash
SQLServerCoverageCore

  -v, --verbose             Set output to verbose messages.

  -c, --command             Required. Choose command to run from:Get-CoverTSql, Get-CoverExe, Get-CoverRedgateCITest.

  -e, --exportCommand       Required. Choose command to run from:Export-OpenXml, Start-ReportGenerator, Export-Html.

  -b, --debug               Prints out more output.

  -p, --requiredParams      Get required parameters for a command

  -k, --connectionString    Connection String to the sql server

  -d, --databaseName        Default Database

  -q, --query               Sql Query, try tSQLt.runAll

  -o, --outputPath          Output Path

  -a, --args                Arguments for an exe file

  -t, --exeName             executable name

  --help                    Display this help screen.

  --version                 Display version information.
```

##### Example:

1. Generate the coverage report as xml

   ```
   SQLServerCoverageCore -v true -c Get-CoverTSql -e Export-OpenXml -d <DATABASE_NAME> -q <Query> -o <OUTPUT_PATH> -k <CONNECTION_STRING>
   ```

2. Use [ReportGenerator](https://github.com/danielpalme/ReportGenerator) to generate report from xml

   ```
   cd <OUTPUT_PATH>
   reportgenerator  "-reports:Coverage.opencover.xml" "-reporttypes:HtmlInline" "-targetdir:."
   ```

   



### 2. Cover T-SQL Script

If you have a script you want to cover then you can call:

```
Get-CoverTSql  "SQLServerCoverage-path.dll" "server=servername;integrated security=sspi;"  "database-name" "exec tSQLt.RunAll
```

This will give you a CoverageResults where you can either examine the amount of
statement covered or output the full html or xml report.


### 3. Cover anything else

If you want to have more control over what is covered, you can start a coverage session, run whatever queries you like from whatever application and then stop the coverage trace and get the CoverageResults which you can then use to generate a report.

```
$coverage = new-object SQLServerCoverage.CodeCoverage($connectionString, $database)
$coverage.Start()
#DO SOMETHING HERE
$coverageResults = $coverage.Stop()
```

### 4. Redgate DLM Automation Suite 

If you have the DLM automation suite then create a nuget package of your database, deploy the project to a test database and then use the example powershell script (https://github.com/GoEddie/SQLCover/blob/master/example/SQLCover.ps1 and included in the download above):

```
Get-CoverRedgateCITest "SQLServerCoverage-path.dll" "server=servername;integrated security=sspi;" "nuget-package-path.nupkg" "servername" "database-name"
```

To create the nupkg of your database you can use sqlci.exe or create a zip of your .sql files

 see: https://www.simple-talk.com/blogs/2014/12/18/using\_sql\_release\_with\_powershell/

The Get-CoverRedgateCITest will return an array with two objects in, the first object is a:

```
RedGate.SQLRelease.Compare.SchemaTesting.TestResults
```

The second object is a:

```
SQLServerCoverage.CoverageResult
```

This has two public properties:

```
public long StatementCount;
public long CoveredStatementCount;
```

It also has two public methods:

```
public string Html()
```

This creates a basic html report to view the code coverage, highlighting the lines of code in the database which have been covered and:

```
public string OpenCoverXml()
```

which creates an xml file in the OpenCoverageXml format which can be converted
into a very pretty looking report using reportgenerator: https://github.com/danielpalme/ReportGenerator

For a complete example see:

```
$results = Get-CoverRedgateCITest "path\to\SQLServerCoverage.dll" "server=.;integrated security=sspi;initial catalog=tSQLt_Example" "tSQLt_Example"
    Export-DlmDatabaseTestResults $results[0] -OutputFile c:\temp\junit.xml -force
    Export-OpenXml $results[1] "c:\output\path\for\xml\results"
    Start-ReportGenerator "c:\output\path\for\xml\results" "c:\path\to\reportgenerator.exe"
```



___

 ## Tidying up

 When we target local sql instances we delete the trace files but when targetting remote instances we are unable to delete the files as we do not (or potentially) do not have access. If this is the case keep an eye on the log directory and remove old "SQLServerCoverage-Trace-*.xel" and "SQLServerCoverage-Trace-*.xem" files. 
