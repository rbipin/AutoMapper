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
        /// Map the source type to the destination property, this can go N levels deeper and try to man the source type to the target
        /// In case of objects with multiple properties that are of same type, this method will map to the first property it finds.
        /// 
        /// </summary>
        /// <param name="destination">Targer object on which the extension method is called</param>
        /// <param name="source">Source object that will be mapped</param>
        public static void Map(this object destination, object source, bool preserveExistingValue = false)
        {
            if (destination == null)
                throw new NullArgumentException("The desination object (object to which the mapping has to happen) is null",
                    $"{GetCurrentMethodName()}");
            if (source == null)
                throw new NullArgumentException("The source object (object from which the mapping has to happen) is null",
                    $"{GetCurrentMethodName()}");
            
            //Get the source and target object type
            var destinationType = destination.GetType();
            var sourceType = source.GetType();

            //If the destinaton type and source type are same and
            //destination is a list or array, then just assign source
            //Locate the correct property and instance on which the mapping has to happen
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
            //Get the top most node in the tree
            TrieNodeProperty topMostNode = sourcePropertyNodes.GetTopMostRoot();
            //Map the source to the destination
            MapSourceToDestination(destinationNode, topMostNode);

        }

        /// <summary>
        /// Go over the properties and map them to the target (destination)
        /// </summary>
        /// <param name="targetTrieNode">Target Trie node</param>
        /// <param name="sourcePropertyNode">Source Trie Node</param>
        private static void MapSourceToDestination(TrieNodeProperty targetTrieNode, TrieNodeProperty sourcePropertyNode)
        {
           //If the target Instance is null, that mean the property is null
            if (targetTrieNode.Instance == null)
            {
                //get the parent instance and assign the source directly to that.
                var parentInstance = targetTrieNode.GetParent().Instance;
                AssignSourceToDestination(parentInstance, parentInstance, sourcePropertyNode.Instance, targetTrieNode.Property);
                return;
            }

            if (sourcePropertyNode.HasChildren())
            {
                //Since the property has children, then we go deeper perform mapping on them
                //Iterate over each sub property and perform the mapping
                sourcePropertyNode.Children.ForEach(childProperty =>
                {
                    var targetChildProperty = targetTrieNode.Instance.GetType().GetProperty(childProperty.Property.Name);
                    var targetPropertyInstance = targetChildProperty.GetValue(targetTrieNode.Instance);
                    if (targetPropertyInstance == null)
                    {
                        AssignSourceToDestination(targetTrieNode.Instance, targetTrieNode.Instance, childProperty.Instance, targetChildProperty);
                        return;
                    }
                    var targetNode = new TrieNodeProperty(null, targetChildProperty, targetPropertyInstance);
                    MapSourceToDestination(targetNode,
                                            childProperty);
                });

            }
            else
            {
                //Else - this means the property has no more child properties or you have reached the last property.
                //Map directly
                var sourceProperty = sourcePropertyNode.Property;

                var targetChildProperty = targetTrieNode.Instance.GetType().GetProperty(sourceProperty.PropertyType.Name);
                var targetPropertyInstance = targetChildProperty.GetValue(targetTrieNode.Instance);

                AssignSourceToDestination(targetPropertyInstance, targetPropertyInstance, sourcePropertyNode.Instance, targetChildProperty);

            }
        }

        /// <summary>
        /// This method takes care of assigning the value to the targer object
        /// </summary>
        /// <param name="destinationInstance"></param>
        /// <param name="sourceIntance"></param>
        /// <param name="destinationProperty"></param>
        private static void AssignSourceToDestination(object target, 
                                                      object targetData, 
                                                      object sourceIntance, 
                                                      PropertyInfo destinationProperty)
        {
            
            if (destinationProperty.PropertyType.IsGenericType) //Property is a list
                sourceIntance = CopyOverList(targetData, sourceIntance);
            else if (destinationProperty.PropertyType.IsArray) //Property is an array
                sourceIntance = CopyOverArray(targetData, sourceIntance);
            //Set the value
            destinationProperty.SetValue(target, sourceIntance);
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
            object propertyInstance = null;

            //Check the instance of parent property
            if (parentProperty.Instance != null)
                propertyInstance = property.GetValue(parentProperty.Instance);

            //Create the TRIE node
            parentProperty = new TrieNodeProperty(parentProperty, property, propertyInstance);

            propertyName = parentPropertyName.ToUpper();
            //Start indexing the property
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="property"></param>
        /// <param name="propertyMap"></param>
        private static void GetSubProperties(PropertyInfo property,
           TrieNodeProperty propertyMap)
        {
            //Create the object TRIE
            var propertyInstance = property.GetValue(propertyMap.Instance);
            propertyMap = new TrieNodeProperty(propertyMap, property, propertyInstance);
            //Map the Sub properties
            if (IsClass(property))
            {
                //Get all the properties that are public
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

                //Iterate the sub properties
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
        private static TrieNodeProperty LocateSourcePropertyTypeInDestination(object target, object sourceNode, string propertyName = "")
        {
            Dictionary<Type, Dictionary<string, TrieNodeProperty>> destinationPropertyIndex =
                new Dictionary<Type, Dictionary<string, TrieNodeProperty>>();
            destinationPropertyIndex = LoadDestinationPropertyMap(target);
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
                    return propertyNode;
                }
                else
                {
                    var propertyNode = destinationPropertyCollection[propertyName];
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
        /// Copy over an Array, if the array already has items, add them to the array
        /// </summary>
        /// <param name="destinationData">Destination</param>
        /// <param name="SourceData">Source</param>
        /// <returns></returns>
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
