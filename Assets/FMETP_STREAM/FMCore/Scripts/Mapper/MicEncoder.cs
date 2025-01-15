using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using FMSolution.FMZip;

namespace FMSolution.FMETP
{
    [Serializable]
    public enum MicDeviceMode { Default, TargetDevice }
    [AddComponentMenu("FMETP/Mapper/MicEncoder")]
    public class MicEncoder : MonoBehaviour
    {
        #region EditorProps
        public bool EditorShowCapture = true;
        public bool EditorShowAudioInfo = true;
        public bool EditorShowEncoded = true;
        public bool EditorShowPairing = true;
        #endregion

        public AudioOutputFormat OutputFormat = AudioOutputFormat.FMPCM16;
        public bool OutputAsChunks = true;
        public UnityEventByteArray OnDataByteReadyEvent = new UnityEventByteArray();
        public UnityEventByteArray OnRawPCM16ReadyEvent = new UnityEventByteArray();


        private Queue<byte[]> AppendQueueSendByteFMPCM16 = new Queue<byte[]>();
        private Queue<byte[]> AppendQueueSendBytePCM16 = new Queue<byte[]>();

        private int MicSampleRate = 16000;
        private int MicChannels = 1;
        //[Header("[Capture In-Game Sound]")]
        public bool StreamGameSound = true;
        public int OutputSampleRate = 16000;
        [Range(1, 8)]
        public int OutputChannels = 1;


        //----------------------------------------------
        private AudioSource AudioMic;
        private ConcurrentQueue<float> AudioFloats = new ConcurrentQueue<float>();
        private float?[] referenceLastSamplePCM = null;  // Store the last sample from the previous chunk

        public MicDeviceMode DeviceMode = MicDeviceMode.Default;
        public string TargetDeviceName = "MacBook Pro FMMicrophone";
        string CurrentDeviceName = null;

        [TextArea]
        public string DetectedDevices;

        ////[Header("[Capture In-Game Sound]")]
        //public bool StreamGameSound = true;
        //public int OutputSampleRate = 11025;
        //[Range(1, 8)]
        //public int OutputChannels = 1;

        private int CurrentAudioTimeSample = 0;
        private int LastAudioTimeSample = 0;
        //----------------------------------------------

        [Range(1f, 60f)]
        public float StreamFPS = 20f;
        private float interval = 0.05f;

        public bool UseHalf = true;
        public FMGZipEncodeMode GZipMode = FMGZipEncodeMode.None;

        //[Header("Pair Encoder & Decoder")]
        [Range(1000, UInt16.MaxValue)] public UInt16 label = 2001;
        private UInt16 dataID = 0;
        private UInt16 maxID = 1024;
        private int maximumChunkSize = Int32.MaxValue - 1024;
        public int OutputChunkSize = 1436;//8096; //32768
        public int GetChunkSize() { return OutputAsChunks ? OutputChunkSize : maximumChunkSize; }

        private float next = 0f;
        private bool stop = false;
        private byte[] dataByte;
        private byte[] dataByteTemp;

        public int dataLength;

#if !UNITY_WEBGL || UNITY_EDITOR

        // Use this for initialization
        private void Start() { StartAll(); }
        private async void CaptureMicAsync()
        {
            if (AudioMic == null) AudioMic = GetComponent<AudioSource>();
            if (AudioMic == null) AudioMic = gameObject.AddComponent<AudioSource>();

            //Check Target Device
            DetectedDevices = "";
            string[] MicNames = FMMicrophone.devices;
            foreach (string _name in MicNames) DetectedDevices += _name + "\n";
            if (DeviceMode == MicDeviceMode.TargetDevice)
            {
                bool IsCorrectName = false;
                for (int i = 0; i < MicNames.Length; i++)
                {
                    if (MicNames[i] == TargetDeviceName)
                    {
                        IsCorrectName = true;
                        break;
                    }
                }
                if (!IsCorrectName) TargetDeviceName = null;
            }
            //Check Target Device

            CurrentDeviceName = DeviceMode == MicDeviceMode.Default ? MicNames[0] : TargetDeviceName;
            AudioMic.clip = FMMicrophone.Start(CurrentDeviceName, true, 1, OutputSampleRate);
            AudioMic.loop = true;
            while (FMMicrophone.GetPosition(CurrentDeviceName) <= 0) await FMCoreTools.AsyncTask.Yield();
            Debug.Log(CurrentDeviceName + " Start Mic(pos): " + FMMicrophone.GetPosition(CurrentDeviceName));
            AudioMic.Play();

            AudioMic.volume = 0f;

            MicSampleRate = AudioMic.clip.frequency;
            MicChannels = AudioMic.clip.channels;
            if (OutputSampleRate > MicSampleRate) OutputSampleRate = MicSampleRate;
            if (OutputChannels > MicChannels) OutputChannels = MicChannels;

            while (!stoppedOrCancelled())
            {
                AddMicData();
                await FMCoreTools.AsyncTask.Delay(1);
                await FMCoreTools.AsyncTask.Yield();
            }
        }

