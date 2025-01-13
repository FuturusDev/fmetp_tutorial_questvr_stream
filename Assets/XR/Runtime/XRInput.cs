using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SpatialTracking;
using UnityEngine.XR;
#if USING_XR_SDK_OCULUS
using Unity.XR.Oculus;
#endif

namespace Futurus.XR
{
    [System.Serializable]
    public enum Handedness { Left, Right, Both }
    public enum XRControllerInput { PrimaryBtn, SecondaryBtn, Trigger, Grip, MenuBtn, AxisBtn }

    /// <summary>
    /// Class Description
    /// </summary>
    public class XRInput : MonoBehaviour
    {
        const float HAPTIC_BASE_INTENSITY = 0.142f;
        const float HAPTIC_BASE_LENGTH = 0.056f;
        const float TRIGGER_BEGIN = 0.55f;
        const float TRIGGER_END = 0.35f;
        const float TRIGGER_RELATIVE_THRESHOLD = 0.01f;
        const float AXIS_DEADZONE = 0.01f;

        private delegate void InputEvent();
        private delegate void InputFlexEvent(float flex);

#region Inspector
        [Header("XR Input")]
        public XRNode xrNode = XRNode.LeftHand;
        [SerializeField] private XRController xRController = null;
        [SerializeField] private XRLaserPointer laserPointer;

#endregion

#region Public
        public Handedness Handedness {
            get { return (xrNode == XRNode.LeftHand) ? Handedness.Left : Handedness.Right; }
        }
        public XRController XRControllerInput {
            get { return xRController; }
        }
        public XRLaserPointer HandLaserPointer
        {
            get { return laserPointer; }
        }
        public Transform PoseTransform {
            get { return xRController.transform; }
        }
        public Vector3 PoseVelocity {
            get { return lastVelocity; }
        }
        public Vector3 PoseAngularVelocity {
            get { return lastAngularVelocity; }
        }
        public Vector3 PosePositionDelta {
            get { return lastPositionDelta; }
        }
        public Vector3 PoseRotationDelta {
            get { return lastRotationDelta.eulerAngles; }
        }
        public bool PrimaryButtonState {
            get { return lastPrimaryButtonState; }
        }
        public bool SecondaryButtonState {
            get { return lastSecondaryButtonState; }
        }
        public bool GripState {
            get { return lastGripFlexState; }
        }
        public float GripFlex {
            get { return lastGripFlex; }
        }
        public bool TriggerState {
            get { return lastTriggerFlexState; }
        }
        public float TriggerFlex {
            get { return lastTriggerFlex; }
        }
        public bool TriggerTouch {
            get { return lastTriggerTouch; }
        }
        public bool GripTouch {
            get { return lastGripTouch; }
        }
        public bool ThumbTouch {
            get { return lastThumbTouch; }
        }
        public Vector2 AxisState {
            get { return lastAxis; }
        }
        public bool AxisButtonState {
            get { return lastAxisButtonState; }
        }
        public bool UseHaptics {
            get { return useHaptics; }
            set { useHaptics = value; }
        }
        public bool DoingHaptics {
            get { return doingHaptics; }
        }

        public Action<XRInput> OnControllerConnected;
        public Action<XRInput> OnControllerDisconnected;

        public Action<XRInput, XRControllerInput> OnInputPressed;
        public Action<XRInput, XRControllerInput> OnInputReleased;
        public static Action<XRInput, XRControllerInput> OnAnyInputPressed;
        public static Action<XRInput, XRControllerInput> OnAnyInputReleased;

        public Action<XRInput> OnPrimaryButtonPressed;
        public Action<XRInput> OnPrimaryButtonReleased;
        public Action<XRInput> OnSecondaryButtonPressed;
        public Action<XRInput> OnSecondaryButtonReleased;
        public Action<XRInput> OnMenuButtonPressed;
        public Action<XRInput> OnMenuButtonReleased;
        public Action<XRInput> OnTriggerPressed;
        public Action<XRInput> OnTriggerReleased;
        public Action<XRInput, float> OnTriggerChanged;
        public Action<XRInput> OnGripPressed;
        public Action<XRInput> OnGripReleased;
        public Action<XRInput, float> OnGripChanged;
        public Action<XRInput, Vector2> OnAxisChanged;
        public Action<XRInput> OnAxisButtonPressed;
        public Action<XRInput> OnAxisButtonReleased;

