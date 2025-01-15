using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace FMSolution
{
    public static class FMCoreTools
    {
        public static void NativeArrayCopyTo(NativeArray<byte> nativeArray, ref byte[] referenceByteArray)
        {
            if (referenceByteArray.Length != nativeArray.Length) referenceByteArray = new byte[nativeArray.Length];
            nativeArray.CopyTo(referenceByteArray);
        }

        public static byte[] IntPtrToByteArray(IntPtr ptr, int length)
        {
            byte[] byteData = new byte[length];
            Marshal.Copy(ptr, byteData, 0, length);
            return byteData;
        }

        /// <summary>
        /// convert float to Int16 space
        /// </summary>
        public static Int16 FloatToInt16(float inputFloat)
        {
            return Convert.ToInt16(inputFloat * Int16.MaxValue);
            //inputFloat *= 32767;
            //if (inputFloat < -32768) inputFloat = -32768;
            //if (inputFloat > 32767) inputFloat = 32767;
            //return Convert.ToInt16(inputFloat);
        }

        //public static float[] IntPtrToFloatArray(IntPtr ptr, int length)
        //{
        //    if (length % sizeof(float) != 0) throw new ArgumentException("Length must be a multiple of the size of float.");
        //    float[] floatArray = new float[length / sizeof(float)];
        //    Marshal.Copy(ptr, floatArray, 0, floatArray.Length);
        //    return floatArray;
        //}
        public static float[] IntPtrToFloatArray(IntPtr ptr, int length)
        {
            if (length % sizeof(float) != 0) throw new ArgumentException("Length must be a multiple of the size of float.");
            int floatCount = length / sizeof(float);
            float[] floatArray = new float[floatCount];
            Marshal.Copy(ptr, floatArray, 0, floatCount);
            return floatArray;
        }
        public static byte[] FloatArrayToByteArray(float[] floatArray)
        {
            if (floatArray == null) throw new ArgumentNullException(nameof(floatArray));
            byte[] byteArray = new byte[floatArray.Length * sizeof(float)];
            Buffer.BlockCopy(floatArray, 0, byteArray, 0, byteArray.Length);
            return byteArray;
        }

        /// <summary>
        /// convert byte[] to float[]
        /// </summary>
        public static float[] ToFloatArray(byte[] byteArray)
        {
            int len = byteArray.Length / 4;
            float[] floatArray = new float[len];
            Buffer.BlockCopy(byteArray, 0, floatArray, 0, byteArray.Length);
            return floatArray;
        }

        /// <summary>
        /// convert Int16 byte[] to float[]
        /// </summary>
        //public static float[] Int16ToNormalizedFloatArray(byte[] byteArray)
        //{
        //    int len = byteArray.Length / 2;
        //    float[] floatArray = new float[len];

        //    ReadOnlySpan<Int16> shortSpan = MemoryMarshal.Cast<byte, Int16>(byteArray);
        //    for (int i = 0; i < len; i++)
        //    {
        //        floatArray[i] = shortSpan[i] / 32767f;
        //    }

        //    return floatArray;
        //}

        public static float[] Int16ToNormalizedFloatArray(byte[] byteArray)
        {
            int len = byteArray.Length / 2;
            float[] floatArray = new float[len];
            for (int i = 0; i < byteArray.Length; i += 2)
            {
                floatArray[i / 2] = ((float)BitConverter.ToInt16(byteArray, i)) / 32767f;
            }
            return floatArray;
        }

    /// <summary>
    /// convert Int16 byte[] to float[]
    /// </summary>
    //public static float[] Int16ToNormalizedFloatArray(byte[] byteArray)
    //{
    //    int len = byteArray.Length / 2;
    //    float[] floatArray = new float[len];
    //    for (int i = 0; i < byteArray.Length; i += 2)
    //    {
    //        floatArray[i / 2] = ((float)BitConverter.ToInt16(byteArray, i)) / 32767f;
    //    }
    //    return floatArray;
    //}

    /// <summary>
    /// convert float to Int16 space
    /// </summary>
    public static Int16 NormalizeFloatToInt16(float inputFloat)
        {
            if (inputFloat < -1f) inputFloat = -1f;
            else if (inputFloat > 1f) inputFloat = 1f;
            return (Int16)(inputFloat * 32767); // 32767 is Int16.MaxValue
        }

        /// <summary>
        /// convert normalised Float[] to PCM16 Int16[]
        /// </summary>
        public static Int16[] NormalizeFloatArrayToPCM16(float[] InputNormalizedSamples)
        {
            Int16[] pcm16Samples = new Int16[InputNormalizedSamples.Length];
            for (int i = 0; i < InputNormalizedSamples.Length; i++) pcm16Samples[i] = NormalizeFloatToInt16(InputNormalizedSamples[i]);
            return pcm16Samples;
        }

        /// <summary>
        /// Downsample PCM32 float[]
        /// </summary>
        public static float[] PCM32Downsample(float[] inputBuffer, int inputSampleRate, int inputChannels, int outputSampleRate, int outputChannels, ref float?[] referenceLastSamples)
        {
            if (inputSampleRate == outputSampleRate && inputChannels == outputChannels) return inputBuffer;
            if (inputBuffer.Length == 0) return new float[0];
            if (referenceLastSamples == null) referenceLastSamples = new float?[inputChannels];

            float sampleRateRatio = (float)outputSampleRate / inputSampleRate;
            int outputLength = (int)(inputBuffer.Length / inputChannels * sampleRateRatio);
            float[] outputBuffer = new float[outputLength * outputChannels]; // Interleaved output buffer

            for (int i = 0; i < outputLength; i++)
            {
                float fractionalIndex = i / sampleRateRatio;
                int index = (int)fractionalIndex;
                float fraction = fractionalIndex - index;

                for (int c = 0; c < outputChannels; c++)
                {
                    // Map output channel to input channel, assuming simple downmixing (like averaging) if needed
                    int inputChannel = c % inputChannels;

                    // Calculate the current and next sample indices for the specific input channel
                    int currentSampleIndex = index * inputChannels + inputChannel;
                    int nextSampleIndex = currentSampleIndex + inputChannels;

                    if (currentSampleIndex < inputBuffer.Length)
                    {
                        float currentSample = inputBuffer[currentSampleIndex];
                        float nextSample = (nextSampleIndex < inputBuffer.Length) ? inputBuffer[nextSampleIndex] : currentSample;

                        if (index == 0 && referenceLastSamples[inputChannel].HasValue)
                        {
                            currentSample = referenceLastSamples[inputChannel].Value;
                        }

                        // Interleaved output
                        outputBuffer[i * outputChannels + c] = currentSample * (1 - fraction) + nextSample * fraction;
                    }
                    else
                    {
                        outputBuffer[i * outputChannels + c] = inputBuffer[currentSampleIndex]; // Fallback if current index is out of bounds
                    }
                }
            }

            // Store the last sample for each input channel
            for (int c = 0; c < inputChannels; c++)
            {
                referenceLastSamples[c] = inputBuffer[inputBuffer.Length - inputChannels + c];
            }

            return outputBuffer;
        }



        /// <summary>
        /// compare two int values, return true if they are similar referring to the sizeThreshold
        /// </summary>
        public static bool CheckSimilarSize(int inputByteLength1, int inputByteLength2, int sizeThreshold)
        {
            float diff = Mathf.Abs(inputByteLength1 - inputByteLength2);
            return diff < sizeThreshold;
        }

        public static class AsyncTask
        {
            /// <summary>
            /// Async Task delay
            /// </summary>
            public static async Task Yield()
            {
#if UNITY_WSA && !UNITY_EDITOR
                //found overhead issue on HoloLens 2, adding extra 1ms delay should help
                await Task.Delay(1);
#endif
                await Task.Yield();
            }

            private static TimeSpan timeSpanOneMS = TimeSpan.FromMilliseconds(1f);
            /// <summary>
            /// Async Task delay
            /// </summary>
            public static async Task Delay(TimeSpan inputTimeSpan)
            {
                try
                {
#if UNITY_WSA && !UNITY_EDITOR
                    //found overhead issue on HoloLens 2, force minimal 1ms delay
                    inputTimeSpan = inputTimeSpan < timeSpanOneMS ? timeSpanOneMS : inputTimeSpan;
#endif

#if !UNITY_WEBGL || UNITY_EDITOR //FM Notes: stripped in build bug, with #if UNITY_WEBGL || !UNITY_EDITOR
                    // For NON-WebGL platform
                    await Task.Delay(inputTimeSpan);
#else
                    // Unity 2021 do NOT support Task.Delay() in WebGL
                    float startTime = Time.realtimeSinceStartup;
                    float delay = inputTimeSpan.Seconds;

                    while (Time.realtimeSinceStartup - startTime < delay)
                    {
                        // Wait for the delay time to pass
                        await Task.Yield();
                    }
#endif
                }
                catch { }
            }

            /// <summary>
            /// Async Task delay
            /// </summary>
            public static async Task Delay(TimeSpan inputTimeSpan, CancellationToken ct)
            {
                try
                {
#if UNITY_WSA && !UNITY_EDITOR
                    //found overhead issue on HoloLens 2, force minimal 1ms delay
                    inputTimeSpan = inputTimeSpan < timeSpanOneMS ? timeSpanOneMS : inputTimeSpan;
#endif

#if !UNITY_WEBGL || UNITY_EDITOR //FM Notes: stripped in build bug, with #if UNITY_WEBGL || !UNITY_EDITOR
                    // For NON-WebGL platform
                    await Task.Delay(inputTimeSpan, ct);
#else
                    // Unity 2021 do NOT support Task.Delay() in WebGL
                    float startTime = Time.realtimeSinceStartup;
                    float delay = inputTimeSpan.Seconds;

                    while (Time.realtimeSinceStartup - startTime < delay && !ct.IsCancellationRequested)
                    {
                        // Wait for the delay time to pass
                        await Task.Yield();
                    }
#endif
                }
                catch { }
            }

            /// <summary>
            /// Async Task delay
            /// </summary>
            public static async Task Delay(int inputMS)
            {
                try
                {
#if UNITY_WSA && !UNITY_EDITOR
                    //found overhead issue on HoloLens 2, force minimal 1ms delay
                    inputMS = Mathf.Clamp(inputMS, 1, int.MaxValue);
#endif

#if !UNITY_WEBGL || UNITY_EDITOR //FM Notes: stripped in build bug, with #if UNITY_WEBGL || !UNITY_EDITOR
                    // For NON-WebGL platform
                    await Task.Delay(inputMS);
#else
                    // Unity 2021 do NOT support Task.Delay() in WebGL
                    float startTime = Time.realtimeSinceStartup;
                    float delay = (float)inputMS / 1000f;

                    while (Time.realtimeSinceStartup - startTime < delay)
                    {
                        // Wait for the delay time to pass
                        await Task.Yield();
                    }
#endif
                }
                catch { }
            }
            /// <summary>
            /// Async Task delay
            /// </summary>
            public static async Task Delay(int inputMS, CancellationToken ct)
            {
                try
                {
#if UNITY_WSA && !UNITY_EDITOR
                    //found overhead issue on HoloLens 2, force minimal 1ms delay
                    inputMS = Mathf.Clamp(inputMS, 1, int.MaxValue);
#endif

#if !UNITY_WEBGL || UNITY_EDITOR //FM Notes: stripped in build bug, with #if UNITY_WEBGL || !UNITY_EDITOR
                    // For NON-WebGL platform
                    await Task.Delay(inputMS, ct);
#else
                    // Unity 2021 do NOT support Task.Delay() in WebGL
                    float startTime = Time.realtimeSinceStartup;
                    float delay = (float)inputMS / 1000f;

                    while (Time.realtimeSinceStartup - startTime < delay && !ct.IsCancellationRequested)
                    {
                        // Wait for the delay time to pass
                        await Task.Yield();
                    }
#endif
                }
                catch { }
            }

            public static IEnumerator WaitForEndOfFrameCOR(TaskCompletionSource<bool> tcs)
            {
                yield return new WaitForEndOfFrame();
                tcs.TrySetResult(true);
            }
        }


        //WebGL Specific
#if UNITY_WEBGL || !UNITY_EDITOR
        private static int dictionaryID = 0;
        private static int getDictionaryID { get { dictionaryID++; dictionaryID %= UInt16.MaxValue - 1; return dictionaryID; } }
        private static Dictionary<int, TaskCompletionSource<byte[]>> dictionary_callbackTaskCompletionSource = new Dictionary<int, TaskCompletionSource<byte[]>>();

        public static async Task<byte[]> GetBufferWebGL(Action<int, Action<int, int, IntPtr>> action, CancellationToken ct)
        {
            try
            {
                int _callbackID = getDictionaryID;
                TaskCompletionSource<byte[]> _taskCompletionSource = new TaskCompletionSource<byte[]>();
                dictionary_callbackTaskCompletionSource.Add(_callbackID, _taskCompletionSource);

                action.Invoke(_callbackID, callback_buffer);

                byte[] byteData = await _taskCompletionSource.Task;
                dictionary_callbackTaskCompletionSource.Remove(_callbackID);
                return byteData;
            }
            catch (OperationCanceledException) { }
            return null;
        }

        public static async Task<byte[]> GetBufferWebGL(int label, byte[] inputByteArray, int inputByteArraySize, Action<int, byte[], int, int, Action<int, int, IntPtr>> action, CancellationToken ct)
        {
            try
            {
                int _callbackID = getDictionaryID;
                TaskCompletionSource<byte[]> _taskCompletionSource = new TaskCompletionSource<byte[]>();
                dictionary_callbackTaskCompletionSource.Add(_callbackID, _taskCompletionSource);

                action.Invoke(label, inputByteArray, inputByteArraySize, _callbackID, callback_buffer);

                byte[] byteData = await _taskCompletionSource.Task;
                dictionary_callbackTaskCompletionSource.Remove(_callbackID);
                return byteData;
            }
            catch (OperationCanceledException) { }
            return null;
        }

        public static async Task<byte[]> GetBufferWebGL(int label, Action<int, int, Action<int, int, IntPtr>> action, CancellationToken ct)
        {
            try
            {
                int _callbackID = getDictionaryID;
                TaskCompletionSource<byte[]> _taskCompletionSource = new TaskCompletionSource<byte[]>();
                dictionary_callbackTaskCompletionSource.Add(_callbackID, _taskCompletionSource);

                action.Invoke(label, _callbackID, callback_buffer);

                byte[] byteData = await _taskCompletionSource.Task;
                dictionary_callbackTaskCompletionSource.Remove(_callbackID);
                return byteData;
            }
            catch (OperationCanceledException) { }
            return null;
        }

        [AOT.MonoPInvokeCallback(typeof(Action<int, int, IntPtr>))]
        private static void callback_buffer(int dictionaryID, int length, IntPtr ptr)
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
#endif
    }
}