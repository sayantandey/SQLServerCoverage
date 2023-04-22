using CommandLine;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Primitives;
using System.IO;
using System.Linq;

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

            [Option('c', "command", Required = true, HelpText = "Choose command to run: Currently only Get-CoverTSql available")]
            public string Command { get; set; }

            [Option('e', "exportType", Required = true, HelpText = "Choose export options : Export-OpenXml, Export-Html, Export-Cobertura")]
            public string ExportType { get; set; }

            [Option('b', "debug", Required = false, HelpText = "Prints out detailed output.")]
            public bool Debug { get; set; }

            [Option('p', "requiredParams", Required = false, HelpText = "Get required parameters for a command")]
            public bool GetRequiredParameters { get; set; }

            [JsonIgnore]
            [Option('k', "connectionString", Required = false, HelpText = "Connection String to the SQL server")]
            public string ConnectionString { get; set; }

            [Option('d', "databaseName", Required = false, HelpText = "Default Database")]
            public string databaseName { get; set; }

            [Option('q', "query", Required = false, HelpText = "Sql Query, Ex. tSQLt.runAll OR your custom test executor")]
            public string Query { get; set; }

            [Option('o', "outputPath", Required = false, HelpText = "Output Path of The Export Result")]
            public string OutputPath { get; set; }

            [Option('t', "timeout", Required = false, HelpText = "Wait time in Seconds before terminating the attempt to execute test SQL command")]
            public int TimeOut { get; set; }

            [Option('i', "ignore", Required = false, HelpText = "Space separated list of database objects to ignore. Regex Accepted. Case sensitive depending on collation" +
                                                                 "Ex.\"sp_dummy_proc* sp_test_proc\"")]
            public string IgnoreObjects { get; set; }

        }
        private enum CommandType
        {
            GetCoverTSql,
            Unknown
        }

        private enum ExportType
        {
            ExportOpenXml,
            ExportHtml,
            ExportCobertura,
            Unknown
        }
        /// <summary>
        /// 
        /// run by `dotnet run -- -c Get-CoverTSql -r`
        /// `dotnet run -- -c Get-CoverTSql -e Export-OpenXml -k "Data Source=localhost;Initial Catalog=master;User ID=sa;Password=yourStrong(!)Password" -d DatabaseWithTests -q "tSQLt.runAll" -p TestCoverageOpenXml`
        /// </summary>
        /// <param name="args">Arguments required for SQLServerCoverage to execute</param>
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
                       var eType = ExportType.Unknown;
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
                           default:
                               Console.Error.WriteLine("Command:" + o.Command + " is not supported");
                               break;
                       }

                       switch (o.ExportType)
                       {
                           case "Export-OpenXml":
                               eType = ExportType.ExportOpenXml;
                               requiredExportParameters = new string[]{
                                "outputPath"};
                               break;
                           case "Export-Html":
                               eType = ExportType.ExportHtml;
                               requiredExportParameters = new string[]{
                                "outputPath"};
                               break;
                           case "Export-Cobertura":
                               eType = ExportType.ExportCobertura;
                               requiredExportParameters = new string[]{
                                "outputPath"};
                               break;
                           default:
                               // coverage result will be serialized as json for future reference
                               requiredExportParameters = new string[]{
                                "outputPath"};
                               break;
                       }

                       bool isValid = (cType != CommandType.Unknown)
                                       && validateRequired(o, requiredParameters)
                                       && validateRequired(o, requiredExportParameters, isExportParams: true);

                       if (isValid)
                       {
                           if (o.GetRequiredParameters)
                           {
                               Console.WriteLine(o.Command + " requiredParameters are:" + string.Join(',', requiredParameters));
                               Console.WriteLine(o.ExportType + " requiredParameters are:" + string.Join(',', requiredExportParameters));
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
                                       coverage = new CodeCoverage(o.ConnectionString, o.databaseName, o.IgnoreObjects?.Split(), true, o.Debug);
                                       results = coverage.Cover(o.Query, Math.Max(o.TimeOut, 0));
                                       break;

                               }
                               if (coverage != null && results != null)
                               {
                                   Console.WriteLine(":::Exporting Result with " + eType.ToString() + ":::");
                                   var resultString = "";
                                   var outputPath = "";
                                   string openCoverXml = null;

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
                                   var openCoverFile = $"{outputPath}{DefaultCoverageFileName}.opencover.xml";
                                   switch (eType)
                                   {
                                       case ExportType.ExportOpenXml:
                                           openCoverXml = results.ToOpenCoverXml();
                                           results.SaveResult(openCoverFile, openCoverXml);
                                           results.SaveSourceFiles(outputPath);
                                           break;
                                       case ExportType.ExportHtml:
                                           openCoverXml = results.ToOpenCoverXml();
                                           results.SaveResult(openCoverFile, openCoverXml);
                                           results.SaveSourceFiles(outputPath);

                                           results.ToHtml(outputPath, outputPath, openCoverFile);
                                           break;
                                       case ExportType.ExportCobertura:
                                           openCoverXml = results.ToOpenCoverXml();
                                           results.SaveResult(openCoverFile, openCoverXml);
                                           results.SaveSourceFiles(outputPath);

                                           results.ToCobertura(outputPath, outputPath, openCoverFile);
                                           break;
                                       default:
                                           Console.Error.WriteLine("Invalid export option provided. Saving Result as Json");
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
        /// <returns></returns>
        private static bool validateRequired(Options o, string[] requiredParameters, bool isExportParams = false)
        {
            var valid = true;
            var requiredString = isExportParams ? " is required for this exportCommand" : " is required for this command";
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
