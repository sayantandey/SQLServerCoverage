using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using SQLServerCoverage.Gateway;
using SQLServerCoverage.Source;
using SQLServerCoverage.Trace;

namespace SQLServerCoverage
{
    public class CodeCoverage
    {
        private const int MAX_DISPATCH_LATENCY = 1000;
        private readonly DatabaseGateway _database;
        private readonly string _databaseName;
        private readonly bool _debugger;
        private readonly TraceControllerType _traceType;
        private readonly List<string> _excludeFilter;
        private readonly bool _logging;
        private readonly SourceGateway _source;
        private CoverageResult _result;

        public const short TIMEOUT_EXPIRED = -2; //From TdsEnums
        public SQLServerCoverageException Exception { get; private set; } = null;
        public bool IsStarted { get; private set; } = false;

        private TraceController _trace;

        //This is to better support powershell and optional parameters
        public CodeCoverage(string connectionString, string databaseName) : this(connectionString, databaseName, null, false, false, TraceControllerType.Default)
        {
        }

        public CodeCoverage(string connectionString, string databaseName, string[] excludeFilter) : this(connectionString, databaseName, excludeFilter, false, false, TraceControllerType.Default)
        {
        }

        public CodeCoverage(string connectionString, string databaseName, string[] excludeFilter, bool logging) : this(connectionString, databaseName, excludeFilter, logging, false, TraceControllerType.Default)
        {
        }

        public CodeCoverage(string connectionString, string databaseName, string[] excludeFilter, bool logging, bool debugger) : this(connectionString, databaseName, excludeFilter, logging, debugger, TraceControllerType.Default)
        {
        }

        public CodeCoverage(string connectionString, string databaseName, string[] excludeFilter, bool logging, bool debugger, TraceControllerType traceType)
        {
            if (debugger)
                Debugger.Launch();

            _databaseName = databaseName;
            if (excludeFilter == null)
                excludeFilter = new string[0];

            _excludeFilter = excludeFilter.ToList();
            _logging = logging;
            _debugger = debugger;
            _traceType = traceType;
            _database = new DatabaseGateway(connectionString, databaseName);
            _source = new DatabaseSourceGateway(_database);
        }

        public bool Start(int timeOut = 30)
        {
            Exception = null;
            try
            {
                _database.TimeOut = timeOut;
                _trace = new TraceControllerBuilder().GetTraceController(_database, _databaseName, _traceType);
                _trace.Start();
                IsStarted = true;
                return true;
            }
            catch (Exception ex)
            {
                Debug("Error starting trace: {0}", ex);
                Exception = new SQLServerCoverageException("SQL Cover failed to start.", ex);
                IsStarted = false;
                return false;
            }
        }

        private List<string> StopInternal()
        {
            var events = _trace.ReadTrace();
            _trace.Stop();
            _trace.Drop();

            return events;
        }

        public CoverageResult Stop()
        {
            if (!IsStarted)
                throw new SQLServerCoverageException("SQL Cover was not started, or did not start correctly.");

            IsStarted = false;

            WaitForTraceMaxLatency();

            var results = StopInternal();

            GenerateResults(_excludeFilter, results, new List<string>(), "SQLServerCoverage result of running external process");

            return _result;
        }

        private void Debug(string message, params object[] args)
        {
            if (_logging)
                Console.WriteLine(message, args);
        }

        public CoverageResult Cover(string command, int timeOut = 30)
        {

            Debug("Starting Code Coverage");

            _database.TimeOut = timeOut;

            if (!Start())
            {
                throw new SQLServerCoverageException("Unable to start the trace - errors are recorded in the debug output");

            }

            Debug("Executing Command: {0}", command);

            var sqlExceptions = new List<string>();

            try
            {
                _database.Execute(command, timeOut, true);
            }
            catch (Microsoft.Data.SqlClient.SqlException e)
            {
                if (e.Number == -2)
                {
                    throw;
                }

                sqlExceptions.Add(e.Message);
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception running command: {0} - error: {1}", command, e.Message);
            }

            Debug("Executing Command: {0}...done", command);
            WaitForTraceMaxLatency();
            Debug("Stopping Code Coverage");
            try
            {
                var rawEvents = StopInternal();

                Debug("Getting Code Coverage Result");
                GenerateResults(_excludeFilter, rawEvents, sqlExceptions, $"SQLServerCoverage result of running '{command}'");
                Debug("Result generated");
            }
            catch (Exception e)
            {
                Console.Write(e.StackTrace);
                throw new SQLServerCoverageException("Exception gathering the results", e);
            }

            return _result;
        }


        private static void WaitForTraceMaxLatency()
        {
            Thread.Sleep(MAX_DISPATCH_LATENCY);
        }

        private void GenerateResults(List<string> filter, List<string> xml, List<string> sqlExceptions, string commandDetail)
        {
            var batches = _source.GetBatches(filter);
            _result = new CoverageResult(batches, xml, _databaseName, _database.DataSource, sqlExceptions, commandDetail);
        }

        public CoverageResult Results()
        {
            return _result;
        }
    }
}