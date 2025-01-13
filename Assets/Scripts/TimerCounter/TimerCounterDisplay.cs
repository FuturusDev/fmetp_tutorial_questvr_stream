using UnityEngine;
using TMPro;
using NaughtyAttributes;

namespace Futurus
{
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class TimerCounterDisplay : MonoBehaviour
    {
        TextMeshProUGUI _text;

        public bool setTimerCounter = false;
        [ShowIf("setTimerCounter")]
        public TimerCounter timerCounter;

        public string prefix;
        public string suffix;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            _text = GetComponent<TextMeshProUGUI>();
            if(setTimerCounter && timerCounter == null)
                timerCounter = gameObject.AddComponent<TimerCounter>();
            timerCounter?.onAverageTotalChanged.AddListener(SetText);
        }

        // Update is called once per frame
        void Update()
        {
        
        }

        void SetText(int count)
        {
            _text.text = $"{prefix}{count}{suffix}";
        }
    }
}
