# MappedBinarySerializer
 A thought on tollerant, mapped binary serialization, customizable, balanced to performance

 # How To

 **Generate a map**

 A map defines how a type is serialized or deserialized based on member names and an optional collection of ISerializationStrategy
 ```C#
 var map = MappedBinarySerializer.Map.Format(typeof(YourDataModel));
 ```

 **Compile a serializer**

 Serialization is compiled based on a map/type combination. These should be cached for performance.
 ```C#
 var reader = MappedBinarySerializer.Reader.Compile(map, typeof(YourDataModel));
 var writer = MappedBinarySerializer.Writer.Compile(map, typeof(YourDataModel));
 ```

 **Reading and writing to a stream**

 Use pre-compiled writers/readers to convert data to/from binary
 ```C#
 using (var stream = new MemoryStream()) {
   writer.Write(data, stream);
   var newData = reader.Read(stream);
 }
 ```

# Goals

To create a performant method of serializing data to a minimal
binary format, with data-version tolerance,
and the speed of traditional run-time binary formatting.

To accomplish this goal, this approach uses a separated member-map
to define the serialized data's shape without encoding it into the binary
format itself. Readers and writers are pre-compiled run-time based on
a map/type combination and aren't required to be matching type versions. 
These can then be cached in an application for performant, tollerant, 
light-weight communications.

# Notes

This solution out-performs the native C# formatters
(depreciated for security reasons), but it doesn't
match the .net protobuffs performance based on some
crude benchmarking. 

The data size should be as small
as possible for basic data types without using 
a compression algorithm. Data-type serialization can be 
extended / customized to further reduce data size where
appropriate. Well-known types are not covered in the
default serialization strategies (e.g. DateTime, etc).

With some tweeking this solution could be more platform agnostic.
Currently serialization maps are based on .NET type names
and namespaces. These could be changed to be based on a set of
well-known names that would make sense to other platforms.