        public static Action<XRInput, Vector2> OnInputAxisChanged;

        public void DoHaptics()
        {
            DoHaptics(HAPTIC_BASE_INTENSITY, HAPTIC_BASE_LENGTH);
        }

        public void DoHaptics(float amplitude, float duration)
        {
            if (useHaptics == false || doingHaptics) return;
            StartCoroutine(DoHapticsTask(amplitude, duration));
        }
#endregion

#region Unity Methods
        // Called when the object or component is enabled
        protected virtual void OnEnable()
        {

            OnInputPressed += OnAnyInputPressed;
            OnInputReleased += OnAnyInputReleased;

            inputDevice = InputDevices.GetDeviceAtXRNode(xrNode);
            if (inputDevice == null)
            {
                Debug.LogError("Missing Reference to InputDevice!");
            }

            InputDevices.deviceConnected += InputDevices_deviceConnected;
            InputDevices.deviceDisconnected += InputDevices_deviceDisconnected;

            

            lastPosition = xRController.transform.position;
            lastRotation = xRController.transform.rotation;
        }

        // Called when the object or component is disabled
        protected virtual void OnDisable()
        {
            InputDevices.deviceConnected -= InputDevices_deviceConnected;
            InputDevices.deviceDisconnected -= InputDevices_deviceDisconnected;

            OnInputPressed -= OnAnyInputPressed;
            OnInputReleased -= OnAnyInputReleased;
        }

        // Update is called once per frame
        protected virtual void Update()
        {
            if (inputDevice == null || !inputDevice.isValid) return;

            inputDevice.TryGetFeatureValue(CommonUsages.primaryButton, out bool tempPrimaryButtonState);
            inputDevice.TryGetFeatureValue(CommonUsages.secondaryButton, out bool tempSecondaryButtonState);
            inputDevice.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 tempAxis);
            inputDevice.TryGetFeatureValue(CommonUsages.primary2DAxisTouch, out bool tempAxisButtonState);
            inputDevice.TryGetFeatureValue(CommonUsages.trigger, out float tempTriggerFlex);
            inputDevice.TryGetFeatureValue(CommonUsages.grip, out float tempGripFlex);
            inputDevice.TryGetFeatureValue(CommonUsages.menuButton, out bool tempMenuButton);
            inputDevice.TryGetFeatureValue(CommonUsages.isTracked, out bool tempTracked);

            if (lastTrackedState != tempTracked)
            {
                if (tempTracked)
                {
                    OnControllerConnected?.Invoke(this);
                }
                else
                {
                    OnControllerConnected?.Invoke(this);
                }
                lastTrackedState = tempTracked;
            }
#if USING_XR_SDK_OCULUS
            // These are OVR specific capacitive touch checks
            // Index touch and thumbrest are supposed to be under OculusUsages but it doesn't appear to
            // work yet soooooo leave the obsolete call until it's fixed on Oculus side
            // Also it reports right, but it's mapped to a float butttt it only ever reports 0 or 1
            // [OBE] float lastTriggerTouchFloat = 0f;
            inputDevice.TryGetFeatureValue(OculusUsages.indexTouch, out lastTriggerTouch);
            // [OBE] lastTriggerTouch = (lastTriggerTouchFloat == 1) ? true : false;

            // Should report the grip button capacitive touch, but doesn't seem to work
            inputDevice.TryGetFeatureValue(OculusUsages.thumbTouch, out lastGripTouch);
#endif

