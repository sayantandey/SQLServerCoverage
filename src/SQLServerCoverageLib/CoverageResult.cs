using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using SQLServerCoverage.Objects;
using SQLServerCoverage.Parsers;
using SQLServerCoverage.Serializers;
using Newtonsoft.Json;
using Palmmedia.ReportGenerator.Core;
using ReportGenerator;

namespace SQLServerCoverage
{
    public class CoverageResult : CoverageSummary
    {
        private readonly IEnumerable<Batch> _batches;
        private readonly List<string> _sqlExceptions;
        private readonly string _commandDetail;

        public string DatabaseName { get; }
        public string DataSource { get; }

        public List<string> SqlExceptions
        {
            get { return _sqlExceptions; }
        }

        public IEnumerable<Batch> Batches
        {
            get { return _batches; }
        }
        private readonly StatementChecker _statementChecker = new StatementChecker();

        public CoverageResult(IEnumerable<Batch> batches, List<string> xml, string database, string dataSource, List<string> sqlExceptions, string commandDetail)
        {
            _batches = batches;
            _sqlExceptions = sqlExceptions;
            _commandDetail = $"{commandDetail} at {DateTime.Now}";
            DatabaseName = database;
            DataSource = dataSource;
            var parser = new EventsParser(xml);

            var statement = parser.GetNextStatement();

            while (statement != null)
            {
                var batch = _batches.FirstOrDefault(p => p.ObjectId == statement.ObjectId);
                if (batch != null)
                {
                    var item = batch.Statements.FirstOrDefault(p => _statementChecker.Overlaps(p, statement));
                    if (item != null)
                    {
                        item.HitCount++;
                    }
                }

                statement = parser.GetNextStatement();
            }

            foreach (var batch in _batches)
            {
                foreach (var item in batch.Statements)
                {
                    foreach (var branch in item.Branches)
                    {
                        var branchStatement = batch.Statements
                            .Where(x => _statementChecker.Overlaps(x, branch.Offset, branch.Offset + branch.Length))
                            .FirstOrDefault();

                        branch.HitCount = branchStatement.HitCount;
                    }
                }

                batch.CoveredStatementCount = batch.Statements.Count(p => p.HitCount > 0);
                batch.CoveredBranchesCount = batch.Statements.SelectMany(p => p.Branches).Count(p => p.HitCount > 0);
                batch.HitCount = batch.Statements.Sum(p => p.HitCount);
            }

            CoveredStatementCount = _batches.Sum(p => p.CoveredStatementCount);
            CoveredBranchesCount = _batches.Sum(p => p.CoveredBranchesCount);
            BranchesCount = _batches.Sum(p => p.BranchesCount);
            StatementCount = _batches.Sum(p => p.StatementCount);
            HitCount = _batches.Sum(p => p.HitCount);


        }

        public void SaveResult(string path, string resultString)
        {
            File.WriteAllText(path, resultString);
        }

        public void SaveSourceFiles(string path)
        {
            foreach (var batch in _batches)
            {
                File.WriteAllText(Path.Combine(path, batch.ObjectName), batch.Text);
            }
        }
        private static string Unquote(string quotedStr) => quotedStr.Replace("'", "\"");

        public string ToOpenCoverXml()
        {
            return new OpenCoverXmlSerializer().Serialize(this);
        }

        public string ToJson()
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(this, Formatting.Indented);
        }

        /// <summary>
        /// Use ReportGenerator Tool to Convert Open XML To Inline HTML
        /// </summary>
        /// <param name="targetDirectory">diretory where report will be generated</param>
        /// <param name="sourceDirectory">source directory</param>
        /// <param name="openCoverFile">source path of open cover report</param>
        /// <returns></returns>
        public void ToHtml(string targetDirectory, string sourceDirectory, string openCoverFile)
        {
            var reportType = "HtmlInline";
            generateReport(targetDirectory, sourceDirectory, openCoverFile, reportType);
        }

        /// <summary>
        /// Use ReportGenerator Tool to Convert Open XML To Cobertura
        /// </summary>
        /// <param name="targetDirectory">diretory where report will be generated</param>
        /// <param name="sourceDirectory">source directory</param>
        /// <param name="openCoverFile">source path of open cover report</param>
        /// <returns></returns>
        public void ToCobertura(string targetDirectory, string sourceDirectory, string openCoverFile)
        {
            var reportType = "Cobertura";
            generateReport(targetDirectory, sourceDirectory, openCoverFile, reportType);
        }


        /// <summary>
        /// Use ReportGenerator Tool to Convert Open Cover XML To Required Report Type
        /// </summary>
        /// TODO : Use Enum for Report Type
        /// <param name="targetDirectory">diretory where report will be generated</param>
        /// <param name="sourceDirectory">source directory</param>
        /// <param name="openCoverFile">source path of open cover report</param>
        /// <param name="reportType">type of report to be generated</param>
        public void generateReport(string targetDirectory, string sourceDirectory, string openCoverFile, string reportType)
        {
            var reportGenerator = new Generator();
            var reportConfiguration = new ReportConfigurationBuilder().Create(new Dictionary<string, string>()
                                            {
                                                { "REPORTS", openCoverFile },
                                                { "TARGETDIR", targetDirectory },
                                                { "SOURCEDIRS", sourceDirectory },
                                                { "REPORTTYPES", reportType},
                                                { "VERBOSITY", "Info" },

                                            });
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"Report from : '{Path.GetFullPath(openCoverFile)}' \n will be used to generate {reportType} report in: {Path.GetFullPath(targetDirectory)} directory");
            Console.ResetColor();
            var isGenerated = reportGenerator.GenerateReport(reportConfiguration);
            if (!isGenerated)
                Console.WriteLine($"Error Generating {reportType} Report.Check logs for more details");
        }

        public static OpenCoverOffsets GetOffsets(Statement statement, string text)
            => GetOffsets(statement.Offset, statement.Length, text);

        public static OpenCoverOffsets GetOffsets(int offset, int length, string text, int lineStart = 1)
        {
            var offsets = new OpenCoverOffsets();

            var column = 1;
            var line = lineStart;
            var index = 0;

            while (index < text.Length)
            {
                switch (text[index])
                {
                    case '\n':
                        line++;
                        column = 0;
                        break;
                    default:

                        if (index == offset)
                        {
                            offsets.StartLine = line;
                            offsets.StartColumn = column;
                        }

                        if (index == offset + length)
                        {
                            offsets.EndLine = line;
                            offsets.EndColumn = column;
                            return offsets;
                        }
                        column++;
                        break;
                }

                index++;
            }

            return offsets;
        }


        public string NCoverXml()
        {
            return "";
        }
    }

    public struct OpenCoverOffsets
    {
        public int StartLine;
        public int EndLine;
        public int StartColumn;
        public int EndColumn;
    }

    public class CustomCoverageUpdateParameter
    {
        public Batch Batch { get; internal set; }
        public int LineCorrection { get; set; } = 0;
        public int OffsetCorrection { get; set; } = 0;
    }
}
