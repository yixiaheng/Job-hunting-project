using DialogueSystem;
using UnityEditor;
using UnityEngine;

namespace DialogueSystem.Editor
{
    [CustomEditor(typeof(DialogueGraphSO))]
    //DialogueGraphSO的Inspector的设置，在默认的数据后添加Open按钮，点击则显示对话控制视图
    public class DialogueGraphSOEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            if (GUILayout.Button("Open Graph Editor"))
                DialogueGraphWindow.Open(target as DialogueGraphSO);
        }
    }
}
