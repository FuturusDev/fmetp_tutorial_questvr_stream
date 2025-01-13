using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Futurus.XR
{
    /// <summary>
    /// Class Description
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class XRLaserPointer : MonoBehaviour, IManualUpdate
    {
        public enum LineType { Line, Arc, ArcForward }
        const int resolution = 10;
        const float arcUpPowerMin = 0.1f;
        const float arcUpPowerMax = 0.3f;
        const float arcAngleRange = 45f;

        #region Variables
        LineRenderer lineRenderer = null;
        bool lineEnabled = false;
        LineType currentType = LineType.Line;
        Transform startTarget = null;
        Transform endTarget = null;
        Vector3[] points = new Vector3[resolution];
        
        Transform dummyEndTarget = null;
        float dummyForwardDistance = 0f;
        Material cachedMaterial;
        #endregion

        #region Public
        public bool LineEnabled {
            get { return lineEnabled; }
        }

        /// <summary>
        /// Method Description
        /// </summary>
        public Color LineColor {
            get { return cachedMaterial.color; }
            set { cachedMaterial.color = value; }
        }

        /// <summary>
        /// Method Description
        /// </summary>
        public float LineWidthScaler {
            get { return lineRenderer.widthMultiplier; }
            set { lineRenderer.widthMultiplier = value; }
        }

        /// <summary>
        /// Method Description
        /// </summary>
        public Transform StartTarget {
            get { return startTarget; }
        }

        /// <summary>
        /// Method Description
        /// </summary>
        public Transform EndTarget {
            get { return endTarget; }
        }

        /// <summary>
        /// Method Description
        /// </summary>
        public LineType CurrentType {
            get { return currentType; }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="state"></param>
        public void SetLineActive(bool state)
        {
            if (lineEnabled == state) return;
            lineEnabled = state;
            lineRenderer.enabled = state;
            if (state)
            {
                UpdateManager.RegisterManualUpdate(this);
            }
            else
            {
                UpdateManager.DeregisterManualUpdate(this);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="startTarget"></param>
        /// <param name="endTarget"></param>
        /// <param name="newType"></param>
        /// <param name="lineColor"></param>
        /// Valid Selection
        public void SetTargets(Transform startTarget, Transform endTarget, LineType newType, float widthScaler = 1f, Color? lineColor = null)
        {
            if (startTarget == null || endTarget == null) return;
            this.startTarget = startTarget;
            this.endTarget = endTarget;
            dummyEndTarget.localPosition = Vector3.zero;
            this.currentType = newType;
            LineWidthScaler = widthScaler;
            //LineColor = lineColor ?? Color.white; // Color isn't a compile time constant...ugh
            var testColor = lineColor ?? Color.white;
            lineRenderer.SetColors(testColor, testColor);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="startTarget"></param>
        /// <param name="distance"></param>
        /// <param name="newType"></param>
        /// <param name="lineColor"></param>
        /// Invalid Selection
        public void SetTargets(Transform startTarget, float distance, LineType newType, float widthScaler = 1f, Color? lineColor = null)
        {
            if (startTarget == null) return;
            this.startTarget = startTarget;
            this.endTarget = dummyEndTarget;
            this.dummyForwardDistance = distance;
            this.currentType = newType;
            LineWidthScaler = widthScaler;
            var testColor = lineColor ?? Color.white;
            lineRenderer.SetColors(testColor, testColor);
        }

        public Vector3 GetTarget(Transform startTarget, float distance)
        {
            this.dummyForwardDistance = distance;
            if (startTarget == null) 
                return Vector3.zero;
            this.startTarget = startTarget;
            RaycastHit hit;
            if (Physics.Raycast(startTarget.position, startTarget.forward, out hit, dummyForwardDistance, ExLayerMask.ToLayerMask(16)))
            {
                if (!hit.transform.CompareTag("MovableItem"))
                    return hit.point;                
            }
            return Vector3.zero;
        }

        public void HandleUIRaycast()
        {
            RaycastHit hit;
            if (Physics.Raycast(startTarget.position, Vector3.forward, out hit, dummyForwardDistance, 1 >> 5))
            {
                Debug.Log(hit.collider.transform.ToString());                
            }
        }
        #endregion

        #region Unity Methods
        // Use this for initialization
        private void Awake()
        {
            dummyEndTarget = new GameObject("LaserPointerDummyTarget").transform;
            dummyEndTarget.Align(transform, true);
            gameObject.GetSetLocalReference(ref lineRenderer);
            lineRenderer.SetColors(Color.blue, Color.blue);

            //cachedMaterial.color = Color.blue;
            lineRenderer.positionCount = resolution;
        }
        // private void OnDestroy()
        // {
        //     CustomGameLoop.DefaultScheduleJobs.OnTick -= GameLoop_OnTick;
        // }
        #endregion

        #region Internal
        /// <summary>Do this in ManualUpdate instead of CustomGameLoop as the later will be paused by game</summary>
        public void ManualUpdate()
        {
            if (!lineEnabled)// || (startTarget == null || endTarget == null))
            {
                SetLineActive(false); // if either target becomes null
                return;
            }
            // lineRenderer.enabled = true;
            switch (currentType)
            {
                case LineType.Arc:
                    ComputeArc(startTarget, endTarget);
                    break;
                case LineType.ArcForward:
                    // Debug.Log("This");
                    ComputeArc(startTarget, endTarget, computeForwardTan: true);
                    break;
                case LineType.Line:
                default:
                    if (startTarget != null)
                    {
                        dummyEndTarget.position = startTarget.position + (startTarget.forward * dummyForwardDistance);
                    }
                    ComputeLine(startTarget, endTarget);
                    break;
            }
        }

        private void OnValidate()
        {
            gameObject.GetSetLocalReference(ref lineRenderer);
            if (lineRenderer == null) return;
            lineRenderer.positionCount = resolution;
            lineRenderer.enabled = false;
        }
        void ComputeArc(Transform start, Transform end, bool computeForwardTan = true)
        {
            Vector3 startTan = start.position;
            Vector3 endTan = end.position;
            if (computeForwardTan)
            {
                ComputeArcTangents(start, end, out startTan);
            }
            var bezier = new BezierCurve(start.up, Vector3.up, start.position, startTan, end.position, endTan);
            points = bezier.GetEvaluatedPoints(resolution);
            lineRenderer.SetPositions(points);
        }
        void ComputeArcTangents(Transform start, Transform end, out Vector3 startTan)
        {
            Vector3 dir = end.position - start.position;
            float upPower = Mathf.Lerp(arcUpPowerMin, arcUpPowerMax, Mathf.InverseLerp(0f, arcAngleRange, Vector3.Angle(dir, start.forward)));
            startTan = start.position + ((start.forward * (dir.magnitude / 2)) + (start.up * upPower));
            /* TODO Make this work, end tangent calc is bad
            dir = start.position - end.position;
            endTan = end.position + ((dir * (dir.magnitude / 2)) + (start.up * upPower));
            */
        }
        void ComputeLine(Transform start, Transform end)
        {
            for (int i = 0; i < resolution; i++)
            {
                if (points != null)
                {
                    if (i < points.Length)
                    {
                        if (start != null && end != null)
                        {

                            points[i] = Vector3.Lerp(start.position, end.position, i);
                        }
                    }
                }
                
            }
            lineRenderer.SetPositions(points);
        }
        #endregion
    }
}