            // Because I only care if the thumb is touching *something* I just override
            // the same field over and over again if it's false, otherwise we just ride with the true value
            bool tempThumbTouch = false;
            inputDevice.TryGetFeatureValue(CommonUsages.primary2DAxisTouch, out tempThumbTouch);
            if (!tempThumbTouch) { inputDevice.TryGetFeatureValue(CommonUsages.primaryTouch, out tempThumbTouch); }
            if (!tempThumbTouch) { inputDevice.TryGetFeatureValue(CommonUsages.secondaryTouch, out tempThumbTouch); }
            lastThumbTouch = tempThumbTouch;

            // Primary Button
            HandleButtonInput(XR.XRControllerInput.PrimaryBtn, ref tempPrimaryButtonState, ref lastPrimaryButtonState,
                ref lastPrimaryButtonDown, ref lastPrimaryButtonUp, OnPrimaryButtonPressed, OnPrimaryButtonReleased,
                this.PrimaryButtonPressed, this.PrimaryButtonReleased);

            // Secondary Button
            HandleButtonInput(XR.XRControllerInput.SecondaryBtn, ref tempSecondaryButtonState, ref lastSecondaryButtonState,
                ref lastSecondaryButtonDown, ref lastSecondaryButtonUp, OnSecondaryButtonPressed, OnSecondaryButtonReleased,
                this.SecondaryButtonPressed, this.SecondaryButtonReleased);

            // Axis Button
            HandleButtonInput(XR.XRControllerInput.AxisBtn, ref tempAxisButtonState, ref lastAxisButtonState,
                ref lastAxisButtonDown, ref lastAxisButtonUp, OnAxisButtonPressed, OnAxisButtonReleased,
                this.AxisButtonPressed, this.AxisButtonReleased);

            // Menu Button
            HandleButtonInput(XR.XRControllerInput.MenuBtn, ref tempMenuButton, ref lastMenuButtonState,
                ref lastMenuButtonDown, ref lastMenuButtonUp, OnMenuButtonPressed, OnMenuButtonReleased,
                this.MenuButtonPressed, this.MenuButtonReleased);

            // Trigger 
            HandleFlexInput(XR.XRControllerInput.Trigger, ref tempTriggerFlex, ref lastTriggerFlex, ref lastTriggerFlexState,
                ref lastTriggerFlexDown, ref lastTriggerFlexUp, OnTriggerPressed, OnTriggerReleased, OnTriggerChanged,
                this.TriggerPressed, this.TriggerReleased, this.TriggerChanged);

            // Grip 
            HandleFlexInput(XR.XRControllerInput.Grip, ref tempGripFlex, ref lastGripFlex, ref lastGripFlexState,
                ref lastGripFlexDown, ref lastGripFlexUp, OnGripPressed, OnGripReleased, OnGripChanged,
                this.GripPressed, this.GripReleased, this.GripChanged);

            // Axis
            if (lastAxis != tempAxis)
            {
                if (tempAxis.sqrMagnitude > AXIS_DEADZONE)
                {
                    lastAxis = tempAxis;
                    AxisChanged(tempAxis);
                    OnAxisChanged?.Invoke(this, tempAxis);
                    OnInputAxisChanged?.Invoke(this, tempAxis);
                    OnAnyInputPressed?.Invoke(this, XR.XRControllerInput.AxisBtn);
                }
                else if (lastAxis != Vector2.zero)
                {
                    // Failed Deadzone test, report zero for predicatable API 
                    lastAxis = Vector2.zero;
                    AxisChanged(Vector2.zero);
                    OnAxisChanged?.SafeInvoke(this, Vector2.zero);
                    OnInputAxisChanged?.Invoke(this, Vector2.zero);                   
                }

            }

            lastVelocity = ((lastPosition - transform.position) / Time.deltaTime);
            lastPositionDelta = xRController.transform.position - lastPosition;
            lastPosition = xRController.transform.position;

