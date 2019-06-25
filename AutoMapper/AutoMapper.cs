using AutoMapper.Exceptions;
using System;
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

            if (destination.GetType().Equals(source.GetType()))
            {
                destination = source;
                return;
            }
                                
            //Property Mapping Index
            Dictionary<Type, Dictionary<string, TrieNodeProperty>> propertyMappingIndex
           = new Dictionary<Type, Dictionary<string, TrieNodeProperty>>();

            Stack<TrieNodeProperty> initializationStack = new Stack<TrieNodeProperty>();

            //Property Trie - Make the top most root, doesn't have a value or a parent
            TrieNodeProperty rootPropertyMap = new TrieNodeProperty(null, null);

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

            //Start mapping, auto memory initializationa and value assigning
            StartAutoMappingProcess(destination, source, propertyNode, preserveExistingValue);
        }

        /// <summary>
        /// Map method override to pass the property name, use this if there are multiple properties with same type,
        /// provide the property name in the format of "ParentProperty.ChildProperty" ChildProperty is the target you want to map the type to, this scan only till
        /// 3 levels deep to look for the property and maps them automatically.
        /// </summary>
        /// <param name="destination">Destination object on which the extension method is called</param>
        /// <param name="source">Source object to map to the destination</param>
        /// <param name="propertyName">Name of the property to Map it to</param>
        public static void Map(this object destination,
            object source,
            string propertyName,
            bool preserveExistingValue = false)
        {
            if (destination == null)
                throw new NullArgumentException("The desination object (object to which the mapping has to happen) is null",
                    $"{GetCurrentMethodName()}");
            if (source == null)
                throw new NullArgumentException("The source object (object from which the mapping has to happen) is null",
                    $"{GetCurrentMethodName()}");
            if (string.IsNullOrEmpty(propertyName))
                throw new NullArgumentException("Property is empty or null, please provide a vaild property name", $"{GetCurrentMethodName()}");

            //Object Mapping tree, hold the index of the properties and it's parents
            Dictionary<Type, Dictionary<string, TrieNodeProperty>> propertyMappingIndex
           = new Dictionary<Type, Dictionary<string, TrieNodeProperty>>();



            //Property Trie - Make the top most root, doesn't have a value or a parent
            TrieNodeProperty rootPropertyMap = new TrieNodeProperty(null, null);


            TrieNodeProperty propertyNode = null;

            propertyName = propertyName.ToUpper();
            //Type of the source
            var sourceType = source.GetType();

            //Build the object mapping tree
            LoadObjectDictionary(destination, propertyMappingIndex, rootPropertyMap);

            //Get the dictionary by property type, this can still have other properties by source type
            var propertyNodes = propertyMappingIndex[sourceType];

            if (propertyNodes.Count > 1) //Check if there is more than one property of source type, if so get the one by the supplied name
                propertyNode = propertyNodes[propertyName];
            else
                propertyNode = propertyNodes.Values.First(); //else get the first one

            //Start mapping, auto memory initializationa and value assigning
            StartAutoMappingProcess(destination, source, propertyNode, preserveExistingValue);

        }

        /// <summary>
        /// Start the auto mapping process, this initializes memory and assigns value
        /// </summary>
        /// <param name="destination"></param>
        /// <param name="source"></param>
        /// <param name="propertyNode"></param>
        /// <param name="preserveExistingValue"></param>
        private static void StartAutoMappingProcess(object destination,
                                                    object source,
                                                    TrieNodeProperty propertyNode,
                                                    bool preserveExistingValue = false)
        {
            Stack<TrieNodeProperty> initializationStack = new Stack<TrieNodeProperty>();

            TrieNodeProperty[] initializationStackCopy = null;

            //Create the object initialization stack - Order in which to initalize the objects 
            initializationStack = CreateObjectInitializationStack(propertyNode);

            //Create a copy of the stack, so that it does not operate on the stack object
            if (initializationStack != null)
                initializationStackCopy = initializationStack.Reverse().ToArray();

            //Auto Initialize the stack
            AutoInitializeStackItems(destination, initializationStackCopy);

            //Identify if we need to preserve the existing values
            preserveExistingValue = IdentifyIfNeedsPreserveExistingValues(initializationStackCopy,
                                                                        preserveExistingValue);

            //Identify the correct Destination Object to operate on
            object correctDestination = IdentifyCorrectDestinationObject(initializationStackCopy,destination);

            //Assign the values to the destination from the source
            AssignSourceDataToDestination(correctDestination, propertyNode, source, preserveExistingValue);

        }

        /// <summary>
        /// Create a stack order in which the memory allocation has to happen for the object
        /// </summary>
        /// <param name="propertyTree"></param>
        /// <returns></returns>
        private static Stack<TrieNodeProperty> CreateObjectInitializationStack(TrieNodeProperty propertyTree)
        {
            Stack<TrieNodeProperty> stackOrderToInitialize = new Stack<TrieNodeProperty>();
            var propertyNode = propertyTree;
            //Add the properties to the Stack
            while (!propertyNode.IsTopRoot())
            {
                stackOrderToInitialize.Push(propertyNode);
                propertyNode = propertyNode.GetParent();
            }
            return stackOrderToInitialize;
        }

        /// <summary>
        /// Auto Initlaize the Parents
        /// </summary>
        /// <param name="destination"></param>
        /// <param name="propertyNode"></param>
        private static void AutoInitializeStackItems(object destination,
            TrieNodeProperty[] objectInitializationOrder)
        {

            Stack<TrieNodeProperty> stackOrderToInitialize = new Stack<TrieNodeProperty>(objectInitializationOrder);

            //Iterate the stack and initalize the property
            while (stackOrderToInitialize.Count > 0)
            {
                var property = stackOrderToInitialize.Pop();
                //Get the property information of the property.
                //PropertyInfo - gives you details and a handle to control that particular property,..
                //but this is not the instance of the property
                var propertyInfo = property.Property;
                if (stackOrderToInitialize.Count == 0)
                {
                    //you have reached the last item in the stack, which is the original property, 
                    //check if the last time is initalized or not, if so index the instance of the property
                    var parentPropertyNode = property.GetParent();

                    object parentInstance = null;
                    if (parentPropertyNode.IsTopRoot())
                        parentInstance = destination;
                    else if (parentPropertyNode.PropertyInstance != null)
                        parentInstance = parentPropertyNode.PropertyInstance;


                    var childPropertyValue = propertyInfo.GetValue(parentInstance, null);
                    if (IsClass(propertyInfo) && childPropertyValue != null)
                        //Index the instance of the property
                        property.PropertyInstance = childPropertyValue;
                    break;
                }

                if (IsClass(propertyInfo))
                {
                    //Verify and instantiate  object
                    var propertyValue = propertyInfo.GetValue(destination, null);
                    if (propertyValue == null)
                    {
                        var newTypeInstance = Activator.CreateInstance(propertyInfo.PropertyType);
                        propertyInfo.SetValue(destination, newTypeInstance);
                        propertyValue = newTypeInstance;
                    }
                    property.PropertyInstance = propertyValue;
                }
            }

        }

        /// <summary>
        /// Identify if last object is a class if it is not initialized already, that means the object does not have any preexisiting value,
        /// the source value can be assigned directly to the target.
        /// </summary>
        /// <param name="objectInitializationStack"></param>
        /// <param name="preserveExising"></param>
        /// <returns></returns>
        private static bool IdentifyIfNeedsPreserveExistingValues(TrieNodeProperty[] objectInitializationStack,
            bool preserveExising)
        {
            Stack<TrieNodeProperty> initializationStack = new Stack<TrieNodeProperty>(objectInitializationStack);
            bool result = false;
            if (preserveExising)
                return result;
            while (initializationStack.Count > 0)
            {
                var propertyNode = initializationStack.Pop();
                //you have reached the last item in the stack, which is the original property, 
                //no need to initialize this as we are going to map the source directly
                var propertyInfo = propertyNode.Property;
                if (IsClass(propertyInfo))
                {
                    var propertyValue = propertyNode.PropertyInstance;
                    if (propertyValue == null)
                        result = false;
                    else
                    {
                        result = true;
                        continue;
                    }
                }

            }
            return result;
        }

        /// <summary>
        /// Identify the correct destination property to operate on, 
        /// I need the correct parent object to set the value to.
        /// </summary>
        /// <param name="initializationStack"></param>
        /// <returns></returns>
        private static object IdentifyCorrectDestinationObject(TrieNodeProperty[] initializationStack,object destination)
        {
            Stack<TrieNodeProperty> newInitializationStack = new Stack<TrieNodeProperty>(initializationStack);
            object destinationObject = destination;
            while (newInitializationStack.Count > 0)
            {
                var propertyNode = newInitializationStack.Pop();
                if (newInitializationStack.Count == 0)
                {
                    if (propertyNode.PropertyInstance != null)
                        return propertyNode.PropertyInstance;
                    else
                        return destinationObject;
                }
                destinationObject = propertyNode.PropertyInstance;
            }
            return destinationObject;
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
          Dictionary<Type, Dictionary<string, TrieNodeProperty>> propertyMapping,
          TrieNodeProperty rootPropertyMap
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
            TrieNodeProperty propertyNode,
            object source,
            bool preserveExistingProperty)
        {
            object valueToSetToDestination = null;
            var property = propertyNode.Property;
            if (property.PropertyType.IsGenericType || property.PropertyType.IsArray)
            {
                valueToSetToDestination = CopyOverListorArray(property.GetValue(destination), source);
                property.SetValue(destination, valueToSetToDestination);
                return;
            }
            //if (property.PropertyType.IsArray)
            //{
            //    valueToSetToDestination = CopyOverArray(property.GetValue(destination), source);
            //    property.SetValue(destination, valueToSetToDestination);
            //    return;
            //}
            //Preserve the original value of the property if only a few properties have values in the 
            if (preserveExistingProperty)
            {
                CopyOverWhilePreservingExisitingProperty(destination, propertyNode, source);
                return;
            }
            property.SetValue(destination, source);

        }


        /// <summary>
        /// Copy Data to the destination from source while keeping the already assigned property values intact
        /// </summary>
        /// <param name="destination">destination object to copy to</param>
        /// <param name="source">source object to copy from</param>
        private static void CopyOverWhilePreservingExisitingProperty(object destination,
            TrieNodeProperty propertyTrie
            , object source)
        {
            //Get all the public properties of the current source object, filter out the once with null values
            var SourceProperties = source.GetType()
              .GetProperties(BindingFlags.Instance | BindingFlags.Public)
              .Where(x => x.GetValue(source) != null
                        && !x.GetMethod.IsPrivate
                        && !x.SetMethod.IsPrivate).ToList();

            //Filter out the public value types with default values
            SourceProperties.ToList().ForEach(prop =>
            {
                if (prop.PropertyType.IsValueType && object.Equals(prop.GetValue(source), Activator.CreateInstance(prop.PropertyType)))
                    SourceProperties.Remove(prop);
            });

            var destinationProperty = propertyTrie.Property;
            //Assign the value for each of the identified properties to the destination
            SourceProperties.ForEach(sourceProperty =>
            {
                var destinationProp = destinationProperty.PropertyType.GetProperty(sourceProperty.Name);
                var sourceValue = sourceProperty.GetValue(source);
                destinationProp.SetValue(propertyTrie.PropertyInstance, sourceValue);

            });
        }

        /// <summary>
        /// Copy over a list, if the destination list already has data, will be combine them.
        /// </summary>
        /// <param name="destinationData">Destination</param>
        /// <param name="SourceData">Source</param>
        /// <returns></returns>
        private static object CopyOverListorArray(object destinationData, object SourceData)
        {
            var destinationList = destinationData as IEnumerable<object>;
            var sourceList = SourceData as IEnumerable<object>;
            if (destinationList != null && destinationList.Count() > 0)
            {

                var clonedList = new List<object>();
                clonedList.AddRange(destinationList);
                clonedList.AddRange(sourceList);
                return clonedList;
            }
            else
            {
                return sourceList;
            }

        }

        ///// <summary>
        ///// Copy over an Array, if the array already has items, add them to the array
        ///// </summary>
        ///// <param name="destinationData">Destination</param>
        ///// <param name="SourceData">Source</param>
        ///// <returns></returns>
        private static object CopyOverArray(object destinationData, object SourceData)
        {
            var destinationList = destinationData as IEnumerable<object>;
            var sourceList = SourceData as IEnumerable<object>;
            if (destinationList != null && destinationList.Count() > 0)
            {
                var newClonedList = new List<object>();
                newClonedList.AddRange(destinationList);
                newClonedList.AddRange(sourceList);
                return newClonedList;
            }
            else
            {
                return sourceList;
            }


            //ArrayList tempArrayList = new ArrayList();
            //var destinationArray = destinationData as Array;
            //var sourceArray = SourceData as Array;
            //var destArrayCount = destinationArray.Length;
            //if (destinationArray != null && destArrayCount > 0)
            //{
            //    tempArrayList.AddRange(destinationArray);
            //    tempArrayList.AddRange(sourceArray);
            //    return tempArrayList.ToArray();
            //}
            //else
            //{
            //    return sourceArray;
            //}
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
            TrieNodeProperty propertyMap,
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
            propertyMap = new TrieNodeProperty(propertyMap, property);

            if (propertyMappingIndex.ContainsKey(property.PropertyType))
            {
                var propertiesList = propertyMappingIndex[property.PropertyType];
                propertiesList.Add(propertyName, propertyMap);
            }
            else
            {
                propertyMappingIndex.Add(property.PropertyType,
                                        new Dictionary<string, TrieNodeProperty>()
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
