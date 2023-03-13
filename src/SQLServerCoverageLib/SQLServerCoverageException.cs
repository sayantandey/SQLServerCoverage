using System;
using System.Runtime.Serialization;

namespace SQLServerCoverage
{
    [Serializable]
    public class SQLServerCoverageException : Exception
    {
        public SQLServerCoverageException()
        {
        }

        public SQLServerCoverageException(string message) : base(message)
        {
        }

        public SQLServerCoverageException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected SQLServerCoverageException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}