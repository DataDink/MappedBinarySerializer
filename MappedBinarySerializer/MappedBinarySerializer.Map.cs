using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Serialization
{
    public static partial class MappedBinarySerializer
    {
        /// <summary>
        /// Defines a layout for writing/reading binary formatting
        /// </summary>
        public static class Map {

            /// <summary>
            /// Parses a map from a string
            /// </summary>
            public static Node Parse(string map) {
                var index = 0;
                return Read(map.ToCharArray(), ref index);
            }

            private static Node Read(char[] map, ref int index) {
                Trim(map, ref index);
                if (map[index] == '"') { return ReadContent(map, ref index); }
                if (map[index] == '[') { return ReadCollection(map, ref index); }
                if (map[index] == '{') { return ReadMap(map, ref index); }
                throw new SerializationException($"Failed to parse map at {index}");
            }

            private static void Trim(char[] map, ref int index, char deliminator = default) {
                while (char.IsWhiteSpace(map[index]) || map[index] == deliminator) { index++; }
            }

            private static Content ReadContent(char[] map, ref int index) {
                var builder = new StringBuilder();
                while(map[++index] != '"') {
                    builder.Append(map[index]);
                }
                index++;
                return new Content(builder.ToString());
            }

            private static Collection ReadCollection(char[] map, ref int index) {
                index ++;
                var contents = Read(map, ref index);
                Trim(map, ref index);
                if (map[index] != ']') {
                    throw new SerializationException($"Expected ']' at {index}");
                }
                index++;
                return new Collection(contents);
            }

            private static Model ReadMap(char[] map, ref int index) {
                var members = new List<KeyValuePair<string, Node>>();
                index++;
                while (map[index] != '}') {
                    Trim(map, ref index);
                    var name = ReadContent(map, ref index).Value;
                    Trim(map, ref index, ':');
                    var value = Read(map, ref index);
                    Trim(map, ref index, ',');
                    members.Add(new KeyValuePair<string, Node>(name, value));
                }
                return new Model(members);
            }

            /// <summary>
            /// Formats a map based on a type and optional ISerializationStrategies
            /// </summary>
            public static string Format(Type type, IEnumerable<ISerializationStrategy> strategies = null) 
                => Format(type, CreateStrategyDictionary(strategies));

            private static string Format(Type type, Dictionary<string, ISerializationStrategy> strategies) {
                if (strategies.ContainsKey(type.FullName)) { return FormatContent(type, strategies); }
                if (type.IsArray) { return FormatCollection(type, strategies); }
                return FormatMap(type, strategies);
            }

            private static string FormatContent(Type type, Dictionary<string, ISerializationStrategy> strategies) {
                return $"\"{type.FullName}\"";
            }

            private static string FormatCollection(Type type, Dictionary<string, ISerializationStrategy> strategies) {
                return $"[{Format(type.GetElementType(), strategies)}]";
            }

            private static string FormatMap(Type type, Dictionary<string, ISerializationStrategy> strategies) {
                var members = 
                    GetMembers(type)
                    .Select(m => 
                        $"\"{m.Name}\":{Format((m as FieldInfo)?.FieldType ?? (m as PropertyInfo)?.PropertyType, strategies)}"
                    );
                return $"{{{string.Join(",", members)}}}";
            }
            /// <summary>
            /// Base Map.Node class
            /// </summary>
            public abstract class Node {}
            /// <summary>
            /// Represents a serializable value
            /// </summary>
            public class Content : Node {
                /// <summary>
                /// The serialization strategy name
                /// </summary>
                public readonly string Value;
                /// <inheritdoc />
                public Content(string value) => Value = value;
            }
            /// <summary>
            /// Represents a collection of nodes
            /// </summary>
            public class Collection : Node {
                /// <summary>
                /// The Array element definition
                /// </summary>
                public readonly Node Contents;
                /// <inheritdoc />
                public Collection(Node contents) => Contents = contents;
            }
            /// <summary>
            /// Represents a data model
            /// </summary>
            public class Model : Node, IEnumerable<KeyValuePair<string, Node>> {
                private readonly KeyValuePair<string, Node>[] Nodes;
                /// <inheritdoc />
                public Model(IEnumerable<KeyValuePair<string, Node>> nodes) 
                    => Nodes = nodes.ToArray();
                /// <inheritdoc />
                public IEnumerator<KeyValuePair<string, Node>> GetEnumerator()
                    => Nodes.AsEnumerable().GetEnumerator();
                /// <inheritdoc />
                IEnumerator IEnumerable.GetEnumerator()
                    => Nodes.GetEnumerator();
            }
        }
    }
}
