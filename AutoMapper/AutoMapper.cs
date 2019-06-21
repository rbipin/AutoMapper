using AutoMapper.Exceptions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace AutoMapper
{
    public static class AutoMapper
    {
        /// <summary>
        /// Map the source type to the destination property, this method can scan three levels and map automatically if the type is unique
        /// This extention method will scan to only three levels down, this is to handle the conflicting property
        /// </summary>
        /// <param name="destination">Destination object on which the extension method is called</param>
        /// <param name="source">Source object that will be mapped</param>
        public static void Map(this object destination, object source)
        {

            if (destination == null)
                throw new NullException("The desination object (object to which the mapping has to happen) is null",
                    $"{GetCurrentMethodName()}");
            if (source == null)
                throw new NullException("The source object (object from which the mapping has to happen) is null",
                    $"{GetCurrentMethodName()}");

            //Property Mapping Index
            Dictionary<Type, Dictionary<string, PropertyTrieNode>> propertyMappingIndex
           = new Dictionary<Type, Dictionary<string, PropertyTrieNode>>();

            //Property Trie
            PropertyTrieNode rootPropertyMap = new PropertyTrieNode(null, null);

            //Type of the source
            var sourceType = source.GetType();
            //Create the object mapping tree
            LoadObjectDictionary(destination, propertyMappingIndex, rootPropertyMap);
            //Get the property nodes that matches the source type
            var propertyNodes = propertyMappingIndex[sourceType];
            //Return exception in case multiple properties are available for the same type
            if (propertyNodes.Count > 1)
                throw new MappingConflict($"Multiple Property of type {sourceType.Name}"
                            , $"{GetCurrentMethodName()}");

            var propertyNode = propertyNodes.Values.First();

            var property = propertyNode.Property;
            //Auto Initialize the parents
            AutoInitializeParents(destination, propertyNode);
            //Assign the values to the destination from the source
            AssignSourceDataToDestination(destination, property, source);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="destination">Destination object on which the extension method is called</param>
        /// <param name="source">Source object to map to the destination</param>
        /// <param name="propertyName">Name of the property to Map it to</param>
        public static void Map(this object destination, object source, string propertyName)
        {

            if (destination == null)
                throw new NullException("The desination object (object to which the mapping has to happen) is null",
                    $"{GetCurrentMethodName()}");
            if (source == null)
                throw new NullException("The source object (object from which the mapping has to happen) is null",
                    $"{GetCurrentMethodName()}");
            if (string.IsNullOrEmpty(propertyName))
                throw new NullException("Property is empty or null, please provide a vaild property name", $"{GetCurrentMethodName()}");

            //Object Mapping tree, hold the index of the properties and it's parents
            Dictionary<Type, Dictionary<string, PropertyTrieNode>> propertyMappingIndex
           = new Dictionary<Type, Dictionary<string, PropertyTrieNode>>();

            //Property Trie
            PropertyTrieNode rootPropertyMap = new PropertyTrieNode(null, null);
            PropertyTrieNode propertyNode = null;

            propertyName = propertyName.ToUpper();
            //Type of the source
            var sourceType = source.GetType();
            //Build the object mapping tree
            LoadObjectDictionary(destination, propertyMappingIndex, rootPropertyMap);
            var propertyNodes = propertyMappingIndex[sourceType];
            if (propertyNodes.Count > 1)
                propertyNode = propertyNodes[propertyName];
            else
                propertyNode = propertyNodes.Values.First();

            var property = propertyNode.Property;
            //If the parent property of the current is not initialized, then initalize it
            AutoInitializeParents(destination, propertyNode);
            //Assign the source to the destination
            AssignSourceDataToDestination(destination, property, source);

        }

        /// <summary>
        /// Auto Initlaize the Parents
        /// </summary>
        /// <param name="destination"></param>
        /// <param name="propertyNode"></param>
        private static void AutoInitializeParents(object destination,
            PropertyTrieNode propertyNode)
        {
            Stack<PropertyInfo> stackOrderToInitialize = new Stack<PropertyInfo>();
            //Add the properties to the Stack
            while (!propertyNode.IsTopRoot())
            {
                stackOrderToInitialize.Push(propertyNode.Property);
                propertyNode = propertyNode.GetParent();
            }
            //Iterate the stack and initalize the property
            while (stackOrderToInitialize.Count > 0)
            {
                var property = stackOrderToInitialize.Pop();
                //you have reached the last item in the stack, which is the original property, 
                //no need to initialize this as we are going to map the source directly
                if (stackOrderToInitialize.Count == 0)
                    break;

                if (IsClass(property))
                {
                    var propertyValue = property.GetValue(destination, null);
                    if (propertyValue == null)
                    {
                        var newTypeInstance = Activator.CreateInstance(property.PropertyType);
                        property.SetValue(destination, newTypeInstance);
                    }
                }
            }

        }

        /// <summary>
        /// Get the current method name for the exceptions
        /// </summary>
        /// <returns></returns>
        private static string GetCurrentMethodName()
        {
            StackTrace stackTrace = new StackTrace();
            StackFrame stackFrame = stackTrace.GetFrame(1);
            MethodBase methodBase = stackFrame.GetMethod();
            return methodBase.Name;
        }

        /// <summary>
        /// Map the Object dictionary
        /// </summary>
        /// <param name="destination">Destination object</param>
        /// <param name="propertyMapping">Property mapping dictionary</param>
        private static void LoadObjectDictionary(object destination,
          Dictionary<Type, Dictionary<string, PropertyTrieNode>> propertyMapping,
          PropertyTrieNode rootPropertyMap
            )
        {
            var publicProperties = destination.GetType()
                  .GetProperties(BindingFlags.Public | BindingFlags.Instance).ToList();
            publicProperties.ForEach(property =>
            {
                GetProperties(property,
                              property.Name,
                              propertyMapping,
                              rootPropertyMap,
                              1);
            });

        }

        /// <summary>
        /// Assign Source to the destination object
        /// </summary>
        /// <param name="destination">Destination object</param>
        /// <param name="property">Property</param>
        /// <param name="source"></param>
        private static void AssignSourceDataToDestination(object destination,
            PropertyInfo property,
            object source)
        {
            object valueToSetToDestination = null;
            if (property.PropertyType.IsGenericType)
            {
                valueToSetToDestination = CopyOverList(property.GetValue(destination), source);
                property.SetValue(destination, valueToSetToDestination);
                return;
            }
            if (property.PropertyType.IsArray)
            {
                valueToSetToDestination = CopyOverArray(property.GetValue(destination), source);
                property.SetValue(destination, valueToSetToDestination);
                return;
            }
            property.SetValue(destination, source);

        }

        /// <summary>
        /// Copy over a list, if the destination list already has data, will be combine them.
        /// </summary>
        /// <param name="destinationData">Destination</param>
        /// <param name="SourceData">Source</param>
        /// <returns></returns>
        private static object CopyOverList(object destinationData, object SourceData)
        {
            var destinationList = destinationData as IList;

            var sourceList = SourceData as IList;
            if (destinationList != null && destinationList.Count > 0)
            {
                var elementType = destinationData.GetType().GetGenericArguments()[0];
                var listType = typeof(List<>).MakeGenericType(elementType);
                var cloneData = (IList)Activator.CreateInstance(listType);

                foreach (var existingItem in destinationList)
                {
                    cloneData.Add(existingItem);
                }
                foreach (var newItem in sourceList)
                {
                    cloneData.Add(newItem);
                }
                return cloneData;
            }
            else
            {
                return sourceList;
            }

        }

        /// <summary>
        /// Copy over an Array, if the array already has items, add them to the array
        /// </summary>
        /// <param name="destinationData">Destination</param>
        /// <param name="SourceData">Source</param>
        /// <returns></returns>
        private static object CopyOverArray(object destinationData, object SourceData)
        {
            ArrayList tempArrayList = new ArrayList();
            var destinationArray = destinationData as Array;
            var sourceArray = SourceData as Array;
            var destArrayCount = destinationArray.Length;
            if (destinationArray != null && destArrayCount > 0)
            {
                tempArrayList.AddRange(destinationArray);
                tempArrayList.AddRange(sourceArray);
                return tempArrayList.ToArray();
            }
            else
            {
                return sourceArray;
            }
        }


        /// <summary>
        /// Get the properties of object map and index them.
        /// </summary>
        /// <param name="property">Property</param>
        /// <param name="parentPropertyName">Parent Property Name</param>
        /// <param name="propertyMappingIndex">Index of the object mapping</param>
        /// <param name="propertyMap">Property Node</param>
        /// <param name="propertyDepth">Current Depth of the scan</param>
        private static void GetProperties(PropertyInfo property,
            string parentPropertyName,
            Dictionary<Type, Dictionary<string, PropertyTrieNode>> propertyMappingIndex,
            PropertyTrieNode propertyMap,
            int propertyDepth)
        {
            string propertyName = string.Empty;

            if (propertyDepth > 3)
                return;
            
            if (propertyDepth == 1)
                propertyName = parentPropertyName.ToUpper();
            
            else
                  propertyName = $"{parentPropertyName.ToUpper()}.{property.Name.ToUpper()}";
            
            //Create the object TRIE
            propertyMap = new PropertyTrieNode(propertyMap, property);

            if (propertyMappingIndex.ContainsKey(property.PropertyType))
            {
                var propertiesList = propertyMappingIndex[property.PropertyType];
                propertiesList.Add(propertyName, propertyMap);
            }
            else
            {
                propertyMappingIndex.Add(property.PropertyType,
                                        new Dictionary<string, PropertyTrieNode>()
                                        {
                                            { propertyName, propertyMap }
                                        });
            }
            //Map the Sub properties
            if (IsClass(property))
            {
                propertyDepth++;
                var subProperties = property.PropertyType.GetProperties(BindingFlags.Public | BindingFlags.Instance).ToList();
                subProperties.ForEach(subproperty =>
                {
                    GetProperties(subproperty, propertyName, propertyMappingIndex, propertyMap, propertyDepth);
                });
            }
        }

        /// <summary>
        /// Check if this is a Class Property
        /// </summary>
        /// <param name="property">Property detail</param>
        /// <returns></returns>
        private static bool IsClass(PropertyInfo property)
        {
            if (!property.PropertyType.IsPrimitive &&
                !property.PropertyType.Equals(typeof(string)) &&
                !property.PropertyType.Equals(typeof(decimal)) &&
                !property.PropertyType.IsGenericType &&
                !property.PropertyType.IsArray &&
                property.PropertyType.IsClass &&
                !property.PropertyType.IsEnum)
            {
                return true;
            }
            return false;
        }
    }
}
