using System.Diagnostics.CodeAnalysis;
namespace AutoMapper.Exceptions
{
    [ExcludeFromCodeCoverage]
    /// <summary>
    /// Mapping Conflict Exception
    /// </summary>
    public class MappingConflict: AutoMapperBaseException
    {
        /// <summary>
        /// Mapping Conflict Exception
        /// </summary>
        /// <param name="message">Error Message</param>
        /// <param name="errorSource">Source of the error</param>
        public MappingConflict(string message, string errorSource) : base(message, errorSource) { }
    }
}
