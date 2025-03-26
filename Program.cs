using System;
using System.IO;
using System.Threading.Tasks;
using System.Buffers;
using System.Runtime.InteropServices;
using System.Text; // Add this for Encoding
using System.Text.Json;
using System.Diagnostics;
using System.Runtime.Intrinsics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.X86;

// HIPAA-sensitive patient record
public record PatientRecord(string Name, int Age, string Diagnosis);

public static class Program
{
//    private static string
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void highPerfJsonifier(ReadOnlySpan<PatientRecord> records){
      var buffer = new ArrayBufferWriter<byte>(1024 * 1024);
      using var writer = new Utf8JsonWriter(buffer);

      writer.WriteStartArray(); //the first bracket [
      foreach(var record in records){
        if(record is null){
          writer.WriteNullValue();
          continue;
        }
        //Console.WriteLine($"The name is: {record.Name}");
        writer.WriteStartObject(); //The first curled brace that encapsulates the one object
        writer.WriteString("Name", record.Name);
        writer.WriteNumber("Age", record.Age);
        writer.WriteString("Diagnosis", record.Diagnosis);
        writer.WriteEndObject();
      }

      writer.WriteEndArray(); //The last bracked ]
      writer.Flush();

      File.WriteAllBytes("masked_data_highperf.json", buffer.WrittenSpan.ToArray());
    }

    private static void safe(){
      Stopwatch stopwatch = new Stopwatch();
      const int MaxRecords = 1_000_000;
      PatientRecord[] records = new PatientRecord[MaxRecords];
      for(uint i = 0; i < MaxRecords; i++){    
          records[i] = new PatientRecord(
              Name: $"Patient_{i}",
              Age: Random.Shared.Next(0, 120),
              Diagnosis: i % 100 == 0 ? "Cancer" : "Flu"
          );
      }
      stopwatch.Start();
      for(uint i = 0; i < MaxRecords; i++){
          var record = records[i];
          var maskedName = record.Name.Length > 0 
              ? $"{record.Name[0]}****"
              : string.Empty;
          records[i] = record with { Name = maskedName };
      }          
      
      stopwatch.Stop();
      Console.WriteLine($"The time taken for data anonymization in safe: {stopwatch.ElapsedMilliseconds} ms");

      File.WriteAllText("masked_data.json",
          JsonSerializer.Serialize( records,
          new JsonSerializerOptions {WriteIndented = false }
          )
      );
      //stopwatch.Stop();
      //Console.WriteLine($"Time taken for the safe code: {stopwatch.ElapsedMilliseconds} ms");
    }
 
    private static unsafe void semiSafe(){
      Stopwatch stopwatch = new Stopwatch();
      const int MaxRecords = 1_000_000;
      PatientRecord[] records = new PatientRecord[MaxRecords];
      Parallel.For(0, MaxRecords, i => {    
          records[i] = new PatientRecord(
              Name: $"Patient_{i}",
              Age: Random.Shared.Next(0, 120),
              Diagnosis: i % 100 == 0 ? "Cancer" : "Flu");
      }
      );
      stopwatch.Start();
      Parallel.For(0, MaxRecords, i => {
          var record = records[i];
          var maskedName = record.Name.Length > 0 
              ? $"{record.Name[0]}****"
              : string.Empty;
          records[i] = record with { Name = maskedName };
      }          
      );
      stopwatch.Stop();
      Console.WriteLine($"Time taken for semisafe data anonymization : {stopwatch.ElapsedMilliseconds} ms");

      File.WriteAllText("masked_dataSemiSafe.json",
          JsonSerializer.Serialize( records,
          new JsonSerializerOptions {WriteIndented = true}
          )
      );

    }

    public static unsafe void Main()
    {
      const int MaxRecords = 1_000_000; //_ is used as an identifier for the programmer

      Stopwatch stopwatch = new Stopwatch();
      //Stopwatch stopwatch2 = new Stopwatch();
      //using means that the thing will be deleted itself
      //The shared means it's a singleton or whatever, rent means we are trying to allocate MaxRecords of memory
        using var memoryOwner = MemoryPool<PatientRecord>.Shared.Rent(MaxRecords);
        //Since Span is stack memory "window", that means that it can only be accessed by a funciotn and the pointer to the memory "window" gets deleted once out of scope
        Parallel.For(0, MaxRecords, i => 
        {
            //Since span has near zero overhead, we don't need to wory about making new var records for each threads as it's just passing a reference to the memory block andnot creating anythign or allocating anything
            var records = memoryOwner.Memory.Span;
            records[i] = new PatientRecord(
                Name: $"Patient_{i}",
                Age: Random.Shared.Next(0, 120),
                Diagnosis: i % 100 == 0 ? "Cancer" : "Flu"
            );
        });
        stopwatch.Start();
        //ReadOnlySpan<PatientRecord> recordSpan = memoryOwner.Memory.Span;
        //stopwatch2.Start();
        //highPerfJsonifier(recordSpan);
        //stopwatch2.Stop();
        //Console.WriteLine($"The time taken for unsafe own serializer is: {stopwatch2.ElapsedMilliseconds} ms");
        
        //stopwatch.Stop();
        //Console.WriteLine($"The time taken for unsafe data Geneartion is: {stopwatch.ElapsedMilliseconds} ms");

        var outputBuffer = (byte*)NativeMemory.Alloc((nuint)(MaxRecords * 128));
        try
        {
            Parallel.For(0, MaxRecords, i =>
            {            
                var records = memoryOwner.Memory.Span;
                var record = records[i];
                var maskedName = record.Name.Length > 0 
                    ? $"{record.Name[0]}****" 
                    : string.Empty;
                
                byte[] stringBytes = Encoding.UTF8.GetBytes(maskedName);
                Span<byte> bytes = stringBytes.AsSpan();
                
                unsafe 
                {
                    fixed (byte* src = bytes)
                    {
                        Buffer.MemoryCopy(
                            source: src,
                            destination: outputBuffer + (i * 128),
                            destinationSizeInBytes: 128,
                            sourceBytesToCopy: Math.Min(bytes.Length, 128)); // Prevent overflow
                    }
                }
            });
            stopwatch.Stop();
            Console.WriteLine($"The time taken to generate the data for unsafe is: {stopwatch.ElapsedMilliseconds} ms");

            File.WriteAllBytes("masked_data.bin",
                new ReadOnlySpan<byte>(outputBuffer, MaxRecords * 128).ToArray());

        }
        finally
        {
            NativeMemory.Free(outputBuffer);
        }
    safe();
    semiSafe();
    }
}
