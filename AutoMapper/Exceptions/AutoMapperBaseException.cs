using System;
using System.Diagnostics.CodeAnalysis;

namespace AutoMapper.Exceptions
{
    [ExcludeFromCodeCoverage]
    /// <summary>
    /// Base Exception Class
    /// </summary>
    public abstract class AutoMapperBaseException: Exception
    {
        /// <summary>
        /// Error Source
        /// </summary>
        protected internal string ErrorSource { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message">Error Message</param>
        /// <param name="errorSource">Source of the error</param>
        protected internal AutoMapperBaseException(string message, string errorSource):base(message)
        {
           ErrorSource = errorSource;
        }
    }
}
