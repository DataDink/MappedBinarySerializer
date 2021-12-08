using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Serialization {
    public static partial class MappedBinarySerializer {
        /// <summary>
        /// A binary serialization writer that is precompiled to a Map and Type and should be cached for performance
        /// </summary>
        public class Writer {
            /// <summary>
            /// Describes a single unit of serialization
            /// </summary>
            private delegate IEnumerable<ValueWriter> ValueWriter(BinaryWriter writer);
            /// <summary>
            /// Represents a entry point for pre-compiled serialization
            /// </summary>
            private delegate ValueWriter WriterNode(object data);
            /// <summary>
            /// The entry point for this writer
            /// </summary>
            private readonly WriterNode Start;
            /// <summary>
            /// A compiled MappedBinarySerializer Writer
            /// </summary>
            private Writer(WriterNode start) 
                => Start = start;
            /// <summary>
            /// Serializes data to a byte array
            /// </summary>
            public byte[] Write(object data) {
                using (var stream = new MemoryStream()) {
                    Write(data, stream);
                    stream.Position = 0;
                    return stream.ToArray();
                }
            }
            /// <summary>
            /// Serializes data to a Stream
            /// </summary>
            public void Write(object data, Stream stream) {
                using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true)) {
                    Write(data, writer);
                }
            }
            /// <summary>
            /// Serializes data to a BinaryWriter
            /// </summary>
            public void Write(object data, BinaryWriter writer) 
                => Write(Start(data)(writer), writer);
            /// <summary>
            /// Recursively writes data to a binary writer
            /// </summary>
            private void Write(IEnumerable<ValueWriter> writes, BinaryWriter writer) {
                if (writes == null) { return; }
                foreach (var write in writes) {
                    var moreWrites = write(writer);
                    Write(moreWrites, writer);
                }
            }
            /// <inheritdoc cref="Compile(Map.Node, Type, IEnumerable&lt;ISerializationStrategy&gt;)" />
            public static Writer Compile(string map, Type type, IEnumerable<ISerializationStrategy> strategies = null)
                => Compile(Map.Parse(map), type, strategies);
            /// <summary>
            /// Precompiles a MappedBinarySerializer.Writer that should be cached for performance
            /// </summary>
            public static Writer Compile(Map.Node map, Type type, IEnumerable<ISerializationStrategy> strategies = null)
                => new Writer(Build(map, type, CreateStrategyDictionary(strategies)));
            /// <summary>
            /// Prefetches member/array getters and strategies and compiles a WriterNode
            /// </summary>
            private static WriterNode Build(Map.Node map, Type type, IDictionary<string, ISerializationStrategy> strategies) {
                var model = map as Map.Model;
                if (model != null) { return BuildModel(model, type, strategies); }
                var collection = map as Map.Collection;
                if (collection != null) { return BuildCollection(collection, type, strategies); }
                var content = map as Map.Content;
                if (content != null) { return BuildContent(content, type, strategies); }
                return default;
            }
            /// <summary>
            /// Prefetches a content Serialization strategy and builds a WriterNode
            /// </summary>
            private static WriterNode BuildContent(Map.Content content, Type type, IDictionary<string, ISerializationStrategy> strategies) {
                if (!strategies.ContainsKey(content.Value)) {
                    throw new SerializationException($"Missing serialization strategy: {content.Value}");
                }
                var strategy = strategies[content.Value];
                var defaultValue = strategy.Type.IsValueType
                    ? Activator.CreateInstance(strategy.Type)
                    : default;
                return (value) => (writer) => {
                    strategy.Write(writer, value is Missing ? defaultValue : value);
                    return default;
                };
            }
            /// <summary>
            /// Prefetches an array getter and builds a WriterNode
            /// </summary>
            private static WriterNode BuildCollection(Map.Collection collection, Type type, IDictionary<string, ISerializationStrategy> strategies) {
                var collectionType = type.GetElementType();
                var countWriter = strategies[typeof(int).FullName];
                var itemWriter = Build(collection.Contents, collectionType, strategies);
                return (data) => (writer) => {
                    var array = data as Array;
                    var count = array?.Length ?? 0;
                    var writers = new ValueWriter[count + 1];
                    writers[0] = _ => {
                        countWriter.Write(writer, count);
                        return default;
                    };
                    for (var i = 0; i < count; i++) {
                        writers[i + 1] = itemWriter(array.GetValue(i));
                    }
                    return writers;
                };
            }
            /// <summary>
            /// Prefetches member getters and builds a WriterNode
            /// </summary>
            private static WriterNode BuildModel(Map.Model map, Type type, IDictionary<string, ISerializationStrategy> strategies) {
                var factories = map
                    .Select(mapMember => {
                        var memberInfo = GetMembers(type).FirstOrDefault(info => info.Name == mapMember.Key);
                        var memberType = (memberInfo as FieldInfo)?.FieldType 
                            ?? (memberInfo as PropertyInfo)?.PropertyType
                            ?? Type.GetType((mapMember.Value as Map.Content)?.Value, false)
                            ?? typeof(object);
                        var memberGetter = memberInfo is FieldInfo ? ((FieldInfo)memberInfo).GetValue
                            : memberInfo is PropertyInfo ? ((PropertyInfo)memberInfo).GetValue
                            : new Func<object, object>(_ => default);
                        var writerFactory = Build(mapMember.Value, memberType, strategies);
                        return new Func<object, ValueWriter>(
                            model => writerFactory(memberGetter(model))
                        );
                    })
                    .ToArray();
                return (model) => (writer) => factories.Select(f => f(model));
            }
        }
    }
}
