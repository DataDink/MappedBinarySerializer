using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Serialization {
    /// <summary>
    /// Binary serialization that is mapped allowing data mutation
    /// </summary>
    public static partial class MappedBinarySerializer {
        /// <summary>
        /// Thrown when an error is encountered.
        /// </summary>
        public class SerializationException : Exception {
            public SerializationException(string message, Exception inner = null) : base(message, inner) {}
        }

        /// <summary>
        /// A binary serialization strategy for reading and writing a value
        /// </summary>
        public interface ISerializationStrategy {
            /// <summary>
            /// The type this strategy can read and write
            /// </summary>
            Type Type { get; }
            /// <summary>
            /// Writes a value
            /// </summary>
            void Write(BinaryWriter writer, object value);
            /// <summary>
            /// Reads a value
            /// </summary>
            object Read(BinaryReader reader);
        }

        private class DefaultStrategy : ISerializationStrategy {
            public Type Type { get; private set; }
            private readonly Func<BinaryReader, object> _read;
            public object Read(BinaryReader reader) => _read(reader);
            private readonly Action<BinaryWriter, object> _write;
            public void Write(BinaryWriter writer, object value) => _write(writer, value);
            public DefaultStrategy(Type type, Func<BinaryReader, object> read, Action<BinaryWriter, object> write) {
                Type = type;
                _read = read;
                _write = write;
            }
        }

        /// <summary>
        /// The default strategies for basic types
        /// </summary>
        public static readonly IEnumerable<ISerializationStrategy> DefaultStrategies = new[] {
            new DefaultStrategy(typeof(bool), r => r.ReadBoolean(), (w,v) => w.Write((bool)v)),
            new DefaultStrategy(typeof(byte), r => r.ReadByte(), (w,v) => w.Write((byte)v)),
            new DefaultStrategy(typeof(sbyte), r => r.ReadSByte(), (w,v) => w.Write((sbyte)v)),
            new DefaultStrategy(typeof(char), r => r.ReadChar(), (w,v) => w.Write((char)v)),
            new DefaultStrategy(typeof(short), r => r.ReadInt16(), (w,v) => w.Write((short)v)),
            new DefaultStrategy(typeof(ushort), r => r.ReadUInt16(), (w,v) => w.Write((ushort)v)),
            new DefaultStrategy(typeof(int), r => r.ReadInt32(), (w,v) => w.Write((int)v)),
            new DefaultStrategy(typeof(uint), r => r.ReadUInt32(), (w,v) => w.Write((uint)v)),
            new DefaultStrategy(typeof(long), r => r.ReadInt64(), (w,v) => w.Write((long)v)),
            new DefaultStrategy(typeof(ulong), r => r.ReadUInt64(), (w,v) => w.Write((ulong)v)),
            new DefaultStrategy(typeof(float), r => r.ReadSingle(), (w,v) => w.Write((float)v)),
            new DefaultStrategy(typeof(double), r => r.ReadDouble(), (w,v) => w.Write((double)v)),
            new DefaultStrategy(typeof(decimal), r => r.ReadDecimal(), (w,v) => w.Write((decimal)v)),
            new DefaultStrategy(typeof(string), r => r.ReadString(), (w,v) => w.Write((string)v))
        };

        private static bool HasStrategy(Type type, IEnumerable<ISerializationStrategy> strategies) {
            return strategies.Any(s => s.Type == type);
        }

        private static Dictionary<string, ISerializationStrategy> CreateStrategyDictionary(IEnumerable<ISerializationStrategy> strategies) {
            return (strategies ?? new ISerializationStrategy[0])
                .Concat(DefaultStrategies)
                .GroupBy(s => s.Type.FullName)
                .ToDictionary(s => s.Key, s => s.First());
        }

        private static IEnumerable<MemberInfo> GetMembers(Type type) {
            return type
                .GetMembers(BindingFlags.Instance | BindingFlags.Public)
                .Where(m => m is PropertyInfo || m is FieldInfo)
                .Where(m => (m as FieldInfo)?.IsInitOnly != true)
                .Where(m => (m as PropertyInfo)?.CanRead != false)
                .Where(m => (m as PropertyInfo)?.CanWrite != false)
                .Where(m => (m as PropertyInfo)?.GetMethod?.IsPublic != false)
                .Where(m => (m as PropertyInfo)?.SetMethod?.IsPublic != false)
                .ToArray();
        }
    }
}
