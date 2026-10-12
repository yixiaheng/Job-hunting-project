using DialogueSystem;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DialogueSystem.Editor
{
    //对话节点编辑窗口
    public class DialogueGraphWindow : EditorWindow
    {
        [SerializeField] DialogueGraphSO _graph;
        DialogueGraphView _graphView;
        ObjectField _graphField;
        TextField _nameField;

        //在Tools中提供打开的选项
        [MenuItem("Tools/Dialogue/Graph Editor")]
        public static void Open()
        {
            GetWindow<DialogueGraphWindow>("Dialogue Graph");
        }

        //DialogueGraphSOEditor中调用
        public static void Open(DialogueGraphSO graph)
        {
            var window = GetWindow<DialogueGraphWindow>("Dialogue Graph");
            window.Load(graph);
        }

        void CreateGUI()
        {

            var toolbar = new Toolbar();                //创建工具栏对象
            _graphField = new ObjectField("Graph")      //对象选择框，
            {
                objectType = typeof(DialogueGraphSO),   //选择资源的类型
                value = _graph                          //资源本身
            };
            _graphField.style.minWidth = 280;           //最小宽度

            //为对象选择框注册一个值变化回调。
            // RegisterValueChangedCallback 值变化的时候调用
            // Load(evt.newValue as DialogueGraphSO) 重新加载
            _graphField.RegisterValueChangedCallback(evt => Load(evt.newValue as DialogueGraphSO));

            
            toolbar.Add(_graphField);

            _nameField = new TextField("Name") { value = _graph != null ? _graph.graphName : string.Empty };
            _nameField.style.minWidth = 180;
            _nameField.RegisterValueChangedCallback(evt =>
            {
                if (_graph == null)
                    return;
                Undo.RecordObject(_graph, "Rename Dialogue Graph");
                _graph.graphName = evt.newValue;
                EditorUtility.SetDirty(_graph);
            });
            toolbar.Add(_nameField);

            toolbar.Add(new ToolbarButton(Save) { text = "Save" });
            rootVisualElement.Add(toolbar);

            _graphView = new DialogueGraphView();
            rootVisualElement.Add(_graphView);
            if (_graph != null)
                _graphView.Load(_graph);
        }

        void Load(DialogueGraphSO graph)
        {
            _graph = graph;
            if (_graphField != null)
                _graphField.SetValueWithoutNotify(graph);
            if (_nameField != null)
                _nameField.SetValueWithoutNotify(graph != null ? graph.graphName : string.Empty);
            _graphView?.Load(graph);
        }

        void Save()
        {
            if (_graph == null)
                return;
            EditorUtility.SetDirty(_graph);
            AssetDatabase.SaveAssets();
        }
    }
}
