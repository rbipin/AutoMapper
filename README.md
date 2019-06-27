# AutoMapper
Mapping a part of object to another complex object becomes a tedious process.
This is an auto mapper solution that can be used to solve this problem.

### Features
  * Automatically finds the first occurance of a object type and maps them.
  * If there are multiple object of same type in the target object, you can specify the propety name to map it to.
  * If the object already has some data in it, this will preserve the existing once and map only the new once.
  * If target propert is `null`, the automapper can initialize the property and map the values.
  * If the target property is a `List` or an `Array[]`, it can initilize if the property is `null` or if the it already has values the new values get added to it.