            lastAngularVelocity = GetAngularVelocity(lastRotation, xRController.transform.rotation);
            lastRotationDelta = xRController.transform.rotation * Quaternion.Inverse(lastRotation);
            lastRotation = xRController.transform.rotation;
        }
        private void OnValidate()
        {
            TrackedPoseDriver poseDriver = GetComponent<TrackedPoseDriver>();
            if (poseDriver != null)
            {
                switch (poseDriver.poseSource)
                {
                    case TrackedPoseDriver.TrackedPose.LeftPose:
                        xrNode = XRNode.LeftHand;
                        break;
                    case TrackedPoseDriver.TrackedPose.RightPose:
                        xrNode = XRNode.RightHand;
                        break;
                    case TrackedPoseDriver.TrackedPose.Head:
                        xrNode = XRNode.Head;
                        break;
                    default:
                        break;
                }
            }
        }
#endregion

#region Internal
        Vector3 lastPosition = Vector3.zero;
        Vector3 lastPositionDelta = Vector3.zero;
        Quaternion lastRotation = Quaternion.identity; //The value of the rotation at the previous update
        Quaternion lastRotationDelta = Quaternion.identity; //The difference in rotation between now and the previous update
        Vector3 lastAngularVelocity = Vector3.zero;
        Vector3 lastVelocity = Vector3.zero;
        bool lastTrackedState = false;
        bool lastPrimaryButtonState = false;
        bool lastPrimaryButtonUp = false; // REMOVE
        bool lastPrimaryButtonDown = false; // REMOVE
        bool lastSecondaryButtonState = false;
        bool lastSecondaryButtonUp = false; // REMOVE
        bool lastSecondaryButtonDown = false; // REMOVE
        bool lastMenuButtonState = false;
        bool lastMenuButtonUp = false; // REMOVE
        bool lastMenuButtonDown = false; // REMOVE
        bool lastTriggerFlexState = false;
        bool lastTriggerFlexUp = false; // REMOVE
        bool lastTriggerFlexDown = false;  // REMOVE
        float lastTriggerFlex = 0.0f;
        bool lastGripFlexState = false;
        bool lastGripFlexUp = false; // REMOVE
        bool lastGripFlexDown = false; // REMOVE
        float lastGripFlex = 0.0f;
        Vector2 lastAxis = Vector2.zero;
        bool lastAxisButtonState = false;
        bool lastAxisButtonUp = false; // REMOVE
        bool lastAxisButtonDown = false; // REMOVE
        bool lastTriggerTouch = false;
        bool lastGripTouch = false;
        bool lastThumbTouch = false;
        bool useHaptics = true;
        bool doingHaptics = false;
        InputDevice inputDevice;

        // TODO: Consider removing these virtual functions as XRInput is not intended to be derived from
        protected virtual void PrimaryButtonPressed() { }
        protected virtual void PrimaryButtonReleased() { }
        protected virtual void SecondaryButtonPressed() { }
        protected virtual void SecondaryButtonReleased() { }
        protected virtual void MenuButtonPressed() { }
        protected virtual void MenuButtonReleased() { }
        protected virtual void TriggerPressed() { }
        protected virtual void TriggerReleased() { }
        protected virtual void TriggerChanged(float trigger) { }
        protected virtual void GripPressed() { }
        protected virtual void GripReleased() { }
        protected virtual void GripChanged(float grip) { }
        protected virtual void AxisChanged(Vector2 axis) { }
        protected virtual void AxisButtonPressed() { }
        protected virtual void AxisButtonReleased() { }

