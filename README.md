
```
                _        __  __                             
     /\        | |      |  \/  |                            
    /  \  _   _| |_ ___ | \  / | __ _ _ __  _ __   ___ _ __ 
   / /\ \| | | | __/ _ \| |\/| |/ _` | '_ \| '_ \ / _ \ '__|
  / ____ \ |_| | || (_) | |  | | (_| | |_) | |_) |  __/ |   
 /_/    \_\__,_|\__\___/|_|  |_|\__,_| .__/| .__/ \___|_|   
                                     | |   | |              
                                     |_|   |_|              
```
Mapping a part of object to another complex object becomes a tedious process if there are object is very complex or if there are lists and arrays in them.
This is an object auto mapper, that will help simplify the mapping.

### Features
* Automatically finds the first occurrence of an object type and maps them.
* If there are multiple objects of the same type in the target object, you can specify the property name to map it to.
* If the object already has some data in it, this will preserve the existing once and map only the new once.
* If target property is ```null```, the automapper can initialize the property and map the values.
* If the target property is a ```List``` or an ```Array[]```, it can initialize if the property is ```null``` or if it already has values the new values get added to it.

