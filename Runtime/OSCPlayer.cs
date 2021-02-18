using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using SeqOSC.Net;
using Debug = UnityEngine.Debug;

namespace SeqOSC
{
    public class OSCPlayer
    {
        public OSCBuffer Buffer { get; set; }
        public float Speed { get; set; } = 1.0f;

        public bool Loop { get; set; } = false;
        public string Host { get; set; } = IPAddress.Loopback.ToString();
        public int Port { get; set; } = 8000;

        public OSCClient Client { get; set; }

        public bool IsPlaying => _playing;

        public int Position => _position;

        private volatile bool _playing = false;

        private volatile int _position = 0;

        private Stopwatch watch = new Stopwatch();

        public OSCPlayer()
        {
            Client = new OSCClient();
        }

        public OSCPlayer(OSCBuffer buffer) : this()
        {
            Buffer = buffer;
        }

        public async Task Play()
        {
            if (Buffer.Samples.Count == 0)
                return;

            _playing = true;
            _position = 0;

            var receiver = new IPEndPoint(IPAddress.Parse(Host), Port);
            var lastTimeStamp = Buffer.Samples.First().Timestamp;

            // drift avoidance
            var firstTimeStamp = lastTimeStamp;
            watch.Start();

            while (_position < Buffer.Samples.Count && _playing)
            {
                var sample = Buffer.Samples[_position++];
                var delta = sample.Timestamp - lastTimeStamp;

                // adjust to stopwatch
                var watchTime = (int) Math.Round(watch.ElapsedMilliseconds * Speed);
                var totalSampleTime = sample.Timestamp - firstTimeStamp;
                var deltaWatchSample = watchTime - totalSampleTime;

                // sample every 60 buffers for drift debugging
                /*
                if (_position % 30 == 0)
                {
                    Debug.Log($"WT: {watchTime}\t" +
                              $"ST: {totalSampleTime}\t" +
                              $"D: {deltaWatchSample}\t" +
                              $"OD: {delta}\t" +
                              $"CD: {Math.Max(0, delta - deltaWatchSample)}\t" +
                              $"DD: {delta - Math.Max(0, delta - deltaWatchSample)}");
                }
                */

                delta = Math.Max(0, delta - deltaWatchSample);

                if (delta != 0)
                {
                    await Task.Delay((int) Math.Round(delta / Speed));
                }

                Client.Send(receiver, sample.Packet);

                lastTimeStamp = sample.Timestamp;

                if (Loop && _position >= Buffer.Samples.Count)
                {
                    lastTimeStamp = Buffer.Samples.First().Timestamp;
                    watch.Restart();
                    _position = 0;
                }
            }

            watch.Stop();
            _playing = false;
        }

        public void Stop()
        {
            _playing = false;
        }
    }
}