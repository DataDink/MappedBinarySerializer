#pragma warning disable 649
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using NUnit.Framework;

namespace Serialization {
  [TestFixture]
  public class MappedBinarySerializerTests {

    private class SerializedFlatData {
      public string C = "asdf";
      public int A = 10;
      public byte B = 11;
    }

    [Test]
    public void CanMapFlatData() {
      var map = MappedBinarySerializer.Map.Format(typeof(SerializedFlatData));
      var node = MappedBinarySerializer.Map.Parse(map);
      var model = node as MappedBinarySerializer.Map.Model;
      Assert.IsNotNull(model);
      Assert.AreEqual(3, model.Count());
      var memberA = model.FirstOrDefault(m => m.Key == "A");
      Assert.IsNotNull(memberA);
      var memberAValue = memberA.Value as MappedBinarySerializer.Map.Content;
      Assert.AreEqual(typeof(int).FullName, memberAValue?.Value);
      var memberB = model.FirstOrDefault(m => m.Key == "B");
      Assert.IsNotNull(memberB);
      var memberBValue = memberB.Value as MappedBinarySerializer.Map.Content;
      Assert.AreEqual(typeof(byte).FullName, memberBValue?.Value);
      var memberC = model.FirstOrDefault(m => m.Key == "C");
      Assert.IsNotNull(memberC);
      var memberCValue = memberC.Value as MappedBinarySerializer.Map.Content;
      Assert.AreEqual(typeof(string).FullName, memberCValue?.Value);
    }

    [Test]
    public void CanWriteFlatData() {
      var map = MappedBinarySerializer.Map.Format(typeof(SerializedFlatData));
      var node = MappedBinarySerializer.Map.Parse(map);
      var model = node as MappedBinarySerializer.Map.Model;
      var build = MappedBinarySerializer.Writer.Compile(node, typeof(SerializedFlatData));

      using (var stream = new MemoryStream())
      using (var writer = new BinaryWriter(stream))
      using (var reader = new BinaryReader(stream)) {
        build.Write(new SerializedFlatData(), writer);
        stream.Position = 0;
        var values = model
            .ToDictionary(
                member => member.Key,
                member =>
                    member.Key == "A" ? reader.ReadInt32()
                    : member.Key == "B" ? reader.ReadByte()
                    : (object)reader.ReadString()
            );
        Assert.AreEqual(10, values["A"]);
        Assert.AreEqual(11, values["B"]);
        Assert.AreEqual("asdf", values["C"]);
      }
    }

    private class SerializedNestedData {
      public int[] A = new[] { 1, 2, 3 };

      public SerializedFlatData B = new SerializedFlatData();
    }

    [Test]
    public void CanMapNestedData() {
      var map = MappedBinarySerializer.Map.Format(typeof(SerializedNestedData));
      var node = MappedBinarySerializer.Map.Parse(map);
      var model = node as MappedBinarySerializer.Map.Model;
      Assert.IsNotNull(model);
      Assert.AreEqual(2, model.Count());
      var memberA = model.FirstOrDefault(m => m.Key == "A");
      Assert.IsNotNull(memberA);
      var collection = memberA.Value as MappedBinarySerializer.Map.Collection;
      var collectionType = collection?.Contents as MappedBinarySerializer.Map.Content;
      Assert.AreEqual(typeof(int).FullName, collectionType?.Value);
      var memberB = model.FirstOrDefault(m => m.Key == "B");
      Assert.IsNotNull(memberB);
      var child = memberB.Value as MappedBinarySerializer.Map.Model;
      var memberC = model.FirstOrDefault(m => m.Key == "C");
      Assert.IsNotNull(memberC);
    }

