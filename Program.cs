using System;
using System.IO;
using System.Threading.Tasks;
using System.Buffers;
using System.Runtime.InteropServices;
using System.Text; // Add this for Encoding
using System.Text.Json;
using System.Diagnostics;

// HIPAA-sensitive patient record
public record PatientRecord(string Name, int Age, string Diagnosis);

public static class Program
{
    private static void safe(){
      Stopwatch stopwatch = new Stopwatch();
      stopwatch.Start();
    
      const int MaxRecords = 1_000_000;
      PatientRecord[] records = new PatientRecord[MaxRecords];
      for(uint i = 0; i < MaxRecords; i++){    
          records[i] = new PatientRecord(
              Name: $"Patient_{i}",
              Age: Random.Shared.Next(0, 120),
              Diagnosis: i % 100 == 0 ? "Cancer" : "Flu"
          );
      }

      stopwatch.Stop();
      Console.WriteLine($"The time taken for safe data Geneartion is: {stopwatch.ElapsedMilliseconds} ms");
      for(uint i = 0; i < MaxRecords; i++){
          var record = records[i];
          var maskedName = record.Name.Length > 0 
              ? $"{record.Name[0]}****"
              : string.Empty;
          records[i] = record with { Name = maskedName };
      }          
      
      File.WriteAllText("masked_data.json",
          JsonSerializer.Serialize( records,
          new JsonSerializerOptions {WriteIndented = true }
          )
      );
      //stopwatch.Stop();
      //Console.WriteLine($"Time taken for the safe code: {stopwatch.ElapsedMilliseconds} ms");
    }
 
    private static unsafe void semiSafe(){
      Stopwatch stopwatch = new Stopwatch();
      stopwatch.Start();
    
      const int MaxRecords = 1_000_000;
      PatientRecord[] records = new PatientRecord[MaxRecords];
      for(uint i = 0; i < MaxRecords; i++){    
          records[i] = new PatientRecord(
              Name: $"Patient_{i}",
              Age: Random.Shared.Next(0, 120),
              Diagnosis: i % 100 == 0 ? "Cancer" : "Flu"
          );
      }
      Parallel.For(0, MaxRecords, i => {
          var record = records[i];
          var maskedName = record.Name.Length > 0 
              ? $"{record.Name[0]}****"
              : string.Empty;
          records[i] = record with { Name = maskedName };
      }          
      );
      File.WriteAllText("masked_dataSemiSafe.json",
          JsonSerializer.Serialize( records,
          new JsonSerializerOptions {WriteIndented = true }
          )
      );
      stopwatch.Stop();
      Console.WriteLine($"Time taken for the semi safe code: {stopwatch.ElapsedMilliseconds} ms");

    }

    public static unsafe void Main()
    {
      Stopwatch stopwatch = new Stopwatch();
      stopwatch.Start();
      const int MaxRecords = 1_000_000; //_ is used as an identifier for the programmer

      //using means that the thing will be deleted itself
      //The shared means it's a singleton or whatever, rent means we are trying to allocate MaxRecords of memory
        using var memoryOwner = MemoryPool<PatientRecord>.Shared.Rent(MaxRecords);
        //Since Span is stack memory "window", that means that it can only be accessed by a funciotn and the pointer to the memory "window" gets deleted once out of scope
        // 1. Generate synthetic data (parallel)
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
        stopwatch.Stop();
        Console.WriteLine($"The time taken for unsafe data Geneartion is: {stopwatch.ElapsedMilliseconds} ms");

        // 2. Process with zero-copy and SIMD-ready spans
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
            File.WriteAllBytes("masked_data.bin",
                new ReadOnlySpan<byte>(outputBuffer, MaxRecords * 128).ToArray());
        }
        finally
        {
            NativeMemory.Free(outputBuffer);
        }
    //stopwatch.Stop();
    //Console.WriteLine($"Time taken for the unsafe code: {stopwatch.ElapsedMilliseconds} ms");
    safe();
    semiSafe();
    }
}
