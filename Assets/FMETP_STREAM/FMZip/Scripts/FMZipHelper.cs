using System;
using System.Runtime;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using ICSharpCode.SharpZipLib.GZip;
using System.Collections;
using ICSharpCode.SharpZipLib.Zip;
using ICSharpCode.SharpZipLib.Core;
using UnityEngine;
using System.Runtime.InteropServices;

namespace FMSolution.FMZip
{
    [Serializable]
    public enum FMGZipEncodeMode
    {
        ///<summary>Disabled GZip</summary>
        None = 0,
        ///<summary>Apply GZip in current Thread(Sync)</summary>
        LowLatency = 1,
        ///<summary>Apply GZip in current Thread(Async)</summary>
        Balance = 2,
        ///<summary>Apply GZip in other Thread(Async)</summary>
        HighPerformance = 3
    }
    [Serializable]
    public enum FMGZipDecodeMode
    {
        ///<summary>Apply GZip in current Thread(Sync)</summary>
        LowLatency = 1,
        ///<summary>Apply GZip in current Thread(Async)</summary>
        Balance = 2,
        ///<summary>Apply GZip in other Thread(Async)</summary>
        HighPerformance = 3
    }
    [Serializable]
    public class FMZipCreator
    {
        private float progress = 0f;
        public float GetProgress() { return progress; }
        private bool ContinueRunning = true;
        public void Stop() { ContinueRunning = false; }

        private Action<FMZipCreator, float> onProgressUpdatedCallback;
        public FMZipCreator(string inputSourceDirectory, string outputZipName, Action<FMZipCreator, float> inputCallback)
        {
            progress = 0f;
            uptoFileCount = 0;
            totalFileCount = FolderContentsCount(inputSourceDirectory);
            onProgressUpdatedCallback = inputCallback;
            Task.Run(() => { Create(inputSourceDirectory, outputZipName); });
        }

        public void Create(string inputSourceDirectory, string outputZipName)
        {
            if (string.IsNullOrEmpty(outputZipName)) outputZipName = inputSourceDirectory + ".zip";

            FastZipEvents events = new FastZipEvents();
            events.ProcessFile = updateProcessFile;

            FastZip fastZip = new FastZip(events);

            UnityEngine.Debug.Log(inputSourceDirectory + ": " + outputZipName);
            fastZip.CreateZip(outputZipName, inputSourceDirectory, true, null);
        }

        private int uptoFileCount;
        private int totalFileCount;
        private int FolderContentsCount(string path)
        {
            int result = Directory.GetFiles(path).Length;
            string[] subFolders = Directory.GetDirectories(path);
            foreach (string subFolder in subFolders) result += FolderContentsCount(subFolder);
            return result;
        }

        private void updateProcessFile(object sender, ScanEventArgs args)
        {
            uptoFileCount++;
            progress = (float)uptoFileCount / (float)totalFileCount;

            string fileName = args.Name;
            // To terminate the process, set args.ContinueRunning = false
            args.ContinueRunning = ContinueRunning;

            if (args.ContinueRunning == false)
            {
                progress = 0f;
                uptoFileCount = 0;
                totalFileCount = 0;
            }

            onProgressUpdatedCallback(this, progress);
        }
    }

    public static class FMZipHelper
    {
        /// <summary> FastZip Create Sync, return zipped file path as string </summary>
        public static string CreateZip(string inputSourceDirectory, string outputZipName = null)
        {
            if (string.IsNullOrEmpty(outputZipName)) outputZipName += inputSourceDirectory + ".zip";
            FastZip fastZip = new FastZip();
            fastZip.CreateZip(outputZipName, inputSourceDirectory, true, null);
            return outputZipName;
        }
        /// <summary> FastZip Create Async </summary>
        public static void CreateZipAsync(string inputSourceDirectory, string outputZipName = null)
        {
            Task.Run(() => { CreateZip(inputSourceDirectory, outputZipName); });
        }

        /// <summary> FastZip Create Async with callback </summary>
        public static void CreateZipAsync(string inputSourceDirectory, string outputZipName, Action<FMZipCreator, float> onProgressUpdatedCallback)
        {
            new FMZipCreator(inputSourceDirectory, outputZipName, onProgressUpdatedCallback);
        }
        /// <summary> FastZip Create Async with callback </summary>
        public static void CreateZipAsync(string inputSourceDirectory, Action<FMZipCreator, float> onProgressUpdatedCallback)
        {
            CreateZipAsync(inputSourceDirectory, "", onProgressUpdatedCallback);
        }