        private void AddMicData()
        {
            LastAudioTimeSample = CurrentAudioTimeSample;
            CurrentAudioTimeSample = FMMicrophone.GetPosition(CurrentDeviceName);

            if (CurrentAudioTimeSample != LastAudioTimeSample)
            {
                float[] samples = new float[AudioMic.clip.samples];
                AudioMic.clip.GetData(samples, 0);

                if (CurrentAudioTimeSample > LastAudioTimeSample)
                {
                    for (int i = LastAudioTimeSample; i < CurrentAudioTimeSample; i++) AudioFloats.Enqueue(samples[i]);
                }
                else if (CurrentAudioTimeSample < LastAudioTimeSample)
                {
                    for (int i = LastAudioTimeSample; i < samples.Length; i++) AudioFloats.Enqueue(samples[i]);
                    for (int i = 0; i < CurrentAudioTimeSample; i++) AudioFloats.Enqueue(samples[i]);
                }
            }
        }

#else

#endif
        private void OnEnable() { StartAll(); }
        private void OnDisable() { StopAll(); }
        private void OnApplicationQuit() { StopAll(); }
        private void OnDestroy() { StopAll(); }

        private CancellationTokenSource cancellationTokenSource_global;
        private bool stoppedOrCancelled() { return stop || cancellationTokenSource_global.IsCancellationRequested; }
        private void InitAsyncTokenSource() { cancellationTokenSource_global = new CancellationTokenSource(); }
        private void StopAllAsync()
        {
            if (cancellationTokenSource_global != null)
            {
                if (!cancellationTokenSource_global.IsCancellationRequested) cancellationTokenSource_global.Cancel();
            }
        }

        public void OnReceivedRawPCMData(float[] floatArray)
        {
            foreach (float _float in floatArray) AudioFloats.Enqueue(_float);
        }
        int webMicID = -1;
        private bool initialised = false;
        private void StartAll()
        {
            if (initialised) return;
            initialised = true;

            stop = false;
            InitAsyncTokenSource();

#if !UNITY_WEBGL || UNITY_EDITOR
            CaptureMicAsync();
#else
            webMicID = FMMicrophone.StartFMMicrophoneWebGL(OutputFormat, OutputSampleRate, OutputChannels, OnReceivedRawPCMData);
#endif
            SenderAsync();
        }

        private void StopAll()
        {
            initialised = false;
            stop = true;
            StopAllAsync();

#if !UNITY_WEBGL || UNITY_EDITOR
            if (AudioMic != null)
            {
                AudioMic.Stop();
                FMMicrophone.End(CurrentDeviceName);
            }
#else
            FMMicrophone.StopWebGL(webMicID);
#endif
            AppendQueueSendByteFMPCM16.Clear();
            AppendQueueSendBytePCM16.Clear();
        }

        private async void InvokeEventsCheckerAsync()
        {
            while (!stoppedOrCancelled())
            {
                await FMCoreTools.AsyncTask.Delay(1);
                await FMCoreTools.AsyncTask.Yield();
                while (AppendQueueSendByteFMPCM16.Count > 0) OnDataByteReadyEvent.Invoke(AppendQueueSendByteFMPCM16.Dequeue());
                while (AppendQueueSendBytePCM16.Count > 0) OnRawPCM16ReadyEvent.Invoke(AppendQueueSendBytePCM16.Dequeue());
            }
        }

