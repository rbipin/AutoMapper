using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace AutoMapper
{
    [ExcludeFromCodeCoverage]
    /// <summary>
    /// Property Trie Node
    /// </summary>
    internal class TrieNodeProperty
    {
        //Parent Property
        private TrieNodeProperty ParentProperty { get; set; }

        //Current Property Info
        public PropertyInfo Property { get; private set; }

        public object PropertyInstance { get; set; }

        public TrieNodeProperty(TrieNodeProperty parentProperty,
                PropertyInfo currentProperty)
        {
            ParentProperty = parentProperty;
            Property = currentProperty;
        }

        /// <summary>
        /// Return the Parent of the property
        /// </summary>
        /// <returns></returns>
        public TrieNodeProperty GetParent()
        {
            return this.ParentProperty;
        }

        /// <summary>
        /// Return if this is top most root
        /// </summary>
        /// <returns></returns>
        public bool IsTopRoot()
        {
            if (ParentProperty == null && Property == null)
                return true;
            return false;
        }

        /// <summary>
        /// Return if the Nodes have a parent or not
        /// </summary>
        /// <returns></returns>
        public bool HasParent()
        {
            if (ParentProperty == null)
                return false;
            return true;
        }


    }
}
