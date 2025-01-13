using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Events;

namespace Futurus
{
    /// <summary>
    /// The Timer Counter is useful for counting the number of times something happens in a period of time.
    /// AddOneToCounter must be called from somewhere to add to the counter that is reset each time period. 
    /// </summary>
    public class TimerCounter : MonoBehaviour
    {
        [Tooltip("Time in Seconds between each counting period")]
        public float timePeriodSeconds = 1;

        [Tooltip("The number of periods to average to get the final count")]
        public int queueSize = 10;

        int _latestCounter = 0;
        float _timeSinceLastTotal = 0;

        static Queue<int> averageTimePeriodQueue = new Queue<int>();

        public int AverageTotal { get; private set; }

        public UnityEvent<int> onAverageTotalChanged = new UnityEvent<int>();

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            
        }

        // Update is called once per frame
        void Update()
        {
            _timeSinceLastTotal += Time.deltaTime;


            if (_timeSinceLastTotal >= timePeriodSeconds)
            {
                _timeSinceLastTotal = 0;
                UpdateQueue();
            }
        }

        public void AddOneToCounter()
        {
            _latestCounter++;
        }

        void UpdateQueue()
        {
            averageTimePeriodQueue.Enqueue(_latestCounter);
            _latestCounter = 0;

            while (averageTimePeriodQueue.Count > queueSize)
                averageTimePeriodQueue.Dequeue();

            AverageTotal = (int)averageTimePeriodQueue.Average();

            onAverageTotalChanged?.Invoke(AverageTotal);
        }
    }
}