        /// <summary> FastZip Extract Sync, return output Directory as string </summary>
        public static string ExtractZip(string inputZipName, string outputDirectory = null)
        {
            if (string.IsNullOrEmpty(outputDirectory)) outputDirectory = new FileInfo(inputZipName).Directory.FullName;
            FastZip fastZip = new FastZip();
            string fileFilter = null;
            fastZip.ExtractZip(inputZipName, outputDirectory, fileFilter);
            return outputDirectory;
        }
        /// <summary> FastZip Extract Async </summary>
        public static void ExtractZipAsync(string inputZipName, string outputDirectory = null)
        {
            Task.Run(() => { ExtractZip(inputZipName, outputDirectory); });
        }
        /// <summary> FastZip Extract Async with callback </summary>
        public static void ExtractZipAsync(string inputZipName, string outputDirectory, Action<string> onCompletedCallback)
        {
            Task.Run(() => { onCompletedCallback(ExtractZip(inputZipName, outputDirectory)); });
        }
        /// <summary> FastZip Extract Async with callback </summary>
        public static void ExtractZipAsync(string inputZipName, Action<string> onCompletedCallback)
        {
            ExtractZipAsync(inputZipName, "", onCompletedCallback);
        }

        public static byte[] ReadAllBytes(this GZipInputStream stream)
        {
            const int bufferSize = 4096;
            using (MemoryStream ms = new MemoryStream())
            {
                byte[] buffer = new byte[bufferSize];
                int count;
                try
                {
                    while ((count = stream.Read(buffer, 0, buffer.Length)) != 0) ms.Write(buffer, 0, count);
                }
                catch { return null; }
                return ms.ToArray();
            }
        }
        public static async Task<byte[]> ReadAllBytesAsync(this GZipInputStream stream, CancellationToken ct)
        {
            const int bufferSize = 4096;
            using (MemoryStream ms = new MemoryStream())
            {
                byte[] buffer = new byte[bufferSize];
                int count;
                try
                {
                    //while ((count = await stream.ReadAsync(buffer, 0, buffer.Length, ct)) != 0)
                    //async read is too slow, use sync read instead
                    while ((count = stream.Read(buffer, 0, buffer.Length)) != 0)
                    {
                        await ms.WriteAsync(buffer, 0, count, ct);
                        if (ct.IsCancellationRequested) return null;
                    }
                }
                catch { return null; }
                return ms.ToArray();
            }
        }

        public static string unzipText(byte[] bytes)
        {
            if (bytes == null) return null;
            using (Stream memInput = new MemoryStream(bytes))
            using (GZipInputStream zipInput = new GZipInputStream(memInput))
            using (StreamReader reader = new StreamReader(zipInput))
            {
                string text = reader.ReadToEnd();
                return text;
            }
        }
        public static async Task<string> unzipTextAsync(byte[] bytes)
        {
            if (bytes == null) return null;
            try
            {
                using (Stream memInput = new MemoryStream(bytes))
                using (GZipInputStream zipInput = new GZipInputStream(memInput))
                using (StreamReader reader = new StreamReader(zipInput))
                {
                    var _task = await reader.ReadToEndAsync();
                    return _task;
                }
            }
            catch (OperationCanceledException) { return null; }
        }

        public static byte[] FMZipBytes(this byte[] RawBytes)
        {
            if (RawBytes == null) return null;
            using (Stream memOutput = new MemoryStream())
            using (GZipOutputStream zipOut = new GZipOutputStream(memOutput))
            using (BinaryWriter writer = new BinaryWriter(zipOut))
            {
                writer.Write(RawBytes);
                writer.Flush();
                zipOut.Finish();

                byte[] bytes = new byte[memOutput.Length];
                memOutput.Seek(0, SeekOrigin.Begin);
                memOutput.Read(bytes, 0, bytes.Length);
                return bytes;
            }
        }

        public static async Task<byte[]> FMZipBytesAsync(this byte[] RawBytes, CancellationToken ct)
        {
            if (RawBytes == null) return null;
            try
            {
                using (MemoryStream memOutput = new MemoryStream())
                using (GZipOutputStream zipOut = new GZipOutputStream(memOutput))
                {
                    await zipOut.WriteAsync(RawBytes, 0, RawBytes.Length, ct);
                    await zipOut.FlushAsync(ct);
                    await zipOut.FinishAsync(ct);

                    byte[] bytes = new byte[memOutput.Length];
                    memOutput.Seek(0, SeekOrigin.Begin);
                    await memOutput.ReadAsync(bytes, 0, bytes.Length);
                    return bytes;
                }
            }
            catch (OperationCanceledException) { return null; }
        }

