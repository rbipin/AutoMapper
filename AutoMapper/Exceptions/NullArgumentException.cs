using System.Diagnostics.CodeAnalysis;

namespace AutoMapper.Exceptions
{
    [ExcludeFromCodeCoverage]
    public class NullArgumentException : AutoMapperBaseException
    {
        /// <summary>
        /// Null Exception
        /// </summary>
        /// <param name="message">Error Message</param>
        /// <param name="errorSource">Source of the error</param>
        public NullArgumentException(string message, string errorSource) : base(message, errorSource) { }
       
    }
}