    [Test]
    public void CanWriteNestedData() {
      var map = MappedBinarySerializer.Map.Format(typeof(SerializedNestedData));
      var node = MappedBinarySerializer.Map.Parse(map);
      var model = node as MappedBinarySerializer.Map.Model;
      var build = MappedBinarySerializer.Writer.Compile(node, typeof(SerializedNestedData));

      using (var stream = new MemoryStream())
      using (var writer = new BinaryWriter(stream))
      using (var reader = new BinaryReader(stream)) {
        build.Write(new SerializedNestedData(), writer);
        stream.Position = 0;
        var values = model
            .ToDictionary(
                member => member.Key,
                member =>
                    (member.Value as MappedBinarySerializer.Map.Model)
                        ?.ToDictionary(
                            member2 => member2.Key,
                            member2 =>
                                member2.Key == "A" ? reader.ReadInt32()
                                : member2.Key == "B" ? reader.ReadByte()
                                : (object)reader.ReadString()
                        )
                    ?? new Dictionary<string, object> {
                                {"0", reader.ReadInt32()},
                                {"1", reader.ReadInt32()},
                                {"2", reader.ReadInt32()},
                                {"3", reader.ReadInt32()},
                    }
            );
        Assert.AreEqual(3, values["A"]["0"]);
        Assert.AreEqual(1, values["A"]["1"]);
        Assert.AreEqual(2, values["A"]["2"]);
        Assert.AreEqual(3, values["A"]["3"]);
        Assert.AreEqual(10, values["B"]["A"]);
        Assert.AreEqual(11, values["B"]["B"]);
        Assert.AreEqual("asdf", values["B"]["C"]);
      }
    }

    private class DeserializedFlatData {
      public int A;
      public byte B;
      public string C;
    }

    [Test]
    public void CanReadFlatData() {
      var map = MappedBinarySerializer.Map.Format(typeof(SerializedFlatData));
      var node = MappedBinarySerializer.Map.Parse(map);
      var model = node as MappedBinarySerializer.Map.Model;
      var buildWriter = MappedBinarySerializer.Writer.Compile(node, typeof(SerializedFlatData));
      var buildReader = MappedBinarySerializer.Reader.Compile(node, typeof(DeserializedFlatData));

      using (var stream = new MemoryStream())
      using (var reader = new BinaryReader(stream))
      using (var writer = new BinaryWriter(stream)) {
        buildWriter.Write(new SerializedFlatData(), writer);
        stream.Position = 0;
        var result = (DeserializedFlatData)buildReader.Read(reader);
        Assert.AreEqual(10, result.A);
        Assert.AreEqual(11, result.B);
        Assert.AreEqual("asdf", result.C);
      }
    }

    private class DeserializedNestedData {
      public int[] A;

      public DeserializedFlatData B;
    }

    [Test]
    public void CanReadNestedData() {
      var map = MappedBinarySerializer.Map.Format(typeof(SerializedNestedData));
      var node = MappedBinarySerializer.Map.Parse(map);
      var model = node as MappedBinarySerializer.Map.Model;
      var buildWriter = MappedBinarySerializer.Writer.Compile(node, typeof(SerializedNestedData));
      var buildReader = MappedBinarySerializer.Reader.Compile(node, typeof(DeserializedNestedData));

      using (var stream = new MemoryStream())
      using (var reader = new BinaryReader(stream))
      using (var writer = new BinaryWriter(stream)) {
        buildWriter.Write(new SerializedNestedData(), writer);
        stream.Position = 0;
        var result = (DeserializedNestedData)buildReader.Read(reader);
        Assert.AreEqual(3, result.A.Length);
        Assert.AreEqual(1, result.A[0]);
        Assert.AreEqual(2, result.A[1]);
        Assert.AreEqual(3, result.A[2]);
        Assert.AreEqual(10, result.B.A);
        Assert.AreEqual(11, result.B.B);
        Assert.AreEqual("asdf", result.B.C);
      }
    }

    private class MissingFlatData {
      public int A { get; set; }
      public string C { get; set; }
    }

    [Test]
    public void CanReadToMissingMembers() {
      var map = MappedBinarySerializer.Map.Format(typeof(SerializedFlatData));
      var buildWriter = MappedBinarySerializer.Writer.Compile(map, typeof(SerializedFlatData));
      var buildReader = MappedBinarySerializer.Reader.Compile(map, typeof(MissingFlatData));

      using (var stream = new MemoryStream())
      using (var reader = new BinaryReader(stream))
      using (var writer = new BinaryWriter(stream)) {
        buildWriter.Write(new SerializedFlatData(), writer);
        stream.Position = 0;
        var result = (MissingFlatData)buildReader.Read(reader);
        Assert.AreEqual(10, result.A);
        Assert.AreEqual("asdf", result.C);
      }
    }

    public class ExtraFlatData {
      public int A;
      public byte B;
      public string C;
      public long D;
    }

