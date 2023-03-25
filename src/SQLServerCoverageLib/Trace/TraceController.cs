using System;
using System.Collections.Generic;
using SQLServerCoverage.Gateway;

namespace SQLServerCoverage.Trace
{
    abstract class TraceController
    {
        protected readonly string DatabaseId;
        protected readonly DatabaseGateway Gateway;
        protected string FileName;
        
        protected readonly string Name;
        
        public TraceController(DatabaseGateway gateway, string databaseName)
        {
            Gateway = gateway;
            DatabaseId = gateway.GetString(string.Format("select db_id('{0}')", databaseName));
            Name = string.Format($"SQLServerCoverage-Trace-{Guid.NewGuid().ToString()}");
        }

        public abstract void Start();
        public abstract void Stop();
        public abstract List<string> ReadTrace();
        public abstract void Drop();


        protected void RunScript(string query, string error, int timeout = 60)
        {
            var script = GetScript(query);
            try
            {
                Gateway.Execute(script, timeout);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(string.Format(error, ex.Message), ex);
            }
        }

        protected string GetScript(string query)
        {
            if (query.Contains("{2}"))
            {
                return string.Format(query, Name, DatabaseId, FileName + ".xel");
            }

            if (query.Contains("{1}"))
            {
                return string.Format(query, Name, DatabaseId);
            }

            return string.Format(query, Name);
        }
    }
}