        public static byte[] FMUnzipBytes(this byte[] ZipBytes)
        {
            if (ZipBytes == null) return null;

            using (Stream memInput = new MemoryStream(ZipBytes))
            using (GZipInputStream zipInput = new GZipInputStream(memInput))
            {
                byte[] bytes = ReadAllBytes(zipInput);
                return bytes;
            }
        }

        public static async Task<byte[]> FMUnzipBytesAsync(this byte[] ZipBytes, CancellationToken ct)
        {
            if (ZipBytes == null) return null;
            try
            {
                using (Stream memInput = new MemoryStream(ZipBytes))
                using (GZipInputStream zipInput = new GZipInputStream(memInput))
                {
                    return await ReadAllBytesAsync(zipInput, ct);
                }
            }
            catch (OperationCanceledException) { return null; }
        }

        //Async functions
        /// <summary>
        /// FMZip method(
        /// example script:
        /// byte[] zippedBytes = await FMZipHelper.FMZippedByteAsync(inputBytes, ct); //running in the current thread
        /// byte[] zippedBytes = await FMZipHelper.FMZippedByteAsync(inputBytes, ct, false); //multi-thread solution
        /// </summary>
        public static async Task<byte[]> FMZippedByteAsync(byte[] inputByte, CancellationToken ct, FMGZipEncodeMode inputGZipMode = FMGZipEncodeMode.LowLatency)
        {
            try
            {
                switch (inputGZipMode)
                {
                    case FMGZipEncodeMode.None: return null;
                    case FMGZipEncodeMode.LowLatency: return FMZipBytes(inputByte);
                    case FMGZipEncodeMode.Balance: return await FMZipBytesAsync(inputByte, ct);
                    case FMGZipEncodeMode.HighPerformance: return await Task.Run(async () => { return await FMZipBytesAsync(inputByte, ct); });
                }
            }
            catch (OperationCanceledException) { }
            return null;
        }
        //Async functions
        /// <summary>
        /// FMZip method(Async)
        /// example script:
        /// byte[] unzippedBytes = await FMZipHelper.FMUnzippedByteAsync(inputByteData, ct); //running in the current thread
        /// byte[] unzippedBytes = await FMZipHelper.FMUnzippedByteAsync(inputBytes, ct, false); //multi-thread solution
        /// </summary>
        public static async Task<byte[]> FMUnzippedByteAsync(byte[] inputByte, CancellationToken ct, FMGZipDecodeMode inputGZipMode = FMGZipDecodeMode.LowLatency)
        {
            try
            {
                switch (inputGZipMode)
                {
                    case FMGZipDecodeMode.LowLatency: return inputByte.FMUnzipBytes();
                    case FMGZipDecodeMode.Balance: return await inputByte.FMUnzipBytesAsync(ct);
                    case FMGZipDecodeMode.HighPerformance: return await Task.Run(async () => { try { return await inputByte.FMUnzipBytesAsync(ct); } catch { return null; } });
                }
            }
            catch (OperationCanceledException) { }
            return null;
        }

        //Coroutine functions
        /// <summary>
        /// FMZip method(Coroutine) yield return value from coroutine
        /// yield return FMCoreTools.RunCOR<byte[]>(YourCOR, (output) => YourVariableForOutput = output);
        /// </summary>
        public static IEnumerator RunCOR<T>(IEnumerator target, Action<T> output)
        {
            object result = null;
            while (target.MoveNext())
            {
                result = target.Current;
                yield return result;
            }
            output((T)result);
        }

        /// <summary>
        /// FMZip method(Coroutine) yield return value from coroutine
        /// example script:
        /// yield return FMCoreTools.RunCOR<byte[]>(FMCoreTools.FMZippedByteCOR(dataByte), (output) => dataByte = output);
        /// </summary>
        public static IEnumerator FMZippedByteCOR(byte[] inputByte, FMGZipEncodeMode inputGZipMode = FMGZipEncodeMode.LowLatency)
        {
            byte[] _zippedByte = new byte[0];
            if (inputGZipMode == FMGZipEncodeMode.LowLatency)
            {
                try { _zippedByte = inputByte.FMZipBytes(); } catch { }
            }
            else if (inputGZipMode == FMGZipEncodeMode.HighPerformance)
            {
                //need to clone a buffer for multi-threading
                byte[] _bufferByte = new byte[inputByte.Length];
                Buffer.BlockCopy(inputByte, 0, _bufferByte, 0, inputByte.Length);

                bool _taskCompleted = false;
                Task.Run(() => { try { _zippedByte = _bufferByte.FMZipBytes(); } catch { } _taskCompleted = true; });
                while (!_taskCompleted) yield return null;
            }
            yield return _zippedByte;
        }

