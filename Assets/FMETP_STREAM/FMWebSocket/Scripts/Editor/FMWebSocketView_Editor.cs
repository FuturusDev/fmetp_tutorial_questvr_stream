using System;
using UnityEngine;
using UnityEditor;


namespace FMSolution.FMWebSocket
{
    [CustomEditor(typeof(FMWebSocketView))]
    [CanEditMultipleObjects]
    public class FMWebSocketView_Editor : UnityEditor.Editor
    {
        private FMWebSocketView FMView;

        SerializedProperty FMWebSocketProp;
        SerializedProperty SyncFPSProp;
        SerializedProperty SyncTypeProp;

        void OnEnable()
        {
            FMWebSocketProp = serializedObject.FindProperty("FMWebSocket");
            SyncFPSProp = serializedObject.FindProperty("SyncFPS");
            SyncTypeProp = serializedObject.FindProperty("SyncType");
        }

        // Update is called once per frame
        public override void OnInspectorGUI()
        {
            if (FMView == null) FMView = (FMWebSocketView)target;

            serializedObject.Update();


            GUILayout.Space(2);
            GUILayout.BeginVertical("box");
            {
                {
                    //Header
                    Color32 _color1 = new Color32(70, 157, 76, 255);//#469D4C	
                    Color32 _color2 = new Color32(255, 255, 255, 255);//#ffffff

                    GUIStyle style = new GUIStyle();
                    style.normal.textColor = _color2;
                    style.alignment = TextAnchor.MiddleCenter;
                    style.fontSize = 15;

                    Texture2D backgroundTexture = new Texture2D(1, 1);
                    backgroundTexture.SetPixel(0, 0, _color1);
                    backgroundTexture.Apply();
                    style.normal.background = backgroundTexture;

                    GUILayout.BeginHorizontal();
                    GUILayout.Label("(( FM WebSocket 4.0 ))", style);
                    GUILayout.EndHorizontal();
                }

                GUILayout.BeginVertical("box");
                {
                    GUILayout.BeginVertical();
                    {
                        GUILayout.BeginHorizontal();
                        EditorGUILayout.PropertyField(FMWebSocketProp, new GUIContent("FMWebSocket"));
                        GUILayout.EndHorizontal();


                        GUILayout.BeginHorizontal();
                        EditorGUILayout.PropertyField(SyncFPSProp, new GUIContent("Sync FPS"));
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal();
                        EditorGUILayout.PropertyField(SyncTypeProp, new GUIContent("Sync Type"));
                        GUILayout.EndHorizontal();
                    }
                    GUILayout.EndVertical();
                }
                GUILayout.EndVertical();

                GUILayout.BeginVertical("box");
                {
                    GUILayout.BeginVertical();
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Network Info: ");
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal();
                        int _viewID = FMView.GetViewID();
                        //if (_viewID == -1)
                        //{
                        //    FMView.UpdateAllIDs();
                        //    _viewID = FMView.GetViewID();
                        //}
                        //else
                        //{
                        //    if (FMView.CheckAllIDs()) _viewID = FMView.GetViewID();
                        //}

                        GUILayout.Label("- view ID: " + _viewID);
                        GUILayout.EndHorizontal();
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("- local ID: " + FMView.GetLocalViewID());
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal();
                        GUILayout.Label("- Is Owner: " + FMView.IsOwner);
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal();
                        GUILayout.Label("- Owner ID: " + FMView.GetOwnerID());
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal();
                        GUILayout.Label("- Global Room Object: " + (!FMView.GetIsInstaniatedInRuntime()));
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal();
                        string _prefabName = FMView.GetPrefabName();
                        GUILayout.Label("- Prefab(Resources): " + (string.IsNullOrEmpty(_prefabName) ? "null" : _prefabName));
                        GUILayout.EndHorizontal();
                    }
                    GUILayout.EndVertical();
                }
                GUILayout.EndVertical();
            }
            GUILayout.EndVertical();

            serializedObject.ApplyModifiedProperties();
        }
    }
}