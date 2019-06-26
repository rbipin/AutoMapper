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
        //static bool preserveExisting = false;
        /// <summary>
        /// Map the source type to the destination property, this method can scan 3 levels deep and map automatically if the type is unique
        /// Scan is restricted to only 3 levels down to handle larger/ complicated objects that could have multiple properties of same type.
        /// </summary>
        /// <param name="destination">Destination object on which the extension method is called</param>
        /// <param name="source">Source object that will be mapped</param>
        public static void Map(this object destination, object source, bool preserveExistingValue = false)
        {
            if (destination == null)
                throw new NullArgumentException("The desination object (object to which the mapping has to happen) is null",
                    $"{GetCurrentMethodName()}");
            if (source == null)
                throw new NullArgumentException("The source object (object from which the mapping has to happen) is null",
                    $"{GetCurrentMethodName()}");
            var destinationType = destination.GetType();
            var sourceType = source.GetType();

            //If the destinaton type and source type are same and
            //destination is a list or array, then just assign source

            TrieNodeProperty destinationNode = LocateSourcePropertyTypeInDestination(destination, source);

            if (sourceType.IsGenericType || sourceType.IsArray)
            {
                var existingValue = destinationNode.Property.GetValue(destination);
                AssignSourceToDestination(destination, existingValue, source, destinationNode.Property);
                return;
            }

            //Property Trie - Make the top most root, doesn't have a value or a parent
            TrieNodeProperty sourcePropertyNode = LoadSourceObjectDictionary(source);

            //Start mapping, auto memory initializationa and value assigning
            StartAutoMappingProcess(destinationNode, sourcePropertyNode, preserveExistingValue);
        }

        /// <summary>
        /// Start the auto mapping process, this initializes memory and assigns value
        /// </summary>
        /// <param name="destination"></param>
        /// <param name="source"></param>
        /// <param name="propertyNode"></param>
        /// <param name="preserveExistingValue"></param>
        private static void StartAutoMappingProcess(TrieNodeProperty destinationNode,
                                                    TrieNodeProperty sourcePropertyNodes,
                                                    bool preserveExistingValue = false)
        {
            TrieNodeProperty topMostNode = sourcePropertyNodes.GetTopMostRoot();

            MapSourceToDestination(destinationNode, topMostNode);

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="destinationNode"></param>
        /// <param name="sourcePropertyNode"></param>
        private static void MapSourceToDestination(TrieNodeProperty destinationNode, TrieNodeProperty sourcePropertyNode)
        {
            if (destinationNode.Instance == null)
            {
                var parentInstance = destinationNode.GetParent().Instance;
                AssignSourceToDestination(parentInstance, parentInstance, sourcePropertyNode.Instance, destinationNode.Property);
                return;
            }
            if (sourcePropertyNode.HasChildren())
            {
                sourcePropertyNode.Children.ForEach(childProperty =>
                {
                    var targetChildProperty = destinationNode.Instance.GetType().GetProperty(childProperty.Property.Name);
                    var targetPropertyInstance = targetChildProperty.GetValue(destinationNode.Instance);
                    if (targetPropertyInstance == null)
                    {
                        AssignSourceToDestination(destinationNode.Instance, destinationNode.Instance, childProperty.Instance, targetChildProperty);
                        return;
                    }
                    var targetNode = new TrieNodeProperty(null, targetChildProperty, targetPropertyInstance);
                    MapSourceToDestination(targetNode,
                                            childProperty);
                });

            }
            else
            {
                var sourceProperty = sourcePropertyNode.Property;

                var targetChildProperty = destinationNode.Instance.GetType().GetProperty(sourceProperty.PropertyType.Name);
                var targetPropertyInstance = targetChildProperty.GetValue(destinationNode.Instance);

                AssignSourceToDestination(targetPropertyInstance, targetPropertyInstance, sourcePropertyNode.Instance, targetChildProperty);

            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="destinationInstance"></param>
        /// <param name="sourceIntance"></param>
        /// <param name="destinationProperty"></param>
        private static void AssignSourceToDestination(object destination, object destinationData, object sourceIntance, PropertyInfo destinationProperty)
        {
            if (destinationProperty.PropertyType.IsGenericType)
                sourceIntance = CopyOverList(destinationData, sourceIntance);
            else if (destinationProperty.PropertyType.IsArray)
                sourceIntance = CopyOverArray(destinationData, sourceIntance);

            destinationProperty.SetValue(destination, sourceIntance);
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
            Dictionary<Type, Dictionary<string, TrieNodeProperty>> propertyMappingIndex,
            TrieNodeProperty parentProperty)
        {
            string propertyName = string.Empty;
            object propertInstance = null;

            //Create the object TRIE
            if (parentProperty.Instance != null)
                propertInstance = property.GetValue(parentProperty.Instance);
            else
                propertInstance = null;

            parentProperty = new TrieNodeProperty(parentProperty, property, propertInstance);
            propertyName = parentPropertyName.ToUpper();
            if (propertyMappingIndex.ContainsKey(property.PropertyType))
            {
                var propertiesList = propertyMappingIndex[property.PropertyType];
                propertiesList.Add(propertyName, parentProperty);
            }
            else
            {
                propertyMappingIndex.Add(property.PropertyType,
                                        new Dictionary<string, TrieNodeProperty>()
                                        {
                                            { propertyName, parentProperty }
                                        });
            }
            //Map the Sub properties
            if (IsClass(property))
            {
                var subProperties = property.PropertyType.GetProperties(BindingFlags.Public | BindingFlags.Instance).ToList();
                subProperties.ForEach(subproperty =>
                {
                    propertyName = $"{parentPropertyName}.{subproperty.Name}";
                    GetProperties(subproperty, propertyName, propertyMappingIndex, parentProperty);
                });
            }
        }


        private static void GetSubProperties(PropertyInfo property,
           TrieNodeProperty propertyMap)
        {
            //Create the object TRIE
            var propertyInstance = property.GetValue(propertyMap.Instance);
            propertyMap = new TrieNodeProperty(propertyMap, property, propertyInstance);
            //Map the Sub properties
            if (IsClass(property))
            {

                var subProperties = property.PropertyType
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(x => !x.GetMethod.IsPrivate
                        && !x.SetMethod.IsPrivate)
                    .ToList();
                //Get the could of properties that are public
                var publicPropertiesCount = subProperties.Count;

                //Filter null properties
                subProperties = subProperties
                    .Where(x => x.GetValue(propertyInstance) != null)
                    .ToList();

                //Filter out value types with default value
                subProperties.ToList().ForEach(prop =>
                {
                    if (prop.PropertyType.IsValueType && object.Equals(prop.GetValue(propertyInstance), Activator.CreateInstance(prop.PropertyType)))
                        subProperties.Remove(prop);
                });

                //Get the new count of properties after filtering
                var filteredPublicPropertiesCount = subProperties.Count;

                //if the count does not match, then preserve existings
                if (publicPropertiesCount != filteredPublicPropertiesCount)
                    propertyMap.PreserveExisting = true;


                subProperties.ForEach(subproperty =>
                {
                    GetSubProperties(subproperty, propertyMap);
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
        /// 
        /// </summary>
        /// <param name="destination"></param>
        /// <param name="sourceNode"></param>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        private static TrieNodeProperty LocateSourcePropertyTypeInDestination(object destination, object sourceNode, string propertyName = "")
        {
            Dictionary<Type, Dictionary<string, TrieNodeProperty>> destinationPropertyIndex =
                new Dictionary<Type, Dictionary<string, TrieNodeProperty>>();
            destinationPropertyIndex = LoadDestinationPropertyMap(destination);
            TrieNodeProperty correctProperty = null;

            var sourceType = sourceNode.GetType();
            var destinationPropertyCollection = destinationPropertyIndex[sourceType];
            if (destinationPropertyCollection.Count == 1)
            {
                var propertyNode = destinationPropertyCollection.Values.First();
                correctProperty = new TrieNodeProperty(null, propertyNode.Property, propertyNode.Instance);
                return correctProperty;
            }
            else if (destinationPropertyCollection.Count > 1)
            {
                if (string.IsNullOrEmpty(propertyName))
                {
                    var propertyNode = destinationPropertyCollection.Values.First();
                    //correctProperty = new TrieNodeProperty(null, propertyNode.Property, propertyNode.Instance);
                    return propertyNode;
                }
                else
                {
                    var propertyNode = destinationPropertyCollection[propertyName];
                    //correctProperty = new TrieNodeProperty(null, propertyNode.Property, propertyNode.Instance);
                    return propertyNode;
                }
            }
            destinationPropertyIndex.Clear();
            return null;
        }

        /// <summary>
        /// Map the Object dictionary
        /// </summary>
        /// <param name="destination">Destination object</param>
        /// <param name="propertyMapping">Property mapping dictionary</param>
        private static Dictionary<Type, Dictionary<string, TrieNodeProperty>> LoadDestinationPropertyMap(object destination)
        {
            Dictionary<Type, Dictionary<string, TrieNodeProperty>> propertyMapping =
                new Dictionary<Type, Dictionary<string, TrieNodeProperty>>();

            TrieNodeProperty rootNode = new TrieNodeProperty(null, null, destination);
            var publicProperties = destination.GetType()
                  .GetProperties(BindingFlags.Public | BindingFlags.Instance).ToList();
            publicProperties.ForEach(property =>
            {
                GetProperties(property,
                              property.Name,
                              propertyMapping,
                              rootNode);
            });

            return propertyMapping;
        }

        /// <summary>
        /// Map the Object dictionary
        /// </summary>
        /// <param name="destination">Destination object</param>
        /// <param name="propertyMapping">Property mapping dictionary</param>
        private static TrieNodeProperty LoadSourceObjectDictionary(object source)
        {

            TrieNodeProperty propertyMappedTrie = new TrieNodeProperty(null, null);
            propertyMappedTrie.Instance = source;

            var publicProperties = source.GetType()
                  .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                  .Where(x => !x.GetMethod.IsPrivate
                        && !x.SetMethod.IsPrivate)
                        .ToList();

            var publicPropertiesCount = publicProperties.Count;

            publicProperties = source.GetType()
                  .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                  .Where(x => x.GetValue(source) != null)
                        .ToList();

            //Filter out value types with default value
            publicProperties.ToList().ForEach(prop =>
            {
                if (prop.PropertyType.IsValueType && object.Equals(prop.GetValue(source), Activator.CreateInstance(prop.PropertyType)))
                    publicProperties.Remove(prop);
            });

            var filteredPublicPropertiesCount = publicProperties.Count;

            if (publicPropertiesCount != filteredPublicPropertiesCount)
                propertyMappedTrie.PreserveExisting = true;

            publicProperties.ForEach(property =>
            {
                GetSubProperties(property, propertyMappedTrie);
            });
            return propertyMappedTrie;
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
        /// Map method override to pass the property name, use this if there are multiple properties with same type,
        /// provide the property name in the format of "ParentProperty.ChildProperty" ChildProperty is the target you want to map the type to, this scan only till
        /// 3 levels deep to look for the property and maps them automatically.
        /// </summary>
        /// <param name="destination">Destination object on which the extension method is called</param>
        /// <param name="source">Source object to map to the destination</param>
        /// <param name="propertyName">Name of the property to Map it to</param>
        //public static void Map(this object destination,
        //    object source,
        //    string propertyName,
        //    bool preserveExistingValue = false)
        //{
        //    if (destination == null)
        //        throw new NullArgumentException("The desination object (object to which the mapping has to happen) is null",
        //            $"{GetCurrentMethodName()}");
        //    if (source == null)
        //        throw new NullArgumentException("The source object (object from which the mapping has to happen) is null",
        //            $"{GetCurrentMethodName()}");
        //    if (string.IsNullOrEmpty(propertyName))
        //        throw new NullArgumentException("Property is empty or null, please provide a vaild property name", $"{GetCurrentMethodName()}");

        //    //Object Mapping tree, hold the index of the properties and it's parents
        //    Dictionary<Type, Dictionary<string, TrieNodeProperty>> propertyMappingIndex
        //   = new Dictionary<Type, Dictionary<string, TrieNodeProperty>>();



        //    //Property Trie - Make the top most root, doesn't have a value or a parent
        //    TrieNodeProperty rootPropertyMap = new TrieNodeProperty(null, null);


        //    TrieNodeProperty propertyNode = null;

        //    propertyName = propertyName.ToUpper();
        //    //Type of the source
        //    var sourceType = source.GetType();

        //    //Build the object mapping tree
        //    LoadObjectDictionary(destination, propertyMappingIndex, rootPropertyMap);

        //    //Get the dictionary by property type, this can still have other properties by source type
        //    var propertyNodes = propertyMappingIndex[sourceType];

        //    if (propertyNodes.Count > 1) //Check if there is more than one property of source type, if so get the one by the supplied name
        //        propertyNode = propertyNodes[propertyName];
        //    else
        //        propertyNode = propertyNodes.Values.First(); //else get the first one

        //    //Start mapping, auto memory initializationa and value assigning
        //    StartAutoMappingProcess(destination, source, propertyNode, preserveExistingValue);

        //}

        ///// <summary>
        ///// Copy over an Array, if the array already has items, add them to the array
        ///// </summary>
        ///// <param name="destinationData">Destination</param>
        ///// <param name="SourceData">Source</param>
        ///// <returns></returns>
        private static object CopyOverArray(object destinationData, object SourceData)
        {
            var destinationList = destinationData as Array;
            var sourceList = SourceData as Array;
            var itemCount = sourceList.Length;
            var elementType = SourceData.GetType().GetElementType();

            var tempArray = new ArrayList();
            if (destinationList != null && destinationList.Length > 0)
                tempArray.AddRange(destinationList);
            tempArray.AddRange(sourceList);

            var cloneData = Array.CreateInstance(elementType, tempArray.Count);
            for (int counter = 0; counter < tempArray.Count; counter++)
            {
                cloneData.SetValue(tempArray[counter], counter);
            }
            return cloneData;
        }

  }
}
