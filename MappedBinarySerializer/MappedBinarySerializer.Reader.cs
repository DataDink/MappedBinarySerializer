using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Serialization {
    public static partial class MappedBinarySerializer {
        /// <summary>
        /// A binary serialization reader that is precompiled to a Map and Type and should be cached for performance
        /// </summary>
        public class Reader {
            /// <summary>
            /// Describes a single unit of deserialization
            /// </summary>
            private delegate object ValueReader(BinaryReader reader);
            /// <summary>
            /// The entry point for this reader
            /// </summary>
            private readonly ValueReader Start;
            /// <summary>
            /// A compiled MappedBinarySerializer Reader
            /// </summary>
            private Reader(ValueReader start)
                => Start = start;
            /// <summary>
            /// Reads an entity from a byte array
            /// </summary>
            public object Read(byte[] bytes) {
                using (var stream = new MemoryStream(bytes)) {
                    return Read(stream);
                }
            }
            /// <summary>
            /// Reads an entity from a stream
            /// </summary>
            public object Read(Stream stream) {
                using (var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, true)) {
                    return Read(reader);
                }
            }
            /// <summary>
            /// Reads an entity from a BinaryReader
            /// </summary>
            public object Read(BinaryReader reader)
                => Start(reader);
            /// <inheritdoc cref="Compile(Map.Node, Type, IEnumerable&lt;ISerializationStrategy&gt;)" />
            public static Reader Compile(string map, Type type, IEnumerable<ISerializationStrategy> strategies = null)
                => Compile(Map.Parse(map), type, strategies);
            /// <summary>
            /// Compiles a MappedBinarySerializer.Reader that should be cached for performance
            /// </summary>
            public static Reader Compile(Map.Node map, Type type, IEnumerable<ISerializationStrategy> strategies = null)
                => new Reader(Build(map, type, CreateStrategyDictionary(strategies)));
            /// <summary>
            /// Prefetches member/array setters and strategies and builds a ValueReader
            /// </summary>
            private static ValueReader Build(Map.Node map, Type type, Dictionary<string, ISerializationStrategy> strategies) {
                var model = map as Map.Model;
                if (model != default) { return BuildModel(model, type, strategies); }
                var collection = map as Map.Collection;
                if (collection != null) { return BuildCollection(collection, type, strategies); }
                var content = map as Map.Content;
                if (content != null) { return BuildContent(content, type, strategies); }
                return default;
            }
            /// <summary>
            /// Prefetches a strategy and builds a ValueReader
            /// </summary>
            private static ValueReader BuildContent(Map.Content content, Type type, Dictionary<string, ISerializationStrategy> strategies) {
                if (!strategies.ContainsKey(content.Value)) {
                    throw new SerializationException($"Missing serialization strategy: {content.Value}");
                }
                var strategy = strategies[content.Value];
                return reader => strategy.Read(reader);
            }
            /// <summary>
            /// Compiles an array builder and builds a ValueReader
            /// </summary>
            private static ValueReader BuildCollection(Map.Collection collection, Type type, Dictionary<string, ISerializationStrategy> strategies) {
                var element = type.IsArray ? type.GetElementType() : typeof(object);
                var content = collection.Contents;
                var readFactory = Build(content, element, strategies);
                return reader => {
                    var count = reader.ReadInt32();
                    var array = Array.CreateInstance(element, count);
                    for (var i = 0; i < count; i++) {
                        array.SetValue(readFactory(reader), i);
                    }
                    return array;
                };
            }
            /// <summary>
            /// Prefetches member setters and builds a ValueReader
            /// </summary>
            private static ValueReader BuildModel(Map.Model model, Type type, Dictionary<string, ISerializationStrategy> strategies) {
                var setters = model
                    .Select(member => {
                        var info = GetMembers(type).FirstOrDefault(m => m.Name == member.Key);
                        var setter = 
                            info is PropertyInfo ? ((PropertyInfo)info).SetValue
                            : info is FieldInfo ? ((FieldInfo)info).SetValue
                            : new Action<object, object>((foo, bar) => {});
                        var memberType = 
                            (info as PropertyInfo)?.PropertyType
                            ?? (info as FieldInfo)?.FieldType
                            ?? typeof(object);
                        var read = Build(member.Value, memberType, strategies);
                        return new Action<BinaryReader, object>(
                            (reader, entity) => setter(entity, read(reader))
                        );
                    })
                    .ToArray();
                return reader => {
                    var entity = Activator.CreateInstance(type);
                    foreach (var setter in setters) {
                        setter(reader, entity);
                    }
                    return entity;
                };
            }
        }
    }
}