    [Test]
    public void CanReadToExtraMembers() {
      var map = MappedBinarySerializer.Map.Format(typeof(SerializedFlatData));
      var buildWriter = MappedBinarySerializer.Writer.Compile(map, typeof(SerializedFlatData));
      var buildReader = MappedBinarySerializer.Reader.Compile(map, typeof(ExtraFlatData));

      using (var stream = new MemoryStream())
      using (var reader = new BinaryReader(stream))
      using (var writer = new BinaryWriter(stream)) {
        buildWriter.Write(new SerializedFlatData(), writer);
        stream.Position = 0;
        var result = (ExtraFlatData)buildReader.Read(reader);
        Assert.AreEqual(10, result.A);
        Assert.AreEqual(11, result.B);
        Assert.AreEqual("asdf", result.C);
      }
    }

    private class MissingNestedData1 {
      public SerializedFlatData B = new SerializedFlatData();
    }


    [Test]
    public void CanReadToMissingNested() {
      var map = MappedBinarySerializer.Map.Format(typeof(SerializedNestedData));
      var buildWriter = MappedBinarySerializer.Writer.Compile(map, typeof(SerializedNestedData));
      var buildReader = MappedBinarySerializer.Reader.Compile(map, typeof(MissingNestedData1));

      using (var stream = new MemoryStream())
      using (var reader = new BinaryReader(stream))
      using (var writer = new BinaryWriter(stream)) {
        buildWriter.Write(new SerializedNestedData(), writer);
        stream.Position = 0;
        var result = (MissingNestedData1)buildReader.Read(reader);
        Assert.AreEqual(10, result.B.A);
        Assert.AreEqual(11, result.B.B);
        Assert.AreEqual("asdf", result.B.C);
      }
    }

    [Serializable]
    private class SpeedTest {
      public int A = 1;
      public int[] B = { 1, 2, 3 };
    }

    [Test]
    public void FasterReadThanMS() {
      const int TestCount = 10000;
      TimeSpan msTime = default;
      TimeSpan myTime = default;
      using (var stream = new MemoryStream()) {
        var msReader = new BinaryFormatter();
        msReader.Serialize(stream, new SpeedTest());
        stream.Position = 0;
        var jitRun = msReader.Deserialize(stream) as SpeedTest;
        var timer = Stopwatch.StartNew();
        for (var i = 0; i < TestCount; i++) {
          stream.Position = 0;
          msReader.Deserialize(stream);
        }
        timer.Stop();
        msTime = timer.Elapsed;
      }

      using (var stream = new MemoryStream()) {
        var map = MappedBinarySerializer.Map.Format(typeof(SpeedTest));
        MappedBinarySerializer.Writer.Compile(map, typeof(SpeedTest)).Write(new SpeedTest(), stream);
        var myReader = MappedBinarySerializer.Reader.Compile(map, typeof(SpeedTest));
        stream.Position = 0;
        var jitRun = myReader.Read(stream);
        var timer = Stopwatch.StartNew();
        for (var i = 0; i < TestCount; i++) {
          stream.Position = 0;
          myReader.Read(stream);
        }
        timer.Stop();
        myTime = timer.Elapsed;
      }

      TestContext.WriteLine($"We read {Math.Round((double)myTime.Milliseconds / msTime.Milliseconds * 100d, 2)}% faster than MS");
      Assert.IsTrue(myTime < msTime);
    }

    [Test]
    public void FasterWriteThanMS() {
      const int TestCount = 10000;
      TimeSpan msTime = default;
      TimeSpan myTime = default;
      using (var stream = new MemoryStream()) {
        var msWriter = new BinaryFormatter();
        var data = new SpeedTest();
        msWriter.Serialize(stream, data);
        stream.Position = 0;

        var timer = Stopwatch.StartNew();
        for (var i = 0; i < TestCount; i++) {
          stream.Position = 0;
          msWriter.Serialize(stream, data);
        }
        timer.Stop();
        msTime = timer.Elapsed;
      }

      using (var stream = new MemoryStream()) {
        var map = MappedBinarySerializer.Map.Format(typeof(SpeedTest));
        var myWriter = MappedBinarySerializer.Writer.Compile(map, typeof(SpeedTest));
        var data = new SpeedTest();
        myWriter.Write(data, stream);
        stream.Position = 0;

        var timer = Stopwatch.StartNew();
        for (var i = 0; i < TestCount; i++) {
          stream.Position = 0;
          myWriter.Write(data, stream);
        }
        timer.Stop();
        myTime = timer.Elapsed;
      }

      TestContext.WriteLine($"We write {Math.Round((double)myTime.Milliseconds / msTime.Milliseconds * 100d, 2)}% faster than MS");
      Assert.IsTrue(myTime < msTime);
    }
  }
}