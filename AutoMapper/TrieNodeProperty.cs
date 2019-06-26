using System.Collections.Generic;
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
        private TrieNodeProperty Parent { get; set; }

        //Current Property Info
        public PropertyInfo Property { get; private set; }

        /// <summary>
        /// Current Properties instance
        /// </summary>
        public object Instance { get; set; }

        public bool PreserveExisting { get; set; }

        /// <summary>
        /// Children of the property
        /// </summary>
        public List<TrieNodeProperty> Children { get; private set; }

        public TrieNodeProperty(TrieNodeProperty parentProperty,
                PropertyInfo currentProperty)
        {
            Parent = parentProperty;
            Property = currentProperty;
            if (parentProperty != null)
                parentProperty.AddChildren(this);
        }

        public TrieNodeProperty(TrieNodeProperty parentProperty,
                PropertyInfo currentProperty, object instance)
        {
            Parent = parentProperty;
            Property = currentProperty;
            if (parentProperty != null)
                parentProperty.AddChildren(this);
            Instance = instance;
        }

        public TrieNodeProperty GetTopMostRoot()
        {
            TrieNodeProperty topMostNode = this;
            while(!topMostNode.IsTopMostRoot())
            {
                topMostNode = topMostNode.Parent;
            }
            return topMostNode;
        }

        public bool HasChildren()
        {
            if (Children == null || Children.Count == 0)
                return false;
            return true;
        }

        public void AddChildren(TrieNodeProperty child)
        {
            if (Children == null)
                Children = new List<TrieNodeProperty>();
            Children.Add(child);
        }

        /// <summary>
        /// Return the Parent of the property
        /// </summary>
        /// <returns></returns>
        public TrieNodeProperty GetParent()
        {
            return this.Parent;
        }

        /// <summary>
        /// Return if this is top most root
        /// </summary>
        /// <returns></returns>
        public bool IsTopMostRoot()
        {
            if (Parent == null)
                return true;
            return false;
        }

        /// <summary>
        /// Return if the Nodes have a parent or not
        /// </summary>
        /// <returns></returns>
        public bool HasParent()
        {
            if (Parent == null)
                return false;
            return true;
        }


    }
}