        private async void CombinePacket(byte[] inputData)
        {
            bool _autoSkipGZip = inputData.Length < 1024 || GZipMode == FMGZipEncodeMode.None || OutputFormat == AudioOutputFormat.PCM16;
            dataByteTemp = _autoSkipGZip ? inputData.ToArray() : await FMZipHelper.FMZippedByteAsync(inputData, cancellationTokenSource_global.Token, GZipMode);
            //==================getting byte data==================
            int _length = dataByteTemp.Length;
            dataLength = _length;

            int _offset = 0;
            byte[] _meta_label = BitConverter.GetBytes(label);
            byte[] _meta_id = BitConverter.GetBytes(dataID);
            byte[] _meta_length = BitConverter.GetBytes(_length);

            int _metaByteLength = 14;
            int _chunkSize = GetChunkSize();
            if (OutputFormat == AudioOutputFormat.FMPCM16) _chunkSize -= _metaByteLength;
            int _chunkCount = Mathf.CeilToInt((float)_length / (float)_chunkSize);
            for (int i = 1; i <= _chunkCount; i++)
            {
                int dataByteLength = (i == _chunkCount) ? (_length % _chunkSize) : (_chunkSize);
                if (OutputFormat == AudioOutputFormat.FMPCM16)
                {
                    byte[] _meta_offset = BitConverter.GetBytes(_offset);
                    byte[] SendByte = new byte[dataByteLength + _metaByteLength];

                    Buffer.BlockCopy(_meta_label, 0, SendByte, 0, 2);
                    Buffer.BlockCopy(_meta_id, 0, SendByte, 2, 2);
                    Buffer.BlockCopy(_meta_length, 0, SendByte, 4, 4);
                    Buffer.BlockCopy(_meta_offset, 0, SendByte, 8, 4);
                    SendByte[12] = (byte)(_autoSkipGZip ? 0 : 1);
                    SendByte[13] = (byte)OutputFormat;

                    Buffer.BlockCopy(dataByteTemp, _offset, SendByte, 14, dataByteLength);
                    if (OutputFormat == AudioOutputFormat.FMPCM16) { AppendQueueSendByteFMPCM16.Enqueue(SendByte); }
                }
                else if (OutputFormat == AudioOutputFormat.PCM16)
                {
                    byte[] SendByte = new byte[dataByteLength];
                    Buffer.BlockCopy(dataByteTemp, _offset, SendByte, 0, dataByteLength);
                    if (!BitConverter.IsLittleEndian) Array.Reverse(SendByte);

                    if (OutputFormat == AudioOutputFormat.PCM16) { AppendQueueSendBytePCM16.Enqueue(SendByte); }
                }

                _offset += _chunkSize;
            }

            dataID++;
            if (dataID > maxID) dataID = 0;
        }
        private async void SenderAsync()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            GZipMode = FMGZipEncodeMode.None;//WebGL doesn't support GZip
#endif
            InvokeEventsCheckerAsync();
            while (!stoppedOrCancelled())
            {
                if (Time.realtimeSinceStartup > next)
                {
                    interval = 1f / StreamFPS;
                    next = Time.realtimeSinceStartup + interval;

                    while (!stoppedOrCancelled() && AudioFloats.IsEmpty)
                    {
                        await FMCoreTools.AsyncTask.Delay(1);
                        await FMCoreTools.AsyncTask.Yield();
                    }

                    //==================getting byte data==================
                    byte[] _samplerateByte = BitConverter.GetBytes(OutputSampleRate);
                    byte[] _channelsByte = BitConverter.GetBytes(OutputChannels);

                    float[] _tempAudioFloats = AudioFloats.ToArray();
#if UNITY_WEBGL && !UNITY_EDITOR
#else
                    _tempAudioFloats = FMCoreTools.PCM32Downsample(_tempAudioFloats, MicSampleRate, MicChannels, OutputSampleRate, OutputChannels, ref referenceLastSamplePCM);
#endif
                    AudioFloats = new ConcurrentQueue<float>();

                    int _pcm16ByteCount = _tempAudioFloats.Length * sizeof(Int16);//16bit -> 2 bytes
                    switch (OutputFormat)
                    {
                        case AudioOutputFormat.FMPCM16:
                            dataByte = new byte[_pcm16ByteCount + _samplerateByte.Length + _channelsByte.Length];
                            Buffer.BlockCopy(_samplerateByte, 0, dataByte, 0, _samplerateByte.Length);
                            Buffer.BlockCopy(_channelsByte, 0, dataByte, 4, _channelsByte.Length);
                            Buffer.BlockCopy(FMCoreTools.NormalizeFloatArrayToPCM16(_tempAudioFloats), 0, dataByte, 8, _pcm16ByteCount);

                            CombinePacket(dataByte);
                            break;
                        case AudioOutputFormat.PCM16:
                            dataByte = new byte[_pcm16ByteCount];
                            Buffer.BlockCopy(FMCoreTools.NormalizeFloatArrayToPCM16(_tempAudioFloats), 0, dataByte, 0, _pcm16ByteCount);

                            CombinePacket(dataByte);
                            break;
                    }
                }
                await FMCoreTools.AsyncTask.Delay(1);
                await FMCoreTools.AsyncTask.Yield();
            }
        }
    }
}