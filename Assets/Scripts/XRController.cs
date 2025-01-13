using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Futurus
{
    public class XRController : MonoBehaviour
    {
        [Tooltip("The reference to the action of translating the selected object of this controller.")]
        [SerializeField] InputActionReference _positionAction;

        [Tooltip("The reference to the action of translating the selected object of this controller.")]
        [SerializeField] InputActionReference _rotationAction;

        void Update() => UpdateTrackingInput();
        void LateUpdate() => UpdateTrackingInput();
        void OnEnable()
        {
            _positionAction.action.Enable();
            _rotationAction.action.Enable();
        }
        void OnDisable()
        {
            _positionAction.action.Disable();
            _rotationAction.action.Disable();
        }
        void UpdateTrackingInput()
        {
            var posAction = _positionAction.action;
            var rotAction = _rotationAction.action;
            var hasPositionAction = posAction != null;
            var hasRotationAction = rotAction != null;

            // Update position
            if (hasPositionAction)
            {
                var pos = posAction.ReadValue<Vector3>();
                transform.localPosition = pos;
            }

            // Update rotation
            if (hasRotationAction)
            {
                var rot = rotAction.ReadValue<Quaternion>();
                transform.localRotation = rot;
            }
        }
    }
}
