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
using System.Reflection;
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
        public TimeSpan TotalTimeTaken { get; private set; }


        public List<string> SqlExceptions
        {
            get { return _sqlExceptions; }
        }

        public IEnumerable<Batch> Batches
        {
            get { return _batches; }
        }
        private readonly StatementChecker _statementChecker = new StatementChecker();

        public CoverageResult(IEnumerable<Batch> batches, List<string> xml, string database, string dataSource, List<string> sqlExceptions, string commandDetail,  TimeSpan totalTimeTaken)
        {
            _batches = batches;
            _sqlExceptions = sqlExceptions;
            _commandDetail = $"{commandDetail} at {DateTime.Now}";
            DatabaseName = database;
            DataSource = dataSource;
            var parser = new EventsParser(xml);
            TotalTimeTaken = totalTimeTaken;
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
                string fileName = Path.Combine(path, batch.FileName + ".sql");
                File.WriteAllText(fileName, batch.Text);
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

        public void GenerateNativeHtmlReport(string outputPath)
        {
            // Load the HTML template
            string templateContent;
            var assembly = Assembly.GetExecutingAssembly();
            using (var stream = assembly.GetManifestResourceStream("SQLServerCoverage.NativeHtmlReportTemplate.html"))
            using (var reader = new StreamReader(stream))
            {
                templateContent = reader.ReadToEnd();
            }


            // Prepare data for the template
            var reportData = new
            {
                TotalTimeTaken = Math.Round(this.TotalTimeTaken.TotalSeconds, 2),
                StatementCount = this.StatementCount,
                CoveredStatementCount = this.CoveredStatementCount,
                UncoveredStatementCount = this.StatementCount - this.CoveredStatementCount,
                StatementCoveragePercentage = (this.StatementCount == 0) ? 0 : Math.Round((double)this.CoveredStatementCount / this.StatementCount * 100, 2),
                BranchesCount = this.BranchesCount,
                CoveredBranchesCount = this.CoveredBranchesCount,
                UncoveredBranchesCount = this.BranchesCount - this.CoveredBranchesCount,
                BranchCoveragePercentage = (this.BranchesCount == 0) ? 0 : Math.Round((double)this.CoveredBranchesCount / this.BranchesCount * 100, 2),
                Batches = this.Batches.Select(batch => new
                {
                    ObjectName = batch.ObjectName,
                    StatementCount = batch.StatementCount,
                    CoveredStatementCount = batch.CoveredStatementCount,
                    StatementCoveragePercentage = (batch.StatementCount == 0) ? 0 : Math.Round((double)batch.CoveredStatementCount / batch.StatementCount * 100, 2),
                    BranchesCount = batch.BranchesCount,
                    CoveredBranchesCount = batch.CoveredBranchesCount,
                    BranchCoveragePercentage = (batch.BranchesCount == 0) ? 0 : Math.Round((double)batch.CoveredBranchesCount / batch.BranchesCount * 100, 2),
                    IsTested = batch.CoveredStatementCount > 0,
                    CodeFile = batch.FileName + ".sql",
                    LineCoverage = GetLineCoverage(batch),
                    BranchCoverageDetails = batch.Statements.SelectMany(s => s.Branches.Select(b => new
                    {
                        LineNumber = GetOffsets(b.Offset, b.Length, batch.Text).StartLine,
                        Description = b.Text.Trim(),
                        HitCount = b.HitCount
                    })).ToList()
                }).ToList()
            };

            // Convert the report data to JSON for injection into the template
            string reportDataJson = JsonConvert.SerializeObject(reportData);

            // Inject the data into the template
            string reportContent = templateContent.Replace("{{ReportDataJson}}", reportDataJson);
            reportContent = reportContent.Replace("{{StatementCount}}", reportData.StatementCount.ToString());
            reportContent = reportContent.Replace("{{CoveredStatementCount}}", reportData.CoveredStatementCount.ToString());
            reportContent = reportContent.Replace("{{StatementCoveragePercentage}}", reportData.StatementCoveragePercentage.ToString());
            reportContent = reportContent.Replace("{{BranchesCount}}", reportData.BranchesCount.ToString());
            reportContent = reportContent.Replace("{{CoveredBranchesCount}}", reportData.CoveredBranchesCount.ToString());
            reportContent = reportContent.Replace("{{BranchCoveragePercentage}}", reportData.BranchCoveragePercentage.ToString());
            reportContent = reportContent.Replace("{{TotalTimeTaken}}", reportData.TotalTimeTaken.ToString());
            reportContent = reportContent.Replace("{{UncoveredStatementCount}}", (reportData.StatementCount - reportData.CoveredStatementCount).ToString());
            reportContent = reportContent.Replace("{{UncoveredBranchesCount}}", (reportData.BranchesCount - reportData.CoveredBranchesCount).ToString());

            // Save the report
            string reportPath = Path.Combine(outputPath, "CoverageReport.html");
            File.WriteAllText(reportPath, reportContent);
            Console.WriteLine("Generating HTML report in "+ reportPath);

            // Save code files and generate individual procedure pages
            foreach (var batch in this.Batches)
            {
                string codeFileName = batch.FileName + ".sql";
                string codeFilePath = Path.Combine(outputPath, codeFileName);
                File.WriteAllText(codeFilePath, batch.Text);

                // Generate individual procedure page
                GenerateProcedurePage(batch, outputPath, codeFileName);
            }
        }

    private Dictionary<int, string> GetLineCoverage(Batch batch)
    {
        var lineCoverage = new Dictionary<int, string>(); // LineNumber -> CoverageClass

        foreach (var statement in batch.Statements)
        {
            var offsets = GetOffsets(statement, batch.Text);
            for (int line = offsets.StartLine; line <= offsets.EndLine; line++)
            {
                // Initialize coverage class as not covered
                if (!lineCoverage.ContainsKey(line))
                {
                    lineCoverage[line] = "not-covered";
                }

                if (statement.HitCount > 0)
                {
                    lineCoverage[line] = "covered";
                }
            }

            // Handle branches
            foreach (var branch in statement.Branches)
            {
                var branchOffsets = GetOffsets(branch.Offset, branch.Length, batch.Text);
                for (int line = branchOffsets.StartLine; line <= branchOffsets.EndLine; line++)
                {
                    if (!lineCoverage.ContainsKey(line))
                    {
                        lineCoverage[line] = "not-covered";
                    }

                    if (branch.HitCount > 0)
                    {
                        if (lineCoverage[line] == "not-covered")
                        {
                            lineCoverage[line] = "partial";
                        }
                    }
                    else
                    {
                        if (lineCoverage[line] == "covered")
                        {
                            lineCoverage[line] = "partial";
                        }
                    }
                }
            }
        }

        return lineCoverage;
    }


    private void GenerateProcedurePage(Batch batch, string outputPath, string codeFileName)
    {
        string templateContent;
        var assembly = Assembly.GetExecutingAssembly();
        using (var stream = assembly.GetManifestResourceStream("SQLServerCoverage.sp_template.html"))
        using (var reader = new StreamReader(stream))
        {
            templateContent = reader.ReadToEnd();
        }


        // Read the code content
        string codeContent = batch.Text;

        // Prepare data for the template
        var lineCoverage = GetLineCoverage(batch);
        var lineDetails = GetLineDetails(batch); // Function to get line details from OpenCover data

        var procedureData = new
        {
            ProcedureName = batch.ObjectName,
            CodeContent = codeContent,
            CoveredStatementCount = batch.CoveredStatementCount,
            UncoveredStatementCount = batch.StatementCount - batch.CoveredStatementCount,
            CoveredBranchesCount = batch.CoveredBranchesCount,
            UncoveredBranchesCount = batch.BranchesCount - batch.CoveredBranchesCount,
            LineCoverageJson = JsonConvert.SerializeObject(lineCoverage),
            LineDetailsJson = JsonConvert.SerializeObject(lineDetails)
        };

        // Serialize the code content into a JSON string
        string codeContentJson = JsonConvert.SerializeObject(procedureData.CodeContent);

        // Inject data into template
        string pageContent = templateContent
            .Replace("{{ProcedureName}}", procedureData.ProcedureName)
            .Replace("{{CodeContentJson}}", codeContentJson)
            .Replace("{{CoveredStatementCount}}", procedureData.CoveredStatementCount.ToString())
            .Replace("{{UncoveredStatementCount}}", procedureData.UncoveredStatementCount.ToString())
            .Replace("{{CoveredBranchesCount}}", procedureData.CoveredBranchesCount.ToString())
            .Replace("{{UncoveredBranchesCount}}", procedureData.UncoveredBranchesCount.ToString())
            .Replace("{{LineCoverageJson}}", procedureData.LineCoverageJson)
            .Replace("{{LineDetailsJson}}", procedureData.LineDetailsJson);


        // Save the procedure page
        string sanitizedFileName = GetSafeFilename(batch.ObjectName);
        string procedurePagePath = Path.Combine(outputPath, sanitizedFileName + ".html");
        File.WriteAllText(procedurePagePath, pageContent);
    }

    private string GetSafeFilename(string filename)
    {
        return string.Join("_", filename.Split(Path.GetInvalidFileNameChars()));
    }

    private Dictionary<int, string> GetLineDetails(Batch batch)
    {
        var lineDetails = new Dictionary<int, string>();

        foreach (var statement in batch.Statements)
        {
            var offsets = GetOffsets(statement, batch.Text);
            for (int line = offsets.StartLine; line <= offsets.EndLine; line++)
            {
                string detail = $"Statement Hits: {statement.HitCount}";
                if (lineDetails.ContainsKey(line))
                {
                    lineDetails[line] += "\n" + detail;
                }
                else
                {
                    lineDetails[line] = detail;
                }
            }

            foreach (var branch in statement.Branches)
            {
                var branchOffsets = GetOffsets(branch.Offset, branch.Length, batch.Text);
                for (int line = branchOffsets.StartLine; line <= branchOffsets.EndLine; line++)
                {
                    string detail = $"Branch Hits: {branch.HitCount}";
                    if (lineDetails.ContainsKey(line))
                    {
                        lineDetails[line] += "\n" + detail;
                    }
                    else
                    {
                        lineDetails[line] = detail;
                    }
                }
            }
        }

        return lineDetails;
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
