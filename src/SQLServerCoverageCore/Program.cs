using CommandLine;
using Newtonsoft.Json;
using System;
using System.IO;

namespace SQLServerCoverage.Core
{
    class Program

    {

        // Default path of result storage
        public const string DefaultLocation = "SQL Server Coverage Report";
        // Default path of result storage
        public const string DefaultCoverageFileName = "SQLServerCoverage";

        public class Options
        {
            [Option('v', "verbose", Required = false, HelpText = "Set output to verbose messages.")]
            public bool Verbose { get; set; }

            [Option('c', "command", Required = true, HelpText = "Choose command to run from: Get-CoverTSql, Get-CoverExe.")]
            public string Command { get; set; }
            [Option('e', "exportCommand", Required = true, HelpText = "Choose command to run from:Export-OpenXml, Export-Html")]
            public string ExportCommand { get; set; }
            [Option('b', "debug", Required = false, HelpText = "Prints out detailed output.")]
            public bool Debug { get; set; }
            [Option('p', "requiredParams", Required = false, HelpText = "Get required parameters for a command")]
            public bool GetRequiredParameters { get; set; }
            [Option('k', "connectionString", Required = false, HelpText = "Connection String to the sql server")]
            public string ConnectionString { get; set; }
            [Option('d', "databaseName", Required = false, HelpText = "Default Database")]
            public string databaseName { get; set; }
            [Option('q', "query", Required = false, HelpText = "Sql Query, Ex. tSQLt.runAll")]
            public string Query { get; set; }
            [Option('o', "outputPath", Required = false, HelpText = "Output Path")]
            public string OutputPath { get; set; }
            [Option('a', "args", Required = false, HelpText = "Arguments for an exe file")]
            public string Args { get; set; }
            [Option('t', "exeName", Required = false, HelpText = "executable name")]
            public string ExeName { get; set; }

        }
        private enum CommandType
        {
            GetCoverTSql,
            GetCoverExe,
            ExportOpenXml,
            ExportHtml,
            ExportCobertura,
            Unknown
        }
        /// <summary>
        /// should mimic arguments from example\SQLCover.ps1
        /// run by `dotnet run -- -c Get-CoverTSql -r`
        /// `dotnet run -- -c Get-CoverTSql -e Export-OpenXml -k "Data Source=localhost;Initial Catalog=master;User ID=sa;Password=yourStrong(!)Password" -d DatabaseWithTests -q "tSQLt.runAll" -p TestCoverageOpenXml`
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args)
                   .WithParsed<Options>(o =>
                   {
                       if (o.Verbose)
                       {
                           Console.WriteLine($"Verbose output enabled. Current Arguments: -v {o.Verbose}");
                           Console.WriteLine("Current Arguments Serialized::" + JsonConvert.SerializeObject(o));
                           Console.WriteLine("SQLServerCoverageCore App is in Verbose mode!");
                       }
                       else
                       {
                           Console.WriteLine($"Current Arguments: -v {o.Verbose} -c {o.Command}");
                           Console.WriteLine(":::Running SQLServerCoverageCore:::");
                       }
                       var cType = CommandType.Unknown;
                       var eType = CommandType.Unknown;
                       string[] requiredParameters = null;
                       string[] requiredExportParameters = null;
                       switch (o.Command)
                       {
                           case "Get-CoverTSql":
                               cType = CommandType.GetCoverTSql;
                               requiredParameters = new string[]{
                                "connectionString",
                                "databaseName",
                                "query"};
                               break;
                           case "Get-CoverExe":
                               cType = CommandType.GetCoverExe;
                               requiredParameters = new string[]{
                                "connectionString",
                                "databaseName",
                                "exeName",
                                "args"};
                               break;
                           default:
                               Console.Error.WriteLine("Command:" + o.Command + " is not supported");
                               break;
                       }

                       switch (o.ExportCommand)
                       {
                           case "Export-OpenXml":
                               eType = CommandType.ExportOpenXml;
                               requiredExportParameters = new string[]{
                                "outputPath"};
                               break;
                           case "Export-Html":
                               eType = CommandType.ExportHtml;
                               requiredExportParameters = new string[]{
                                "outputPath"};
                               break;
                           default:
                               // coverage result will be saved as json for future reference
                               requiredExportParameters = new string[]{
                                "outputPath"};
                               break;
                       }

                       var validParams = cType != CommandType.Unknown ? validateRequired(o, requiredParameters) : false;
                       validParams = validParams ? validateRequired(o, requiredExportParameters) : validParams;

                       if (validParams)
                       {
                           if (o.GetRequiredParameters)
                           {
                               Console.WriteLine(o.Command + " requiredParameters are:" + string.Join(',', requiredParameters));
                               Console.WriteLine(o.ExportCommand + " requiredParameters are:" + string.Join(',', requiredExportParameters));
                           }
                           else
                           {
                               Console.WriteLine(":::Running command" + cType.ToString() + ":::");
                               CodeCoverage coverage = null;
                               CoverageResult results = null;
                               // run command
                               switch (cType)
                               {
                                   case CommandType.GetCoverTSql:
                                       coverage = new CodeCoverage(o.ConnectionString, o.databaseName, null, true, o.Debug);
                                       results = coverage.Cover(o.Query);
                                       break;
                                   case CommandType.GetCoverExe:
                                       coverage = new CodeCoverage(o.ConnectionString, o.databaseName, null, true, o.Debug);
                                       results = coverage.CoverExe(o.ExeName, o.Args);
                                       break;
                               }
                               if (coverage != null && results != null)
                               {
                                   Console.WriteLine(":::Running exportCommand" + eType.ToString() + ":::");
                                   var resultString = "";
                                   var outputPath = "";
                                   if (!string.IsNullOrWhiteSpace(o.OutputPath))
                                   {
                                       outputPath = o.OutputPath;
                                       if (outputPath.Substring(outputPath.Length - 1, 1) != Path.DirectorySeparatorChar.ToString())
                                       {
                                           outputPath += Path.DirectorySeparatorChar;
                                       }
                                   }
                                   else
                                   {
                                       outputPath = DefaultLocation + Path.DirectorySeparatorChar + DateTime.Now.ToString("yyyyMMdd_HHmmss") + Path.DirectorySeparatorChar;
                                       if (!Directory.Exists(outputPath))
                                       {
                                           Directory.CreateDirectory(outputPath);
                                       }
                                   }
                                   switch (eType)
                                   {
                                       case CommandType.ExportOpenXml:
                                           resultString = results.ToOpenCoverXml();
                                           results.SaveResult(outputPath + $"{DefaultCoverageFileName}.opencover.xml", resultString);
                                           results.SaveSourceFiles(outputPath);
                                           break;
                                       case CommandType.ExportHtml:
                                           var openCoverXml = results.ToOpenCoverXml();
                                           var openCoverFile = $"{outputPath}{DefaultCoverageFileName}.opencover.xml";
                                           results.SaveResult(openCoverFile, openCoverXml);
                                           results.SaveSourceFiles(outputPath);
                                           results.ToHtml(outputPath, outputPath, openCoverFile);
                                           break;
                                       case CommandType.ExportCobertura:
                                           // TODO Use Report Generator to generate from Opencover
                                           results.SaveResult(outputPath + $"{DefaultCoverageFileName}.cobertura.xml", resultString);
                                           results.SaveSourceFiles(outputPath);
                                           break;
                                       default:
                                           Console.Error.WriteLine("Invalid export command provided. Saving Result as Json");
                                           resultString = results.ToJson();
                                           results.SaveResult(outputPath + $"{DefaultCoverageFileName}.json", resultString);
                                           results.SaveSourceFiles(outputPath);
                                           break;
                                   }
                               }
                           }
                       }
                   });
        }

        /// <summary>
        /// Validate all the required arguments and print error message 
        /// </summary>
        /// <param name="o"> input arguments</param>
        /// <param name="requiredParameters">list of required parameters</param>
        /// <param name="export"></param>
        /// <returns></returns>
        private static bool validateRequired(Options o, string[] requiredParameters, bool export = false)
        {
            var valid = true;
            var requiredString = export ? " is required for this exportCommand" : " is required for this command";
            foreach (var param in requiredParameters)
            {
                switch (param)
                {
                    case "connectionString":
                        if (string.IsNullOrWhiteSpace(o.ConnectionString))
                        {
                            Console.Error.WriteLine("connectionString" + requiredString);
                            valid = false;
                        }
                        break;
                    case "databaseName":
                        if (string.IsNullOrWhiteSpace(o.databaseName))
                        {
                            Console.Error.WriteLine("databaseName" + requiredString);
                            valid = false;
                        }
                        break;
                    case "query":
                        if (string.IsNullOrWhiteSpace(o.Query))
                        {
                            Console.Error.WriteLine("query" + requiredString);
                            valid = false;
                        }
                        break;
                    case "outputPath":
                        if (!string.IsNullOrWhiteSpace(o.OutputPath))
                        {
                            if (!Directory.Exists(o.OutputPath))
                            {
                                Console.Error.WriteLine("outputPath:" + o.OutputPath + " is not a valid directory.");
                                return false;
                            }

                        }
                        else
                        {
                            o.OutputPath = string.Empty;
                        }
                        break;
                    case "args":
                        if (string.IsNullOrWhiteSpace(o.Args))
                        {
                            Console.Error.WriteLine("args" + requiredString);
                            valid = false;
                        }
                        break;

                    case "exeName":
                        if (string.IsNullOrWhiteSpace(o.ExeName))
                        {
                            Console.WriteLine("exeName" + requiredString);
                            valid = false;
                        }
                        break;

                    default:
                        Console.WriteLine("Required check on:" + param + " ignored");
                        // will always be invalid for commands not validated on a required param
                        valid = false;
                        break;
                }
            }
            return valid;
        }
    }
}
