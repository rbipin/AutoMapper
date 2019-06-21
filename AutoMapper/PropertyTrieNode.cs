using System.Reflection;
using System.Diagnostics.CodeAnalysis;

namespace AutoMapper
{
    [ExcludeFromCodeCoverage]
    /// <summary>
    /// Property Trie Node
    /// </summary>
    internal class PropertyTrieNode
    {
        //Parent Property
        private PropertyTrieNode ParentProperty { get; set; }

        //Current Property Info
        public PropertyInfo  Property{ get; private set; }

        public PropertyTrieNode(PropertyTrieNode parentProperty,PropertyInfo currentProperty)
        {
            ParentProperty = parentProperty;
            Property = currentProperty;
        }

        /// <summary>
        /// Return the Parent of the property
        /// </summary>
        /// <returns></returns>
        public PropertyTrieNode GetParent()
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