        void HandleButtonInput(XRControllerInput controllerInput, ref bool tempButtonState, ref bool lastButtonState, ref bool lastButtonDown, ref bool lastButtonUp,
            Action<XRInput> pressedAction, Action<XRInput> releasedAction, InputEvent pressedDelegate, InputEvent releasedDelegate)
        {
            lastButtonDown = false;
            lastButtonUp = false;
            if (tempButtonState != lastButtonState) // Button state changed since last frame
            {
                lastButtonState = tempButtonState;
                if (tempButtonState)
                {
                    lastButtonDown = true;
                    pressedDelegate(); //FIXME: REMOVE
                    pressedAction?.Invoke(this);
                    OnInputPressed?.SafeInvoke(this, controllerInput);
                    OnAnyInputPressed?.Invoke(this, controllerInput);
                }
                else
                {
                    lastButtonUp = true;
                    releasedDelegate(); //FIXME: REMOVE
                    releasedAction?.Invoke(this);
                    OnInputReleased?.SafeInvoke(this, controllerInput);
                    OnAnyInputReleased?.Invoke(this, controllerInput);
                }
            }
        }
        void HandleFlexInput(XRControllerInput controllerInput, ref float currFlex, ref float lastFlex, ref bool lastFlexState, ref bool lastFlexDown, ref bool lastFlexUp,
            Action<XRInput> pressedAction, Action<XRInput> releasedAction, Action<XRInput, float> changedAction,
            InputEvent pressedDelegate, InputEvent releasedDelegate, InputFlexEvent changedDelegate)
        {
            // lastFlexDown = false; // NOT USED; REMOVE
            // lastFlexUp = false; // NOT USED; REMOVE
            if (Mathf.Abs(lastFlex - currFlex) > TRIGGER_RELATIVE_THRESHOLD)
            {
                var didPress = (lastFlex < TRIGGER_BEGIN) && (currFlex >= TRIGGER_BEGIN);
                var didRelease = (lastFlex > TRIGGER_END) && (currFlex <= TRIGGER_END);
                // lastFlexState = didPress; TODO: set it here instead?
                lastFlex = currFlex;
                changedDelegate(currFlex); //FIXME: REMOVE
                changedAction?.SafeInvoke(this, currFlex);
                if (didPress)
                {
                    lastFlexState = true;
                    // lastFlexDown = true;
                    pressedDelegate(); //FIXME: REMOVE
                    pressedAction?.Invoke(this);
                    OnInputPressed?.Invoke(this, controllerInput);
                    OnAnyInputPressed?.Invoke(this, controllerInput);
                }
                else if (didRelease)
                {
                    lastFlexState = false;
                    // lastFlexUp = true;
                    releasedDelegate(); //FIXME: REMOVE
                    releasedAction?.Invoke(this);
                    OnInputReleased?.Invoke(this, controllerInput);
                    OnAnyInputReleased?.Invoke(this, controllerInput);
                }
            }
        }
        Vector3 GetAngularVelocity(Quaternion foreLastFrameRotation, Quaternion lastFrameRotation)
        {
            var q = lastFrameRotation * Quaternion.Inverse(foreLastFrameRotation);
            // no rotation?
            // You may want to increase this closer to 1 if you want to handle very small rotations.
            // Beware, if it is too close to one your answer will be Nan
            if (Mathf.Abs(q.w) > 1023.5f / 1024.0f)
                return new Vector3(0, 0, 0);
            float gain;
            // handle negatives, we could just flip it but this is faster
            if (q.w < 0.0f)
            {
                var angle = Mathf.Acos(-q.w);
                gain = -2.0f * angle / (Mathf.Sin(angle) * Time.deltaTime);
            }
            else
            {
                var angle = Mathf.Acos(q.w);
                gain = 2.0f * angle / (Mathf.Sin(angle) * Time.deltaTime);
            }
            return new Vector3(q.x * gain, q.y * gain, q.z * gain);
        }
        void InputDevices_deviceConnected(InputDevice evidce)
        {
            inputDevice = InputDevices.GetDeviceAtXRNode(xrNode);
            // Do something to reconnect device if it was lost
            Debug.Log($"{xrNode.ToString()} Hand device connected");
            OnControllerConnected?.Invoke(this);
        }
        void InputDevices_deviceDisconnected(InputDevice device)
        {
            // Do something to inform the player that the device is invalid
            Debug.Log($"{xrNode.ToString()} Hand device disconnected");
            OnControllerDisconnected?.Invoke(this);
        }
        IEnumerator DoHapticsTask(float amplitude, float duration)
        {
            doingHaptics = true;
            HapticCapabilities capabilities;
            if (inputDevice.TryGetHapticCapabilities(out capabilities))
            {
                if (capabilities.supportsImpulse)
                {
                    uint channel = 0;
                    inputDevice.SendHapticImpulse(channel, Mathf.Clamp(amplitude, 0, 1f), duration);
                    yield return new WaitForSeconds(duration);
                }
            }
            doingHaptics = false;
        }
#endregion
    }
}