        /// <summary>
        /// FMZip method(Coroutine) yield return value from coroutine
        /// example script:
        /// yield return FMCoreTools.RunCOR<byte[]>(FMCoreTools.FMUnzippedByteCOR(inputByteData), (output) => inputByteData = output);
        /// </summary>
        public static IEnumerator FMUnzippedByteCOR(byte[] inputByte, FMGZipEncodeMode inputGZipMode = FMGZipEncodeMode.LowLatency)
        {
            byte[] _unzippedByte = new byte[0];
            if (inputGZipMode == FMGZipEncodeMode.LowLatency)
            {
                try { _unzippedByte = inputByte.FMUnzipBytes(); } catch { }
            }
            else if (inputGZipMode == FMGZipEncodeMode.HighPerformance)
            {
                //need to clone a buffer for multi-threading
                byte[] _bufferByte = new byte[inputByte.Length];
                Buffer.BlockCopy(inputByte, 0, _bufferByte, 0, inputByte.Length);

                bool _taskCompleted = false;
                Task.Run(() => { try { _unzippedByte = _bufferByte.FMUnzipBytes(); } catch { } _taskCompleted = true; });
                while (!_taskCompleted) yield return null;
            }
            yield return _unzippedByte;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void FMCoreTools_WebGLAddScript_2021_2(string _innerHTML, string _src, string _id = "", string _onloadFunction = "");
        private static string initialisedJavascript = null;
        private static void initialiseMicrophoneJavascript_GZip()
        {
            if (string.IsNullOrEmpty(initialisedJavascript))
            {
                //string javascript = ""
                //    + $@"var startTime = 0;" + "\n"
                //    + $@"var audioCtx = new AudioContext();" + "\n"
                //    ;

                string javascript = gunzipMinJS;

                initialisedJavascript = javascript;

                //string src = "http" + (sslEnabled ? "s" : "") + "://" + IP;
                //if (portRequired) src += (port != 0 ? ":" + port.ToString() : "");
                //string srcGZip = src + "/lib/gunzip.min.js";
                //FMWebSocket_AddGZip(srcGZip);
                //addedGZip = true;

                FMCoreTools_WebGLAddScript_2021_2(javascript, "");
                Debug.Log(javascript);
            }
        }

        //check https://docs.unity3d.com/Packages/com.unity.industrial.forma@3.0/manual/forma-js-api-devGuide.html
        [DllImport("__Internal")] private static extern void FMUnzipBytesJSAsync(byte[] array, int size, int callbackID, Action<int, int, IntPtr> callback);
        private static int dictionaryID = 0;
        private static int getDictionaryID { get { dictionaryID++; dictionaryID %= int.MaxValue - 1; return dictionaryID; } }
        private static Dictionary<int, TaskCompletionSource<byte[]>> dictionary_callbackTaskCompletionSource = new Dictionary<int, TaskCompletionSource<byte[]>>();

        public static async Task<byte[]> FMUnzippedByteAsyncWeb(byte[] inputByte, CancellationToken ct, FMGZipDecodeMode inputGZipMode = FMGZipDecodeMode.LowLatency)
        {
            try
            {
                int _callbackID = getDictionaryID;
                TaskCompletionSource<byte[]> _taskCompletionSource = new TaskCompletionSource<byte[]>();
                dictionary_callbackTaskCompletionSource.Add(_callbackID, _taskCompletionSource);

                initialiseMicrophoneJavascript_GZip();//make sure adding GZip min library or full script
                FMUnzipBytesJSAsync(inputByte, inputByte.Length, _callbackID, callback_decompression);

                byte[] byteData = await _taskCompletionSource.Task;
                dictionary_callbackTaskCompletionSource.Remove(_callbackID);
                return byteData;
            }
            catch (OperationCanceledException) { }
            return null;
        }

        [AOT.MonoPInvokeCallback(typeof(Action<int, int, IntPtr>))]
        private static void callback_decompression(int dictionaryID, int length, IntPtr ptr)
        {
            byte[] byteData = FMCoreTools.IntPtrToByteArray(ptr, length);
            if (dictionary_callbackTaskCompletionSource.TryGetValue(dictionaryID, out TaskCompletionSource<byte[]> _taskCompletionSource))
            {
                _taskCompletionSource.SetResult(byteData);
            }
            else
            {
                _taskCompletionSource.SetResult(new byte[0]);
            }
        }

        private static string gunzipMinJS = $@"/** @license zlib.js 2012 - imaya [ https://github.com/imaya/zlib.js ] The MIT License */(function() {{'use strict';function n(e){{throw e;}}var p=void 0,aa=this;function t(e,b){{var d=e.split("".""),c=aa;!(d[0]in c)&&c.execScript&&c.execScript(""var ""+d[0]);for(var a;d.length&&(a=d.shift());)!d.length&&b!==p?c[a]=b:c=c[a]?c[a]:c[a]={{}}}};var x=""undefined""!==typeof Uint8Array&&""undefined""!==typeof Uint16Array&&""undefined""!==typeof Uint32Array&&""undefined""!==typeof DataView;new (x?Uint8Array:Array)(256);var y;for(y=0;256>y;++y)for(var A=y,ba=7,A=A>>>1;A;A>>>=1)--ba;function B(e,b,d){{var c,a=""number""===typeof b?b:b=0,f=""number""===typeof d?d:e.length;c=-1;for(a=f&7;a--;++b)c=c>>>8^C[(c^e[b])&255];for(a=f>>3;a--;b+=8)c=c>>>8^C[(c^e[b])&255],c=c>>>8^C[(c^e[b+1])&255],c=c>>>8^C[(c^e[b+2])&255],c=c>>>8^C[(c^e[b+3])&255],c=c>>>8^C[(c^e[b+4])&255],c=c>>>8^C[(c^e[b+5])&255],c=c>>>8^C[(c^e[b+6])&255],c=c>>>8^C[(c^e[b+7])&255];return(c^4294967295)>>>0}}
var D=[0,1996959894,3993919788,2567524794,124634137,1886057615,3915621685,2657392035,249268274,2044508324,3772115230,2547177864,162941995,2125561021,3887607047,2428444049,498536548,1789927666,4089016648,2227061214,450548861,1843258603,4107580753,2211677639,325883990,1684777152,4251122042,2321926636,335633487,1661365465,4195302755,2366115317,997073096,1281953886,3579855332,2724688242,1006888145,1258607687,3524101629,2768942443,901097722,1119000684,3686517206,2898065728,853044451,1172266101,3705015759,
2882616665,651767980,1373503546,3369554304,3218104598,565507253,1454621731,3485111705,3099436303,671266974,1594198024,3322730930,2970347812,795835527,1483230225,3244367275,3060149565,1994146192,31158534,2563907772,4023717930,1907459465,112637215,2680153253,3904427059,2013776290,251722036,2517215374,3775830040,2137656763,141376813,2439277719,3865271297,1802195444,476864866,2238001368,4066508878,1812370925,453092731,2181625025,4111451223,1706088902,314042704,2344532202,4240017532,1658658271,366619977,
2362670323,4224994405,1303535960,984961486,2747007092,3569037538,1256170817,1037604311,2765210733,3554079995,1131014506,879679996,2909243462,3663771856,1141124467,855842277,2852801631,3708648649,1342533948,654459306,3188396048,3373015174,1466479909,544179635,3110523913,3462522015,1591671054,702138776,2966460450,3352799412,1504918807,783551873,3082640443,3233442989,3988292384,2596254646,62317068,1957810842,3939845945,2647816111,81470997,1943803523,3814918930,2489596804,225274430,2053790376,3826175755,
2466906013,167816743,2097651377,4027552580,2265490386,503444072,1762050814,4150417245,2154129355,426522225,1852507879,4275313526,2312317920,282753626,1742555852,4189708143,2394877945,397917763,1622183637,3604390888,2714866558,953729732,1340076626,3518719985,2797360999,1068828381,1219638859,3624741850,2936675148,906185462,1090812512,3747672003,2825379669,829329135,1181335161,3412177804,3160834842,628085408,1382605366,3423369109,3138078467,570562233,1426400815,3317316542,2998733608,733239954,1555261956,
3268935591,3050360625,752459403,1541320221,2607071920,3965973030,1969922972,40735498,2617837225,3943577151,1913087877,83908371,2512341634,3803740692,2075208622,213261112,2463272603,3855990285,2094854071,198958881,2262029012,4057260610,1759359992,534414190,2176718541,4139329115,1873836001,414664567,2282248934,4279200368,1711684554,285281116,2405801727,4167216745,1634467795,376229701,2685067896,3608007406,1308918612,956543938,2808555105,3495958263,1231636301,1047427035,2932959818,3654703836,1088359270,
936918E3,2847714899,3736837829,1202900863,817233897,3183342108,3401237130,1404277552,615818150,3134207493,3453421203,1423857449,601450431,3009837614,3294710456,1567103746,711928724,3020668471,3272380065,1510334235,755167117],C=x?new Uint32Array(D):D;function E(){{}}E.prototype.getName=function(){{return this.name}};E.prototype.getData=function(){{return this.data}};E.prototype.G=function(){{return this.H}};function G(e){{var b=e.length,d=0,c=Number.POSITIVE_INFINITY,a,f,k,l,m,r,q,g,h,v;for(g=0;g<b;++g)e[g]>d&&(d=e[g]),e[g]<c&&(c=e[g]);a=1<<d;f=new (x?Uint32Array:Array)(a);k=1;l=0;for(m=2;k<=d;){{for(g=0;g<b;++g)if(e[g]===k){{r=0;q=l;for(h=0;h<k;++h)r=r<<1|q&1,q>>=1;v=k<<16|g;for(h=r;h<a;h+=m)f[h]=v;++l}}++k;l<<=1;m<<=1}}return[f,d,c]}};var J=[],K;for(K=0;288>K;K++)switch(!0){{case 143>=K:J.push([K+48,8]);break;case 255>=K:J.push([K-144+400,9]);break;case 279>=K:J.push([K-256+0,7]);break;case 287>=K:J.push([K-280+192,8]);break;default:n(""invalid literal: ""+K)}}
var ca=function(){{function e(a){{switch(!0){{case 3===a:return[257,a-3,0];case 4===a:return[258,a-4,0];case 5===a:return[259,a-5,0];case 6===a:return[260,a-6,0];case 7===a:return[261,a-7,0];case 8===a:return[262,a-8,0];case 9===a:return[263,a-9,0];case 10===a:return[264,a-10,0];case 12>=a:return[265,a-11,1];case 14>=a:return[266,a-13,1];case 16>=a:return[267,a-15,1];case 18>=a:return[268,a-17,1];case 22>=a:return[269,a-19,2];case 26>=a:return[270,a-23,2];case 30>=a:return[271,a-27,2];case 34>=a:return[272,
a-31,2];case 42>=a:return[273,a-35,3];case 50>=a:return[274,a-43,3];case 58>=a:return[275,a-51,3];case 66>=a:return[276,a-59,3];case 82>=a:return[277,a-67,4];case 98>=a:return[278,a-83,4];case 114>=a:return[279,a-99,4];case 130>=a:return[280,a-115,4];case 162>=a:return[281,a-131,5];case 194>=a:return[282,a-163,5];case 226>=a:return[283,a-195,5];case 257>=a:return[284,a-227,5];case 258===a:return[285,a-258,0];default:n(""invalid length: ""+a)}}}}var b=[],d,c;for(d=3;258>=d;d++)c=e(d),b[d]=c[2]<<24|c[1]<<
16|c[0];return b}}();x&&new Uint32Array(ca);function L(e,b){{this.i=[];this.j=32768;this.d=this.f=this.c=this.n=0;this.input=x?new Uint8Array(e):e;this.o=!1;this.k=M;this.w=!1;if(b||!(b={{}}))b.index&&(this.c=b.index),b.bufferSize&&(this.j=b.bufferSize),b.bufferType&&(this.k=b.bufferType),b.resize&&(this.w=b.resize);switch(this.k){{case N:this.a=32768;this.b=new (x?Uint8Array:Array)(32768+this.j+258);break;case M:this.a=0;this.b=new (x?Uint8Array:Array)(this.j);this.e=this.D;this.q=this.A;this.l=this.C;break;default:n(Error(""invalid inflate mode""))}}}}
var N=0,M=1;
L.prototype.g=function(){{for(;!this.o;){{var e=P(this,3);e&1&&(this.o=!0);e>>>=1;switch(e){{case 0:var b=this.input,d=this.c,c=this.b,a=this.a,f=b.length,k=p,l=p,m=c.length,r=p;this.d=this.f=0;d+1>=f&&n(Error(""invalid uncompressed block header: LEN""));k=b[d++]|b[d++]<<8;d+1>=f&&n(Error(""invalid uncompressed block header: NLEN""));l=b[d++]|b[d++]<<8;k===~l&&n(Error(""invalid uncompressed block header: length verify""));d+k>b.length&&n(Error(""input buffer is broken""));switch(this.k){{case N:for(;a+k>c.length;){{r=
m-a;k-=r;if(x)c.set(b.subarray(d,d+r),a),a+=r,d+=r;else for(;r--;)c[a++]=b[d++];this.a=a;c=this.e();a=this.a}}break;case M:for(;a+k>c.length;)c=this.e({{t:2}});break;default:n(Error(""invalid inflate mode""))}}if(x)c.set(b.subarray(d,d+k),a),a+=k,d+=k;else for(;k--;)c[a++]=b[d++];this.c=d;this.a=a;this.b=c;break;case 1:this.l(da,ea);break;case 2:for(var q=P(this,5)+257,g=P(this,5)+1,h=P(this,4)+4,v=new (x?Uint8Array:Array)(Q.length),s=p,F=p,H=p,w=p,z=p,O=p,I=p,u=p,Z=p,u=0;u<h;++u)v[Q[u]]=P(this,3);if(!x){{u=
h;for(h=v.length;u<h;++u)v[Q[u]]=0}}s=G(v);w=new (x?Uint8Array:Array)(q+g);u=0;for(Z=q+g;u<Z;)switch(z=R(this,s),z){{case 16:for(I=3+P(this,2);I--;)w[u++]=O;break;case 17:for(I=3+P(this,3);I--;)w[u++]=0;O=0;break;case 18:for(I=11+P(this,7);I--;)w[u++]=0;O=0;break;default:O=w[u++]=z}}F=x?G(w.subarray(0,q)):G(w.slice(0,q));H=x?G(w.subarray(q)):G(w.slice(q));this.l(F,H);break;default:n(Error(""unknown BTYPE: ""+e))}}}}return this.q()}};
var S=[16,17,18,0,8,7,9,6,10,5,11,4,12,3,13,2,14,1,15],Q=x?new Uint16Array(S):S,fa=[3,4,5,6,7,8,9,10,11,13,15,17,19,23,27,31,35,43,51,59,67,83,99,115,131,163,195,227,258,258,258],ga=x?new Uint16Array(fa):fa,ha=[0,0,0,0,0,0,0,0,1,1,1,1,2,2,2,2,3,3,3,3,4,4,4,4,5,5,5,5,0,0,0],T=x?new Uint8Array(ha):ha,ia=[1,2,3,4,5,7,9,13,17,25,33,49,65,97,129,193,257,385,513,769,1025,1537,2049,3073,4097,6145,8193,12289,16385,24577],ja=x?new Uint16Array(ia):ia,ka=[0,0,0,0,1,1,2,2,3,3,4,4,5,5,6,6,7,7,8,8,9,9,10,10,11,
11,12,12,13,13],U=x?new Uint8Array(ka):ka,V=new (x?Uint8Array:Array)(288),W,la;W=0;for(la=V.length;W<la;++W)V[W]=143>=W?8:255>=W?9:279>=W?7:8;var da=G(V),X=new (x?Uint8Array:Array)(30),Y,ma;Y=0;for(ma=X.length;Y<ma;++Y)X[Y]=5;var ea=G(X);function P(e,b){{for(var d=e.f,c=e.d,a=e.input,f=e.c,k=a.length,l;c<b;)f>=k&&n(Error(""input buffer is broken"")),d|=a[f++]<<c,c+=8;l=d&(1<<b)-1;e.f=d>>>b;e.d=c-b;e.c=f;return l}}
function R(e,b){{for(var d=e.f,c=e.d,a=e.input,f=e.c,k=a.length,l=b[0],m=b[1],r,q;c<m&&!(f>=k);)d|=a[f++]<<c,c+=8;r=l[d&(1<<m)-1];q=r>>>16;q>c&&n(Error(""invalid code length: ""+q));e.f=d>>q;e.d=c-q;e.c=f;return r&65535}}
L.prototype.l=function(e,b){{var d=this.b,c=this.a;this.r=e;for(var a=d.length-258,f,k,l,m;256!==(f=R(this,e));)if(256>f)c>=a&&(this.a=c,d=this.e(),c=this.a),d[c++]=f;else{{k=f-257;m=ga[k];0<T[k]&&(m+=P(this,T[k]));f=R(this,b);l=ja[f];0<U[f]&&(l+=P(this,U[f]));c>=a&&(this.a=c,d=this.e(),c=this.a);for(;m--;)d[c]=d[c++-l]}}for(;8<=this.d;)this.d-=8,this.c--;this.a=c}};
L.prototype.C=function(e,b){{var d=this.b,c=this.a;this.r=e;for(var a=d.length,f,k,l,m;256!==(f=R(this,e));)if(256>f)c>=a&&(d=this.e(),a=d.length),d[c++]=f;else{{k=f-257;m=ga[k];0<T[k]&&(m+=P(this,T[k]));f=R(this,b);l=ja[f];0<U[f]&&(l+=P(this,U[f]));c+m>a&&(d=this.e(),a=d.length);for(;m--;)d[c]=d[c++-l]}}for(;8<=this.d;)this.d-=8,this.c--;this.a=c}};
L.prototype.e=function(){{var e=new (x?Uint8Array:Array)(this.a-32768),b=this.a-32768,d,c,a=this.b;if(x)e.set(a.subarray(32768,e.length));else{{d=0;for(c=e.length;d<c;++d)e[d]=a[d+32768]}}this.i.push(e);this.n+=e.length;if(x)a.set(a.subarray(b,b+32768));else for(d=0;32768>d;++d)a[d]=a[b+d];this.a=32768;return a}};
L.prototype.D=function(e){{var b,d=this.input.length/this.c+1|0,c,a,f,k=this.input,l=this.b;e&&(""number""===typeof e.t&&(d=e.t),""number""===typeof e.z&&(d+=e.z));2>d?(c=(k.length-this.c)/this.r[2],f=258*(c/2)|0,a=f<l.length?l.length+f:l.length<<1):a=l.length*d;x?(b=new Uint8Array(a),b.set(l)):b=l;return this.b=b}};
L.prototype.q=function(){{var e=0,b=this.b,d=this.i,c,a=new (x?Uint8Array:Array)(this.n+(this.a-32768)),f,k,l,m;if(0===d.length)return x?this.b.subarray(32768,this.a):this.b.slice(32768,this.a);f=0;for(k=d.length;f<k;++f){{c=d[f];l=0;for(m=c.length;l<m;++l)a[e++]=c[l]}}f=32768;for(k=this.a;f<k;++f)a[e++]=b[f];this.i=[];return this.buffer=a}};
L.prototype.A=function(){{var e,b=this.a;x?this.w?(e=new Uint8Array(b),e.set(this.b.subarray(0,b))):e=this.b.subarray(0,b):(this.b.length>b&&(this.b.length=b),e=this.b);return this.buffer=e}};function $(e){{this.input=e;this.c=0;this.m=[];this.s=!1}}$.prototype.F=function(){{this.s||this.g();return this.m.slice()}};
$.prototype.g=function(){{for(var e=this.input.length;this.c<e;){{var b=new E,d=p,c=p,a=p,f=p,k=p,l=p,m=p,r=p,q=p,g=this.input,h=this.c;b.u=g[h++];b.v=g[h++];(31!==b.u||139!==b.v)&&n(Error(""invalid file signature:""+b.u+"",""+b.v));b.p=g[h++];switch(b.p){{case 8:break;default:n(Error(""unknown compression method: ""+b.p))}}b.h=g[h++];r=g[h++]|g[h++]<<8|g[h++]<<16|g[h++]<<24;b.H=new Date(1E3*r);b.N=g[h++];b.M=g[h++];0<(b.h&4)&&(b.I=g[h++]|g[h++]<<8,h+=b.I);if(0<(b.h&8)){{m=[];for(l=0;0<(k=g[h++]);)m[l++]=String.fromCharCode(k);
b.name=m.join("""")}}if(0<(b.h&16)){{m=[];for(l=0;0<(k=g[h++]);)m[l++]=String.fromCharCode(k);b.J=m.join("""")}}0<(b.h&2)&&(b.B=B(g,0,h)&65535,b.B!==(g[h++]|g[h++]<<8)&&n(Error(""invalid header crc16"")));d=g[g.length-4]|g[g.length-3]<<8|g[g.length-2]<<16|g[g.length-1]<<24;g.length-h-4-4<512*d&&(f=d);c=new L(g,{{index:h,bufferSize:f}});b.data=a=c.g();h=c.c;b.K=q=(g[h++]|g[h++]<<8|g[h++]<<16|g[h++]<<24)>>>0;B(a,p,p)!==q&&n(Error(""invalid CRC-32 checksum: 0x""+B(a,p,p).toString(16)+"" / 0x""+q.toString(16)));b.L=
d=(g[h++]|g[h++]<<8|g[h++]<<16|g[h++]<<24)>>>0;(a.length&4294967295)!==d&&n(Error(""invalid input size: ""+(a.length&4294967295)+"" / ""+d));this.m.push(b);this.c=h}}this.s=!0;var v=this.m,s,F,H=0,w=0,z;s=0;for(F=v.length;s<F;++s)w+=v[s].data.length;if(x){{z=new Uint8Array(w);for(s=0;s<F;++s)z.set(v[s].data,H),H+=v[s].data.length}}else{{z=[];for(s=0;s<F;++s)z[s]=v[s].data;z=Array.prototype.concat.apply([],z)}}return z}};t(""Zlib.Gunzip"",$);t(""Zlib.Gunzip.prototype.decompress"",$.prototype.g);t(""Zlib.Gunzip.prototype.getMembers"",$.prototype.F);t(""Zlib.GunzipMember"",E);t(""Zlib.GunzipMember.prototype.getName"",E.prototype.getName);t(""Zlib.GunzipMember.prototype.getData"",E.prototype.getData);t(""Zlib.GunzipMember.prototype.getMtime"",E.prototype.G);}}).call(this);
";

#endif
    }
}
