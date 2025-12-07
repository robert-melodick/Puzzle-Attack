using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MapNode))]
public class MapNodeEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var node = (MapNode)target;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Quick Actions", EditorStyles.boldLabel);

        // Connection buttons
        if (node.nodeToConnect != null)
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Create Connection", GUILayout.Height(30)))
            {
                node.ConnectTo(node.nodeToConnect, node.makeConnectionBidirectional, node.makeConnectionCurved);
                node.nodeToConnect = null;
                EditorUtility.SetDirty(node);
            }

            if (GUILayout.Button("Remove Connection", GUILayout.Height(30)))
            {
                node.DisconnectFrom(node.nodeToConnect);
                node.nodeToConnect = null;
                EditorUtility.SetDirty(node);
            }

            EditorGUILayout.EndHorizontal();
        }
        else
        {
            EditorGUILayout.HelpBox(
                "Drag a MapNode into 'Node To Connect' field above to create or remove connections.", MessageType.Info);
        }

        EditorGUILayout.Space(5);

        // State buttons
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button(node.isUnlocked ? "Lock" : "Unlock"))
        {
            if (node.isUnlocked)
            {
                node.isUnlocked = false;
                node.isCompleted = false;
            }
            else
            {
                node.Unlock();
            }

            EditorUtility.SetDirty(node);
        }

        if (GUILayout.Button("Complete"))
        {
            node.Complete();
            EditorUtility.SetDirty(node);
        }

        EditorGUILayout.EndHorizontal();

        // Show connection info
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField($"Connected to {node.connectedPaths.Count} path(s)", EditorStyles.miniLabel);
    }
}