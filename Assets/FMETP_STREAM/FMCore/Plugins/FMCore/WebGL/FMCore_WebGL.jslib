﻿mergeInto(LibraryManager.library, {
    //Unity 2021_2 or before: Pointer_stringify(_src);
    //Unity 2021_2 or above: UTF8ToString(_src);
    FMStopMicStream: function()
    {
      if (MicRecorder != null) MicRecorder.disconnect();
      MicRecording = false;
    },

    FMCaptureMicStart_2021_2: function(_callbackID, _callback)
    {
      // var gameobject = UTF8ToString(_gameobject);

        console.log("JS: FMCaptureMicStart");

        // var OutputFormat = "FMPCM16";//"FMPCM16","PCM16"
        // var OutputSampleRate = 24000;//48000;//22050;
        // var OutputChannels = 1;
        // var maxID = 1024;
        // var chunkSize = 1400;
        // var dataID = 0;
        // var label_mic = 2001;

        // var MicRecorder;
        // var MicRecording = false;

        if (MicRecording == false) MicRecording = true;

        function FMCaptureMicStream(audioStream)
        {
          // creates the an instance of audioContext
          // const desiredSampleRate = OutputSampleRate;
          // const micAudioContext = new AudioContext({ sampleRate: desiredSampleRate });
          const micAudioContext = new AudioContext();//This can't affect Mic default samplerate...not work
          const sampleRate = micAudioContext.sampleRate; //retrieve the current sample rate of microphone the browser is using
          // console.log("FMLog Mic Samplerate: " + sampleRate);

          const volume = micAudioContext.createGain(); //creates a gain node
          const audioInput = micAudioContext.createMediaStreamSource(audioStream); //creates an audio node from the microphone incoming stream
          audioInput.connect(volume); //connect the stream to the gain node

          const bufferSize = 2048;
          const recorder = (micAudioContext.createScriptProcessor ||  micAudioContext.createJavaScriptNode).call(micAudioContext, bufferSize, 1, 1);
          MicRecorder = recorder;

          recorder.onaudioprocess = function(event)
          {
             const samples = event.inputBuffer.getChannelData(0);
             const PCM32Samples = OutputSampleRate < sampleRate ? downsampleAudioPCMArray(samples, sampleRate, OutputSampleRate) : samples;
             const outputLength = PCM32Samples.length * Float32Array.BYTES_PER_ELEMENT;
             const outputPtr = _malloc(outputLength);
             Module.HEAPU8.set(new Uint8Array(PCM32Samples.buffer), outputPtr);
             Module.dynCall_viii(_callback, _callbackID, outputLength, outputPtr);
          };
          volume.connect(recorder); //connect recorder
          recorder.connect(micAudioContext.destination); //start recording
        }

        navigator.getUserMedia = navigator.getUserMedia ||  navigator.webkitGetUserMedia ||  navigator.mozGetUserMedia ||  navigator.msGetUserMedia;
        if (navigator.mediaDevices)
        {
            navigator.mediaDevices.getUserMedia({ audio: true})
            .then((stream) => {
              /* use the stream */
                    console.log("JS: ediaDevices exist stream!");
              FMCaptureMicStream(stream); console.log("mediaDevices exist stream!");
            })
            .catch((err) => {
              /* handle the error */
              console.log('Error capturing audio.');
            });
        }
        else
        {
            if (navigator.getUserMedia)
            {
               //navigator.getUserMedia({ audio: true}, function(stream){ FMCaptureMicStream(stream); }, function(error){ console.log('Error capturing audio.'); } );
            } else { console.log('getUserMedia not supported in this browser.'); }
        }

        function Int16ToByte(_int16) { return [_int16 & 0xff, (_int16 >> 8) & 0xff] }
        function PCM32fArrayToPCM16Bytes(_PCM32f)
        {
          var result_bytes = new Int8Array(_PCM32f.length * 2); //2 bytes per int16
          for (let i = 0; i < _PCM32f.length; i++)
          {
             let val = Math.floor(32767 * _PCM32f[i]);
             val = Math.min(32767, val);
             val = Math.max(-32768, val);

             var valInt8Byte = Int16ToByte(val);
             var index = i * 2;
             result_bytes[index] = valInt8Byte[0];
             result_bytes[index + 1] = valInt8Byte[1];
          }
          return result_bytes;
        }
        function PCM32fArrayToPCM32Bytes(_PCM32f)
        {
          var result_bytes = new Int8Array(_PCM32f.length * 4); //2 bytes per int32
          for (let i = 0; i < _PCM32f.length; i++)
          {
             let val = Math.floor(32767 * _PCM32f[i]);
             val = Math.min(32767, val);
             val = Math.max(-32768, val);

             var valInt8Byte = Int16ToByte(val);
             var index = i * 4;
             result_bytes[index] = valInt8Byte[0];
             result_bytes[index + 1] = valInt8Byte[1];
             result_bytes[index + 2] = valInt8Byte[2];
             result_bytes[index + 3] = valInt8Byte[3];
          }
          return result_bytes;
        }

        //ref: https://stackoverflow.com/questions/52787510
        function downsampleAudioPCMArray(buffer, fromSampleRate, toSampleRate)
        {
           // buffer is a Float32Array
           var sampleRateRatio = Math.round(fromSampleRate / toSampleRate);
           var newLength = Math.round(buffer.length / sampleRateRatio);

           var result = new Float32Array(newLength);
           var offsetResult = 0;
           var offsetBuffer = 0;
           while (offsetResult < result.length)
           {
               var nextOffsetBuffer = Math.round((offsetResult + 1) * sampleRateRatio);
               var accum = 0, count = 0;
               for (var i = offsetBuffer; i < nextOffsetBuffer && i < buffer.length; i++)
               {
                   accum += buffer[i];
                   count++;
               }
               result[offsetResult] = accum / count;
               offsetResult++;
               offsetBuffer = nextOffsetBuffer;
           }
           return result;
        }
    },

    StreamAudio: function(NUM_LABEL, NUM_CHANNELS, NUM_SAMPLES, SAMPLE_RATE, _array, _size)
    {
      // Create a new buffer and fill it with data from HEAPU8, which stores the PCM32 data.
      const newArray = new ArrayBuffer(_size);
      const newByteArray = new Uint8Array(newArray);
      for (var i = 0; i < _size; i++) newByteArray[i] = HEAPU8[_array + i]; // Copying data byte-by-byte
      var AUDIO_CHUNKS = new Float32Array(newByteArray.buffer);
      console.log("JS: "+NUM_CHANNELS + " , " + NUM_SAMPLES + ", " + SAMPLE_RATE + " , (size)" + _size);
      // Create the audio buffer
      var audioBuffer = audioCtxArray[NUM_LABEL].createBuffer(NUM_CHANNELS, NUM_SAMPLES / NUM_CHANNELS, SAMPLE_RATE);
      for (var channel = 0; channel < NUM_CHANNELS; channel++)
      {
          var channelSamples = new Float32Array(NUM_SAMPLES / NUM_CHANNELS);
          for (var i = 0; i < NUM_SAMPLES; i++) channelSamples[i] = AUDIO_CHUNKS[i * NUM_CHANNELS + channel];
          audioBuffer.getChannelData(channel).set(channelSamples);
      }

      // Create a source and connect it to the destination (speakers)
      var source = audioCtxArray[NUM_LABEL].createBufferSource();
      source.buffer = audioBuffer;
      source.connect(audioCtxArray[NUM_LABEL].destination);

      // Use `audioCtx.currentTime` to synchronize playback more precisely
      let currentTime = audioCtxArray[NUM_LABEL].currentTime;
      let startTime = Math.max(currentTime, startTimeArray[NUM_LABEL]);
      source.start(startTime);
      startTimeArray[NUM_LABEL] = startTime + audioBuffer.duration;
    },

//     StreamAudio: function(NUM_LABEL, NUM_CHANNELS, NUM_SAMPLES, SAMPLE_RATE, PCM_SAMPLE_BIT, _array, _size)
// {
//     // Create a new buffer and fill it with data from HEAPU8, which stores the PCM32 data.
//     const newArray = new ArrayBuffer(_size);
//     const newByteArray = new Uint8Array(newArray);
//
//     for (var i = 0; i < _size; i++) newByteArray[i] = HEAPU8[_array + i]; // Copying data byte-by-byte
//
//     var AUDIO_CHUNKS = PCM_SAMPLE_BIT == 32 ? new Float32Array(newByteArray.buffer) : new Int16Array(newByteArray.buffer);
//     const PCMConversion = PCM_SAMPLE_BIT == 32 ? 1.0 : 1.0 / 32767.0;
//     console.log("JS: " + NUM_CHANNELS + " , " + NUM_SAMPLES + ", " + SAMPLE_RATE + " , (size)" + _size);
//
//     // Create an audio buffer with the specified sample rate and number of channels
//     var audioBuffer = audioCtxArray[NUM_LABEL].createBuffer(NUM_CHANNELS, NUM_SAMPLES / NUM_CHANNELS, SAMPLE_RATE);
//
//     // Fill each channel of the audio buffer with the interleaved PCM data
//     for (var channel = 0; channel < NUM_CHANNELS; channel++)
//     {
//         var pcm32BufferData = audioBuffer.getChannelData(channel);
//         pcm32BufferData.set(AUDIO_CHUNKS.subarray(channel, NUM_SAMPLES / NUM_CHANNELS), 0); // Optimized copy
//     }
//
//     // Create a source and connect it to the destination (speakers)
//     var source = audioCtxArray[NUM_LABEL].createBufferSource();
//     source.buffer = audioBuffer;
//     source.connect(audioCtxArray[NUM_LABEL].destination);
//
//     // Use `audioCtx.currentTime` to synchronize playback more precisely
//     let currentTime = audioCtxArray[NUM_LABEL].currentTime;
//     let startTime = Math.max(currentTime, startTimeArray[NUM_LABEL]);
//     source.start(startTime);
//     startTimeArray[NUM_LABEL] = startTime + audioBuffer.duration;
// },

    // StreamAudio: function(NUM_CHANNELS, NUM_SAMPLES, SAMPLE_RATE, AUDIO_CHUNKS)
    // {
    //     var audioBuffer = audioCtx.createBuffer(NUM_CHANNELS, (NUM_SAMPLES / NUM_CHANNELS), SAMPLE_RATE);
    //     for (var channel = 0; channel < NUM_CHANNELS; channel++)
    //     {
    //         // This gives us the actual ArrayBuffer that contains the data
    //         var nowBuffering = audioBuffer.getChannelData(channel);
    //         for (var i = 0; i < NUM_SAMPLES; i++)
    //         {
    //             var order = i * NUM_CHANNELS + channel;
    //             var localSample = 1.0/32767.0;
    //             localSample *= AUDIO_CHUNKS[order];
    //             nowBuffering[i] = localSample;
    //         }
    //     }
    //     var source = audioCtx.createBufferSource();
    //     source.buffer = audioBuffer;
    //     source.connect(audioCtx.destination);
    //     source.start(startTime);
    //     startTime += audioBuffer.duration;
    // },
});
