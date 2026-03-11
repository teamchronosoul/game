#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VN.Editor
{
    public class VNChapterEditorWindow : EditorWindow
    {
        private VNProjectDatabase _project;
        private VNChapter _chapter;

        private Vector2 _graphScroll;
        private Vector2 _rightScroll;

        private int _selectedStepIndex = -1;
        private readonly HashSet<int> _multiSelection = new();

        private string _search = "";
        private StepFilter _filter = StepFilter.All;

        private bool _collapseSecondaryBranches = true;
        private bool _showOnlyReachable = false;
        private bool _autoLayoutEveryRepaint = false;

        private float _zoom = 1f;
        private const float MinZoom = 0.45f;
        private const float MaxZoom = 2.2f;

        private GUIStyle _nodeStyle;
        private GUIStyle _nodeSelectedStyle;
        private GUIStyle _nodeMultiSelectedStyle;
        private GUIStyle _nodeWarningStyle;
        private GUIStyle _nodeDangerStyle;
        private GUIStyle _badgeStyle;
        private GUIStyle _miniStyle;

        private const float NodeWidth = 300f;
        private const float BaseNodeHeight = 76f;
        private const float PortSpacing = 18f;
        private const float CanvasPadding = 70f;
        private const float PortSize = 12f;

        private const float DepthSpacing = 190f;
        private const float HorizontalNodeSpacing = 80f;

        private Rect _canvasRect = new Rect(0, 0, 2400, 1600);

        private readonly List<GraphNode> _nodes = new();
        private readonly Dictionary<int, GraphNode> _nodeByStep = new();
        private readonly Dictionary<string, int> _stepIndexById = new();
        private readonly Dictionary<string, Vector2> _manualNodePositions = new();
        private readonly HashSet<int> _reachableSteps = new();
        private readonly Dictionary<int, int> _incomingRefs = new();
        private readonly HashSet<int> _danglingFromSteps = new();
        private readonly Dictionary<int, float> _subtreeWidthCache = new();

        private DragNodeState _dragNode;
        private ConnectDragState _connectDrag;
        private SelectionRectState _selectionRect;

        private string _loadedLayoutKey = "";

        private string _newChapterId = "chapter_new";
        private string _newChapterAssetName = "VNChapter_New";

        private enum StepFilter { All, Line, Choice, If, Command, Jump, End }
        private enum GraphPortKind { Input, LinearNext, ChoiceOption, IfTrue, IfFalse, JumpTarget }

        [Serializable]
        private class LayoutEntry
        {
            public string stepId;
            public Vector2 pos;
        }

        [Serializable]
        private class LayoutSaveData
        {
            public float zoom = 1f;
            public List<LayoutEntry> entries = new();
        }

        private class GraphNode
        {
            public int stepIndex;
            public VNChapterStep step;
            public Rect rect;
            public int depth;
            public int row;
            public readonly List<GraphPort> outputs = new();
            public GraphPort input;
        }

        private class GraphPort
        {
            public GraphPortKind kind;
            public int stepIndex;
            public int optionIndex;
            public string label;
            public Rect rect;
        }

        private class DragNodeState
        {
            public bool active;
            public int stepIndex = -1;
            public Vector2 mouseOffset;
        }

        private class ConnectDragState
        {
            public bool active;
            public GraphPort fromPort;
            public Vector2 mouseCanvas;
        }

        private class SelectionRectState
        {
            public bool active;
            public Vector2 startCanvas;
            public Vector2 currentCanvas;

            public Rect Rect
            {
                get
                {
                    float x = Mathf.Min(startCanvas.x, currentCanvas.x);
                    float y = Mathf.Min(startCanvas.y, currentCanvas.y);
                    float w = Mathf.Abs(startCanvas.x - currentCanvas.x);
                    float h = Mathf.Abs(startCanvas.y - currentCanvas.y);
                    return new Rect(x, y, w, h);
                }
            }
        }

        private struct EdgeInfo
        {
            public int from;
            public int? to;
            public GraphPortKind portKind;
            public int optionIndex;
            public string label;
        }

        [MenuItem("Tools/VN/Chapter Editor")]
        public static void Open()
        {
            var w = GetWindow<VNChapterEditorWindow>("VN Chapter Editor");
            w.minSize = new Vector2(1320, 780);
            w.Show();
        }

        private void OnEnable()
        {
            if (_project == null)
            {
                var guids = AssetDatabase.FindAssets("t:VNProjectDatabase");
                if (guids != null && guids.Length > 0)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    _project = AssetDatabase.LoadAssetAtPath<VNProjectDatabase>(path);
                }
            }

            _dragNode ??= new DragNodeState();
            _connectDrag ??= new ConnectDragState();
            _selectionRect ??= new SelectionRectState();

            LoadLayoutState();
        }

        private void OnDisable()
        {
            SaveLayoutState();
        }

        private void OnDestroy()
        {
            SaveLayoutState();
        }

        private void EnsureStyles()
        {
            if (_nodeStyle != null) return;

            _nodeStyle = new GUIStyle(EditorStyles.helpBox)
            {
                richText = true,
                wordWrap = true,
                alignment = TextAnchor.UpperLeft,
                padding = new RectOffset(10, 10, 8, 8)
            };

            _nodeSelectedStyle = new GUIStyle(_nodeStyle);
            _nodeMultiSelectedStyle = new GUIStyle(_nodeStyle);
            _nodeWarningStyle = new GUIStyle(_nodeStyle);
            _nodeDangerStyle = new GUIStyle(_nodeStyle);

            _badgeStyle = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            _miniStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.85f, 0.85f, 0.85f, 1f) }
            };

            _nodeSelectedStyle.normal.background = MakeTex(new Color(0.20f, 0.45f, 0.90f, 1f));
            _nodeSelectedStyle.normal.textColor = Color.white;

            _nodeMultiSelectedStyle.normal.background = MakeTex(new Color(0.18f, 0.60f, 0.65f, 1f));
            _nodeMultiSelectedStyle.normal.textColor = Color.white;

            _nodeWarningStyle.normal.background = MakeTex(new Color(0.55f, 0.43f, 0.12f, 1f));
            _nodeWarningStyle.normal.textColor = Color.white;

            _nodeDangerStyle.normal.background = MakeTex(new Color(0.48f, 0.18f, 0.18f, 1f));
            _nodeDangerStyle.normal.textColor = Color.white;
        }

        private static Texture2D MakeTex(Color color)
        {
            var tex = new Texture2D(1, 1);
            tex.hideFlags = HideFlags.HideAndDontSave;
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return tex;
        }

        private void OnGUI()
        {
            EnsureStyles();
            DrawTop();
            EditorGUILayout.Space(6);

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawGraphPanel();
                DrawInspectorPanel();
            }
        }

        private void DrawTop()
        {
            var prevChapter = _chapter;

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                _project = (VNProjectDatabase)EditorGUILayout.ObjectField(
                    "Project", _project, typeof(VNProjectDatabase), false, GUILayout.MinWidth(260));

                _chapter = (VNChapter)EditorGUILayout.ObjectField(
                    "Chapter", _chapter, typeof(VNChapter), false, GUILayout.MinWidth(260));

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Gen IDs", EditorStyles.toolbarButton, GUILayout.Width(80)) && _chapter != null)
                {
                    Undo.RecordObject(_chapter, "Generate VN Step IDs");
                    _chapter.GenerateMissingStepIds();
                    EditorUtility.SetDirty(_chapter);
                    RebuildGraph(forceAutoLayout: true);
                }

                if (GUILayout.Button("Renumber IDs", EditorStyles.toolbarButton, GUILayout.Width(95)) && _chapter != null)
                    RenumberStepIdsSequential();

                if (GUILayout.Button("Auto Layout", EditorStyles.toolbarButton, GUILayout.Width(90)) && _chapter != null)
                    AutoLayoutGraph();

                if (GUILayout.Button("Normalize", EditorStyles.toolbarButton, GUILayout.Width(80)) && _chapter != null)
                    NormalizeLayout();
            }

            if (prevChapter != _chapter)
            {
                SaveLayoutState(prevChapter);
                _selectedStepIndex = -1;
                _multiSelection.Clear();
                LoadLayoutState();
            }

            using (new EditorGUILayout.VerticalScope("box"))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    _search = EditorGUILayout.TextField("Search", _search);
                    _filter = (StepFilter)EditorGUILayout.EnumPopup("Filter", _filter, GUILayout.Width(220));

                    _collapseSecondaryBranches = EditorGUILayout.ToggleLeft("Collapse side branches", _collapseSecondaryBranches, GUILayout.Width(180));
                    _showOnlyReachable = EditorGUILayout.ToggleLeft("Only reachable", _showOnlyReachable, GUILayout.Width(110));
                    _autoLayoutEveryRepaint = EditorGUILayout.ToggleLeft("Auto layout", _autoLayoutEveryRepaint, GUILayout.Width(100));

                    GUILayout.FlexibleSpace();
                    GUILayout.Label($"Zoom: {Mathf.RoundToInt(_zoom * 100f)}%", EditorStyles.miniLabel, GUILayout.Width(80));
                }

                EditorGUILayout.Space(4);

                using (new EditorGUILayout.HorizontalScope())
                {
                    _newChapterId = EditorGUILayout.TextField("New Chapter Id", _newChapterId);
                    _newChapterAssetName = EditorGUILayout.TextField("Asset Name", _newChapterAssetName);

                    using (new EditorGUI.DisabledScope(_project == null))
                    {
                        if (GUILayout.Button("Create Chapter", GUILayout.Width(130)))
                            CreateChapterAssetAndAddToProject();
                    }
                }
            }
        }

        private void DrawGraphPanel()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(position.width * 0.66f)))
            {
                if (_chapter == null)
                {
                    EditorGUILayout.HelpBox("Assign VNChapter.", MessageType.Info);
                    return;
                }

                DrawGraphToolbar();
                RebuildGraph(forceAutoLayout: _autoLayoutEveryRepaint);

                Vector2 scaledCanvasSize = new Vector2(
                    Mathf.Max(1600f, _canvasRect.width * _zoom),
                    Mathf.Max(1200f, _canvasRect.height * _zoom)
                );

                _graphScroll = EditorGUILayout.BeginScrollView(_graphScroll, true, true);
                try
                {
                    var contentRect = GUILayoutUtility.GetRect(scaledCanvasSize.x, scaledCanvasSize.y);

                    HandleGraphEvents();
                    DrawCanvasBackground(contentRect);
                    DrawGraphConnections();
                    DrawGraphNodes();
                    DrawSelectionRect();
                    DrawConnectionPreview();
                }
                finally
                {
                    EditorGUILayout.EndScrollView();
                }
            }
        }

        private void DrawGraphToolbar()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("+ Line", GUILayout.Width(70))) AddStepAtEnd(typeof(VNLineStep));
                    if (GUILayout.Button("+ Choice", GUILayout.Width(80))) AddStepAtEnd(typeof(VNChoiceStep));
                    if (GUILayout.Button("+ If", GUILayout.Width(60))) AddStepAtEnd(typeof(VNIfStep));
                    if (GUILayout.Button("+ Command", GUILayout.Width(95))) AddStepAtEnd(typeof(VNCommandStep));
                    if (GUILayout.Button("+ Jump", GUILayout.Width(80))) AddStepAtEnd(typeof(VNJumpStep));
                    if (GUILayout.Button("+ End", GUILayout.Width(70))) AddStepAtEnd(typeof(VNEndStep));

                    GUILayout.FlexibleSpace();
                    GUILayout.Label($"Reachable: {_reachableSteps.Count}/{(_chapter?.steps?.Count ?? 0)}", EditorStyles.miniLabel, GUILayout.Width(120));
                    GUILayout.Label($"Selected: {_multiSelection.Count}", EditorStyles.miniLabel, GUILayout.Width(80));
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(_selectedStepIndex < 0))
                    {
                        if (GUILayout.Button("Child Line", GUILayout.Width(85))) AddChildStep(typeof(VNLineStep));
                        if (GUILayout.Button("Child Choice", GUILayout.Width(95))) AddChildStep(typeof(VNChoiceStep));
                        if (GUILayout.Button("Child If", GUILayout.Width(75))) AddChildStep(typeof(VNIfStep));
                        if (GUILayout.Button("Child Cmd", GUILayout.Width(85))) AddChildStep(typeof(VNCommandStep));
                        if (GUILayout.Button("Child Jump", GUILayout.Width(90))) AddChildStep(typeof(VNJumpStep));
                        if (GUILayout.Button("Child End", GUILayout.Width(80))) AddChildStep(typeof(VNEndStep));
                        if (GUILayout.Button("Set As First", GUILayout.Width(95))) SetSelectedAsFirst();
                        if (GUILayout.Button("Delete", GUILayout.Width(80))) DeleteSelected();
                    }

                    GUILayout.FlexibleSpace();
                }
            }
        }

        private Vector2 ViewToCanvas(Vector2 viewPos) => (_graphScroll + viewPos) / _zoom;
        private Vector2 CanvasToView(Vector2 canvasPos) => canvasPos * _zoom - _graphScroll;

        private Rect CanvasToView(Rect canvasRect)
        {
            return new Rect(
                canvasRect.x * _zoom - _graphScroll.x,
                canvasRect.y * _zoom - _graphScroll.y,
                canvasRect.width * _zoom,
                canvasRect.height * _zoom
            );
        }

        private void DrawCanvasBackground(Rect rect)
        {
            if (Event.current.type != EventType.Repaint) return;

            EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.12f, 1f));

            const float grid = 24f;
            Handles.BeginGUI();
            Handles.color = new Color(1f, 1f, 1f, 0.03f);

            float startX = rect.xMin - (rect.xMin % grid);
            float startY = rect.yMin - (rect.yMin % grid);

            for (float x = startX; x < rect.xMax; x += grid)
                Handles.DrawLine(new Vector3(x, rect.yMin), new Vector3(x, rect.yMax));

            for (float y = startY; y < rect.yMax; y += grid)
                Handles.DrawLine(new Vector3(rect.xMin, y), new Vector3(rect.xMax, y));

            Handles.EndGUI();
        }

        private void DrawGraphNodes()
        {
            foreach (var node in _nodes)
            {
                if (node.step == null) continue;

                Rect vr = CanvasToView(node.rect);
                GUI.Box(vr, BuildNodeText(node.stepIndex, node.step), GetNodeStyle(node.stepIndex));

                DrawNodeBadges(node);
                DrawNodePorts(node);
            }
        }

        private GUIStyle GetNodeStyle(int stepIndex)
        {
            if (stepIndex == _selectedStepIndex) return _nodeSelectedStyle;
            if (_multiSelection.Contains(stepIndex)) return _nodeMultiSelectedStyle;
            if (IsDangling(stepIndex) || IsUnreachable(stepIndex)) return _nodeDangerStyle;
            if (IsOrphan(stepIndex)) return _nodeWarningStyle;
            return _nodeStyle;
        }

        private void DrawNodeBadges(GraphNode node)
        {
            Rect vr = CanvasToView(node.rect);

            float x = vr.x + 8;
            float y = vr.yMax - 18;

            if (node.stepIndex == 0)
            {
                DrawBadge(new Rect(x, y, 48, 14), "START", new Color(0.20f, 0.55f, 0.90f, 1f));
                x += 54;
            }

            if (IsUnreachable(node.stepIndex))
            {
                DrawBadge(new Rect(x, y, 78, 14), "UNREACH", new Color(0.72f, 0.22f, 0.22f, 1f));
                x += 84;
            }

            if (IsOrphan(node.stepIndex))
            {
                DrawBadge(new Rect(x, y, 62, 14), "ORPHAN", new Color(0.72f, 0.52f, 0.15f, 1f));
                x += 68;
            }

            if (IsDangling(node.stepIndex))
            {
                DrawBadge(new Rect(x, y, 70, 14), "DANGLING", new Color(0.68f, 0.18f, 0.18f, 1f));
            }
        }

        private void DrawBadge(Rect r, string text, Color bg)
        {
            EditorGUI.DrawRect(r, bg);
            GUI.Label(r, text, _badgeStyle);
        }

        private void DrawNodePorts(GraphNode node)
        {
            if (node.input != null)
                DrawPort(node.input, Color.gray);

            foreach (var port in node.outputs)
            {
                DrawPort(port, GetPortColor(port.kind));
                Rect viewRect = CanvasToView(port.rect);
                var labelRect = new Rect(viewRect.center.x - 40f, viewRect.y - 16f, 80f, 14f);
                GUI.Label(labelRect, port.label, _badgeStyle);
            }
        }

        private void DrawPort(GraphPort port, Color color)
        {
            Rect vr = CanvasToView(port.rect);
            EditorGUI.DrawRect(vr, new Color(0, 0, 0, 0.45f));
            var inner = new Rect(vr.x + 2, vr.y + 2, vr.width - 4, vr.height - 4);
            EditorGUI.DrawRect(inner, color);
        }

        private static Color GetPortColor(GraphPortKind kind)
        {
            return kind switch
            {
                GraphPortKind.Input => new Color(0.75f, 0.75f, 0.75f, 1f),
                GraphPortKind.LinearNext => new Color(0.65f, 0.82f, 1f, 1f),
                GraphPortKind.ChoiceOption => new Color(1f, 0.78f, 0.25f, 1f),
                GraphPortKind.IfTrue => new Color(0.30f, 0.90f, 0.40f, 1f),
                GraphPortKind.IfFalse => new Color(0.95f, 0.38f, 0.38f, 1f),
                GraphPortKind.JumpTarget => new Color(0.82f, 0.55f, 1f, 1f),
                _ => Color.white
            };
        }

        private void DrawGraphConnections()
        {
            Handles.BeginGUI();

            foreach (var node in _nodes)
            {
                foreach (var port in node.outputs)
                {
                    int? targetStep = ResolveTargetForPort(port);
                    if (!targetStep.HasValue) continue;
                    if (!_nodeByStep.TryGetValue(targetStep.Value, out var targetNode) || targetNode?.input == null) continue;

                    Vector3 start = CanvasToView(port.rect.center);
                    Vector3 end = CanvasToView(targetNode.input.rect.center);

                    Vector3 tanA = start + Vector3.down * 70f;
                    Vector3 tanB = end + Vector3.up * 70f;

                    Color col = GetPortColor(port.kind);
                    Handles.DrawBezier(start, end, tanA, tanB, col, null, 3f);

                    Vector2 mid = (start + end) * 0.5f;
                    Rect labelRect = new Rect(mid.x - 54, mid.y - 10, 108, 18);
                    EditorGUI.DrawRect(labelRect, new Color(0.10f, 0.10f, 0.10f, 0.85f));
                    GUI.Label(labelRect, port.label ?? "", _badgeStyle);
                }
            }

            Handles.EndGUI();
        }

        private void DrawConnectionPreview()
        {
            if (_connectDrag == null || !_connectDrag.active || _connectDrag.fromPort == null)
                return;

            Handles.BeginGUI();

            Vector3 start = CanvasToView(_connectDrag.fromPort.rect.center);
            Vector3 end = CanvasToView(_connectDrag.mouseCanvas);

            Vector3 tanA = start + Vector3.down * 70f;
            Vector3 tanB = end + Vector3.up * 70f;

            Color col = GetPortColor(_connectDrag.fromPort.kind);
            Handles.DrawBezier(start, end, tanA, tanB, col, null, 3f);

            Handles.EndGUI();
        }

        private void DrawSelectionRect()
        {
            if (_selectionRect == null || !_selectionRect.active) return;

            Rect r = CanvasToView(_selectionRect.Rect);
            EditorGUI.DrawRect(r, new Color(0.2f, 0.5f, 1f, 0.12f));

            Handles.BeginGUI();
            Handles.color = new Color(0.2f, 0.6f, 1f, 0.9f);
            Handles.DrawAAPolyLine(2f,
                new Vector3(r.xMin, r.yMin),
                new Vector3(r.xMax, r.yMin),
                new Vector3(r.xMax, r.yMax),
                new Vector3(r.xMin, r.yMax),
                new Vector3(r.xMin, r.yMin));
            Handles.EndGUI();
        }

        private void HandleGraphEvents()
        {
            var evt = Event.current;
            Vector2 mouseCanvas = ViewToCanvas(evt.mousePosition);

            if (TryHandleZoom(evt))
                return;

            if (_connectDrag != null && _connectDrag.active)
                _connectDrag.mouseCanvas = mouseCanvas;

            if (evt.type == EventType.ContextClick)
            {
                ShowContextMenu(mouseCanvas);
                evt.Use();
                return;
            }

            if (evt.type == EventType.MouseDown && evt.button == 0)
            {
                GraphPort clickedPort = FindOutputPortAt(mouseCanvas);
                if (clickedPort != null)
                {
                    _connectDrag.active = true;
                    _connectDrag.fromPort = clickedPort;
                    _connectDrag.mouseCanvas = mouseCanvas;
                    evt.Use();
                    return;
                }

                GraphNode clickedNode = FindNodeAt(mouseCanvas);
                if (clickedNode != null)
                {
                    bool additive = evt.control || evt.command;
                    if (!additive)
                        _multiSelection.Clear();

                    if (additive)
                    {
                        if (_multiSelection.Contains(clickedNode.stepIndex))
                            _multiSelection.Remove(clickedNode.stepIndex);
                        else
                            _multiSelection.Add(clickedNode.stepIndex);
                    }
                    else
                    {
                        _selectedStepIndex = clickedNode.stepIndex;
                        _multiSelection.Add(clickedNode.stepIndex);
                    }

                    _selectedStepIndex = clickedNode.stepIndex;
                    _dragNode.active = true;
                    _dragNode.stepIndex = clickedNode.stepIndex;
                    _dragNode.mouseOffset = mouseCanvas - clickedNode.rect.position;
                    GUI.FocusControl(null);
                    Repaint();
                    evt.Use();
                    return;
                }

                _selectionRect.active = true;
                _selectionRect.startCanvas = mouseCanvas;
                _selectionRect.currentCanvas = mouseCanvas;

                if (!(evt.control || evt.command))
                {
                    _multiSelection.Clear();
                    _selectedStepIndex = -1;
                }

                GUI.FocusControl(null);
                Repaint();
            }

            if (evt.type == EventType.MouseDrag && evt.button == 0)
            {
                if (_connectDrag != null && _connectDrag.active)
                {
                    _connectDrag.mouseCanvas = mouseCanvas;
                    Repaint();
                    evt.Use();
                    return;
                }

                if (_dragNode != null && _dragNode.active && _nodeByStep.TryGetValue(_dragNode.stepIndex, out var draggedNode))
                {
                    Vector2 targetPos = mouseCanvas - _dragNode.mouseOffset;
                    targetPos.x = Mathf.Max(20f, targetPos.x);
                    targetPos.y = Mathf.Max(20f, targetPos.y);

                    Vector2 delta = targetPos - draggedNode.rect.position;

                    IEnumerable<int> affected = _multiSelection.Count > 1 && _multiSelection.Contains(_dragNode.stepIndex)
                        ? _multiSelection
                        : new[] { _dragNode.stepIndex };

                    foreach (int idx in affected)
                    {
                        if (_nodeByStep.TryGetValue(idx, out var n))
                        {
                            n.rect.position += delta;
                            n.rect.x = Mathf.Max(20f, n.rect.x);
                            n.rect.y = Mathf.Max(20f, n.rect.y);
                            RememberManualPosition(n);
                        }
                    }

                    Repaint();
                    evt.Use();
                    return;
                }

                if (_selectionRect != null && _selectionRect.active)
                {
                    _selectionRect.currentCanvas = mouseCanvas;
                    Repaint();
                    evt.Use();
                }
            }

            if (evt.type == EventType.MouseUp && evt.button == 0)
            {
                if (_connectDrag != null && _connectDrag.active)
                {
                    GraphNode targetNode = FindNodeAt(mouseCanvas);
                    if (targetNode != null && _connectDrag.fromPort != null && targetNode.stepIndex != _connectDrag.fromPort.stepIndex)
                    {
                        ConnectPortToStep(_connectDrag.fromPort, targetNode.stepIndex);
                    }
                    else if (_connectDrag.fromPort != null)
                    {
                        CreateTargetFromDraggedPort(_connectDrag.fromPort);
                    }

                    _connectDrag.active = false;
                    _connectDrag.fromPort = null;
                    RebuildGraph(forceAutoLayout: false);
                    evt.Use();
                    return;
                }

                if (_selectionRect != null && _selectionRect.active)
                {
                    Rect sel = _selectionRect.Rect;
                    foreach (var node in _nodes)
                    {
                        if (sel.Overlaps(node.rect))
                            _multiSelection.Add(node.stepIndex);
                    }

                    if (_multiSelection.Count > 0)
                        _selectedStepIndex = _multiSelection.Last();

                    _selectionRect.active = false;
                    Repaint();
                    evt.Use();
                }

                if (_dragNode != null)
                {
                    _dragNode.active = false;
                    SaveLayoutState();
                }
            }
        }

        private bool TryHandleZoom(Event evt)
        {
            bool shiftHeld = (evt.modifiers & EventModifiers.Shift) != 0;
            if (evt.type != EventType.ScrollWheel || !shiftHeld)
                return false;

            float oldZoom = _zoom;
            float zoomFactor = evt.delta.y > 0f ? 0.9f : 1.1f;
            float newZoom = Mathf.Clamp(oldZoom * zoomFactor, MinZoom, MaxZoom);

            if (Mathf.Approximately(oldZoom, newZoom))
            {
                evt.Use();
                return true;
            }

            Vector2 mouseCanvasBefore = (_graphScroll + evt.mousePosition) / oldZoom;
            _zoom = newZoom;
            _graphScroll = mouseCanvasBefore * _zoom - evt.mousePosition;

            SaveLayoutState();
            evt.Use();
            Repaint();
            return true;
        }

        private void ShowContextMenu(Vector2 mouseCanvas)
        {
            GraphNode node = FindNodeAt(mouseCanvas);
            var menu = new GenericMenu();

            if (node != null)
            {
                _selectedStepIndex = node.stepIndex;
                if (!_multiSelection.Contains(node.stepIndex))
                {
                    _multiSelection.Clear();
                    _multiSelection.Add(node.stepIndex);
                }

                menu.AddDisabledItem(new GUIContent($"Node {node.stepIndex:000}"));
                menu.AddSeparator("");

                menu.AddItem(new GUIContent("Add Child/Line"), false, () => AddChildStep(typeof(VNLineStep)));
                menu.AddItem(new GUIContent("Add Child/Choice"), false, () => AddChildStep(typeof(VNChoiceStep)));
                menu.AddItem(new GUIContent("Add Child/If"), false, () => AddChildStep(typeof(VNIfStep)));
                menu.AddItem(new GUIContent("Add Child/Command"), false, () => AddChildStep(typeof(VNCommandStep)));
                menu.AddItem(new GUIContent("Add Child/Jump"), false, () => AddChildStep(typeof(VNJumpStep)));
                menu.AddItem(new GUIContent("Add Child/End"), false, () => AddChildStep(typeof(VNEndStep)));

                menu.AddSeparator("");
                menu.AddItem(new GUIContent("Insert After/Line"), false, () => InsertAfter(_selectedStepIndex, typeof(VNLineStep)));
                menu.AddItem(new GUIContent("Insert After/Choice"), false, () => InsertAfter(_selectedStepIndex, typeof(VNChoiceStep)));
                menu.AddItem(new GUIContent("Insert After/If"), false, () => InsertAfter(_selectedStepIndex, typeof(VNIfStep)));
                menu.AddItem(new GUIContent("Insert After/Command"), false, () => InsertAfter(_selectedStepIndex, typeof(VNCommandStep)));
                menu.AddItem(new GUIContent("Insert After/Jump"), false, () => InsertAfter(_selectedStepIndex, typeof(VNJumpStep)));
                menu.AddItem(new GUIContent("Insert After/End"), false, () => InsertAfter(_selectedStepIndex, typeof(VNEndStep)));

                menu.AddSeparator("");
                menu.AddItem(new GUIContent("Set As First"), false, SetSelectedAsFirst);
                menu.AddItem(new GUIContent("Delete Selected"), false, DeleteSelected);
            }
            else
            {
                menu.AddItem(new GUIContent("Create/Line"), false, () => AddStepAtEnd(typeof(VNLineStep)));
                menu.AddItem(new GUIContent("Create/Choice"), false, () => AddStepAtEnd(typeof(VNChoiceStep)));
                menu.AddItem(new GUIContent("Create/If"), false, () => AddStepAtEnd(typeof(VNIfStep)));
                menu.AddItem(new GUIContent("Create/Command"), false, () => AddStepAtEnd(typeof(VNCommandStep)));
                menu.AddItem(new GUIContent("Create/Jump"), false, () => AddStepAtEnd(typeof(VNJumpStep)));
                menu.AddItem(new GUIContent("Create/End"), false, () => AddStepAtEnd(typeof(VNEndStep)));

                menu.AddSeparator("");
                menu.AddItem(new GUIContent("Layout/Auto Layout"), false, AutoLayoutGraph);
                menu.AddItem(new GUIContent("Layout/Normalize Layout"), false, NormalizeLayout);
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("Selection/Clear"), false, () =>
                {
                    _multiSelection.Clear();
                    _selectedStepIndex = -1;
                    Repaint();
                });
            }

            menu.ShowAsContext();
        }

        private GraphNode FindNodeAt(Vector2 mouseCanvas)
        {
            for (int i = _nodes.Count - 1; i >= 0; i--)
            {
                if (_nodes[i].rect.Contains(mouseCanvas))
                    return _nodes[i];
            }
            return null;
        }

        private GraphPort FindOutputPortAt(Vector2 mouseCanvas)
        {
            for (int i = _nodes.Count - 1; i >= 0; i--)
            {
                foreach (var p in _nodes[i].outputs)
                {
                    if (p.rect.Contains(mouseCanvas))
                        return p;
                }
            }
            return null;
        }

        private void RememberManualPosition(GraphNode node)
        {
            if (node?.step == null || string.IsNullOrWhiteSpace(node.step.id)) return;

            Vector2 pos = node.rect.position;
            pos.x = Mathf.Max(20f, pos.x);
            pos.y = Mathf.Max(20f, pos.y);

            node.rect.position = pos;
            _manualNodePositions[node.step.id] = pos;
        }

        private void AutoLayoutGraph()
        {
            if (_chapter?.steps == null || _chapter.steps.Count == 0)
                return;

            _manualNodePositions.Clear();
            _subtreeWidthCache.Clear();

            var visited = new HashSet<int>();

            float rootWidth = GetSubtreeWidth(0);
            LayoutSubtree(0, 0, CanvasPadding, CanvasPadding, visited);

            float extraX = CanvasPadding + rootWidth + HorizontalNodeSpacing * 2f;

            for (int i = 0; i < _chapter.steps.Count; i++)
            {
                if (visited.Contains(i)) continue;

                float w = GetSubtreeWidth(i);
                LayoutSubtree(i, 0, extraX, CanvasPadding, visited);
                extraX += w + HorizontalNodeSpacing * 2f;
            }

            RebuildGraph(forceAutoLayout: false);
            SaveLayoutState();
            Repaint();
        }

        private void NormalizeLayout()
        {
            AutoLayoutGraph();
        }

        private void LayoutSubtree(int stepIndex, int depth, float leftX, float topY, HashSet<int> visited)
        {
            if (_chapter?.steps == null) return;
            if (stepIndex < 0 || stepIndex >= _chapter.steps.Count) return;
            if (visited.Contains(stepIndex)) return;

            visited.Add(stepIndex);

            float subtreeWidth = GetSubtreeWidth(stepIndex);
            float x = leftX + (subtreeWidth - NodeWidth) * 0.5f;
            float y = topY + depth * DepthSpacing;

            var step = _chapter.steps[stepIndex];
            if (step != null && !string.IsNullOrWhiteSpace(step.id))
                _manualNodePositions[step.id] = new Vector2(x, y);

            var children = GetLayoutChildren(stepIndex);
            if (children.Count == 0) return;

            float childLeft = leftX;
            for (int i = 0; i < children.Count; i++)
            {
                int child = children[i];
                float childWidth = GetSubtreeWidth(child);
                LayoutSubtree(child, depth + 1, childLeft, topY, visited);
                childLeft += childWidth + HorizontalNodeSpacing;
            }
        }

        private float FindFreeX(int depth, float desiredX, Dictionary<int, List<float>> taken)
        {
            if (!taken.TryGetValue(depth, out var row))
            {
                row = new List<float>();
                taken[depth] = row;
            }

            float x = Mathf.Max(CanvasPadding, desiredX);
            float minDist = NodeWidth + 20f;

            bool placed = false;
            int guard = 0;

            while (!placed && guard < 2000)
            {
                placed = true;

                for (int i = 0; i < row.Count; i++)
                {
                    if (Mathf.Abs(row[i] - x) < minDist)
                    {
                        placed = false;
                        x += minDist;
                        break;
                    }
                }

                guard++;
            }

            row.Add(x);
            return x;
        }

        private List<int> GetLayoutChildren(int stepIndex)
        {
            var result = new List<int>();
            if (_chapter?.steps == null) return result;
            if (stepIndex < 0 || stepIndex >= _chapter.steps.Count) return result;

            var step = _chapter.steps[stepIndex];
            if (step == null) return result;

            void AddIfValid(int? idx)
            {
                if (idx.HasValue && idx.Value >= 0 && idx.Value < _chapter.steps.Count && !result.Contains(idx.Value))
                    result.Add(idx.Value);
            }

            switch (step)
            {
                case VNLineStep line:
                    AddIfValid(ResolveExplicitOrLinearNext(stepIndex, line.nextStepId));
                    break;

                case VNCommandStep cmd:
                    AddIfValid(ResolveExplicitOrLinearNext(stepIndex, cmd.nextStepId));
                    break;

                case VNJumpStep jump:
                    AddIfValid(ResolveStepId(jump.targetStepId));
                    break;

                case VNIfStep iff:
                    AddIfValid(string.IsNullOrWhiteSpace(iff.trueStepId) ? ResolveLinearNext(stepIndex) : ResolveStepId(iff.trueStepId));
                    AddIfValid(string.IsNullOrWhiteSpace(iff.falseStepId) ? ResolveLinearNext(stepIndex) : ResolveStepId(iff.falseStepId));
                    break;

                case VNChoiceStep choice:
                    if (choice.options != null)
                    {
                        for (int i = 0; i < choice.options.Count; i++)
                        {
                            var opt = choice.options[i];
                            if (opt == null) continue;
                            AddIfValid(ResolveStepId(opt.nextStepId));
                        }
                    }
                    break;
            }

            return result;
        }

        private float GetSubtreeWidth(int stepIndex, HashSet<int> guard = null)
        {
            if (_chapter?.steps == null) return NodeWidth;
            if (stepIndex < 0 || stepIndex >= _chapter.steps.Count) return NodeWidth;

            guard ??= new HashSet<int>();
            if (guard.Contains(stepIndex)) return NodeWidth;

            if (_subtreeWidthCache.TryGetValue(stepIndex, out float cached))
                return cached;

            guard.Add(stepIndex);

            var children = GetLayoutChildren(stepIndex);
            if (children.Count == 0)
            {
                _subtreeWidthCache[stepIndex] = NodeWidth;
                return NodeWidth;
            }

            if (children.Count == 1)
            {
                float one = Mathf.Max(NodeWidth, GetSubtreeWidth(children[0], guard));
                _subtreeWidthCache[stepIndex] = one;
                return one;
            }

            float total = 0f;
            for (int i = 0; i < children.Count; i++)
            {
                total += GetSubtreeWidth(children[i], guard);
                if (i < children.Count - 1)
                    total += HorizontalNodeSpacing;
            }

            total = Mathf.Max(NodeWidth, total);
            _subtreeWidthCache[stepIndex] = total;
            return total;
        }

        private float GetNodeHeightForStep(int stepIndex)
        {
            var outputDefs = BuildPortDefinitionsForStep(stepIndex);
            return Mathf.Max(BaseNodeHeight, 58f + outputDefs.Count * PortSpacing);
        }

        private void RebuildGraph(bool forceAutoLayout)
        {
            _nodes.Clear();
            _nodeByStep.Clear();
            _stepIndexById.Clear();
            _reachableSteps.Clear();
            _incomingRefs.Clear();
            _danglingFromSteps.Clear();

            if (_chapter?.steps == null)
            {
                _canvasRect = new Rect(0, 0, 1600, 1200);
                return;
            }

            for (int i = 0; i < _chapter.steps.Count; i++)
            {
                var s = _chapter.steps[i];
                if (s != null && !string.IsNullOrWhiteSpace(s.id))
                    _stepIndexById[s.id] = i;
            }

            AnalyzeGraphProblems();

            var visible = BuildVisibleStepSet();
            if (_showOnlyReachable)
                visible.RemoveWhere(i => !_reachableSteps.Contains(i));

            if (visible.Count == 0)
            {
                _canvasRect = new Rect(0, 0, 1600, 1200);
                return;
            }

            var roots = BuildRoots(visible);
            var depthOf = new Dictionary<int, int>();
            var placed = new HashSet<int>();

            foreach (var r in roots)
                TraverseForLayout(r, 0, depthOf, placed, visible);

            foreach (var i in visible)
                if (!placed.Contains(i))
                    TraverseForLayout(i, 0, depthOf, placed, visible);

            var rowsByDepth = new Dictionary<int, int>();

            foreach (var si in visible.OrderBy(i => depthOf.TryGetValue(i, out var d) ? d : 0).ThenBy(i => i))
            {
                int depth = depthOf.TryGetValue(si, out var dd) ? dd : 0;
                if (!rowsByDepth.ContainsKey(depth)) rowsByDepth[depth] = 0;

                var step = _chapter.steps[si];
                var outputDefs = BuildPortDefinitionsForStep(si);
                float height = Mathf.Max(BaseNodeHeight, 58f + outputDefs.Count * PortSpacing);

                Vector2 pos;
                if (!forceAutoLayout && step != null && !string.IsNullOrWhiteSpace(step.id) && _manualNodePositions.TryGetValue(step.id, out var manual))
                {
                    pos = manual;
                }
                else
                {
                    float x = CanvasPadding + rowsByDepth[depth] * (NodeWidth + HorizontalNodeSpacing);
                    float y = CanvasPadding + depth * DepthSpacing;
                    pos = new Vector2(x, y);
                }

                rowsByDepth[depth]++;

                var node = new GraphNode
                {
                    stepIndex = si,
                    step = step,
                    depth = depth,
                    row = rowsByDepth[depth] - 1,
                    rect = new Rect(pos.x, pos.y, NodeWidth, height)
                };

                node.input = new GraphPort
                {
                    kind = GraphPortKind.Input,
                    stepIndex = si,
                    label = "in",
                    rect = new Rect(
                        node.rect.center.x - PortSize * 0.5f,
                        node.rect.y - PortSize * 0.5f,
                        PortSize,
                        PortSize)
                };

                for (int p = 0; p < outputDefs.Count; p++)
                {
                    var def = outputDefs[p];

                    float spacing = 22f;
                    float totalWidth = (outputDefs.Count - 1) * spacing;
                    float startX = node.rect.center.x - totalWidth * 0.5f;

                    def.rect = new Rect(
                        startX + p * spacing - PortSize * 0.5f,
                        node.rect.yMax - PortSize * 0.5f,
                        PortSize,
                        PortSize);

                    node.outputs.Add(def);
                }

                _nodes.Add(node);
                _nodeByStep[si] = node;
            }

            float maxX = _nodes.Count > 0 ? _nodes.Max(n => n.rect.xMax) + CanvasPadding : 1600f;
            float maxY = _nodes.Count > 0 ? _nodes.Max(n => n.rect.yMax) + CanvasPadding : 1200f;
            _canvasRect = new Rect(0, 0, Mathf.Max(maxX, 1600f), Mathf.Max(maxY, 1200f));
        }

        private void AnalyzeGraphProblems()
        {
            if (_chapter?.steps == null || _chapter.steps.Count == 0) return;

            for (int i = 0; i < _chapter.steps.Count; i++)
                _incomingRefs[i] = 0;

            var allEdges = BuildAllEdges(includeCollapsedBranches: true);

            foreach (var e in allEdges)
            {
                if (e.to.HasValue && e.to.Value >= 0 && e.to.Value < _chapter.steps.Count)
                    _incomingRefs[e.to.Value]++;

                if (!e.to.HasValue)
                    _danglingFromSteps.Add(e.from);
            }

            TraverseReachableFrom(0, new HashSet<int>(), includeCollapsedBranches: true);
        }

        private void TraverseReachableFrom(int stepIndex, HashSet<int> guard, bool includeCollapsedBranches)
        {
            if (_chapter == null || _chapter.steps == null) return;
            if (stepIndex < 0 || stepIndex >= _chapter.steps.Count) return;
            if (guard.Contains(stepIndex)) return;

            guard.Add(stepIndex);
            _reachableSteps.Add(stepIndex);

            foreach (var edge in BuildStepEdgeInfos(stepIndex, includeCollapsedBranches))
                if (edge.to.HasValue)
                    TraverseReachableFrom(edge.to.Value, guard, includeCollapsedBranches);
        }

        private HashSet<int> BuildVisibleStepSet()
        {
            var set = new HashSet<int>();
            if (_chapter?.steps == null) return set;

            string q = (_search ?? "").Trim().ToLowerInvariant();

            for (int i = 0; i < _chapter.steps.Count; i++)
            {
                var s = _chapter.steps[i];
                if (s == null) continue;
                if (!PassFilter(s)) continue;

                if (!string.IsNullOrEmpty(q))
                {
                    string hay = $"{s.id} {s.label} {ShortPreview(s)}".ToLowerInvariant();
                    if (!hay.Contains(q)) continue;
                }

                set.Add(i);
            }

            return set;
        }

        private List<int> BuildRoots(HashSet<int> visible)
        {
            var roots = new List<int>();
            if (_chapter?.steps == null || _chapter.steps.Count == 0) return roots;

            if (visible.Contains(0))
                roots.Add(0);

            var incoming = new HashSet<int>();
            foreach (var edge in BuildAllEdges(includeCollapsedBranches: false))
                if (edge.to.HasValue && visible.Contains(edge.to.Value))
                    incoming.Add(edge.to.Value);

            foreach (var si in visible)
                if (!incoming.Contains(si) && !roots.Contains(si))
                    roots.Add(si);

            return roots;
        }

        private void TraverseForLayout(int stepIndex, int depth, Dictionary<int, int> depthOf, HashSet<int> placed, HashSet<int> visible)
        {
            if (!visible.Contains(stepIndex)) return;

            if (placed.Contains(stepIndex))
            {
                if (!depthOf.ContainsKey(stepIndex) || depth > depthOf[stepIndex])
                    depthOf[stepIndex] = depth;
                return;
            }

            placed.Add(stepIndex);
            depthOf[stepIndex] = depth;

            foreach (var edge in BuildStepEdgeInfos(stepIndex, includeCollapsedBranches: false))
                if (edge.to.HasValue && visible.Contains(edge.to.Value))
                    TraverseForLayout(edge.to.Value, depth + 1, depthOf, placed, visible);
        }

        private List<EdgeInfo> BuildAllEdges(bool includeCollapsedBranches)
        {
            var result = new List<EdgeInfo>();
            if (_chapter?.steps == null) return result;

            for (int i = 0; i < _chapter.steps.Count; i++)
                result.AddRange(BuildStepEdgeInfos(i, includeCollapsedBranches));

            return result;
        }

        private List<EdgeInfo> BuildStepEdgeInfos(int stepIndex, bool includeCollapsedBranches)
        {
            var result = new List<EdgeInfo>();
            if (_chapter?.steps == null) return result;
            if (stepIndex < 0 || stepIndex >= _chapter.steps.Count) return result;

            var s = _chapter.steps[stepIndex];
            if (s == null) return result;

            bool collapseForThisNode = _collapseSecondaryBranches && !includeCollapsedBranches && stepIndex != _selectedStepIndex;

            switch (s)
            {
                case VNLineStep line:
                    result.Add(new EdgeInfo
                    {
                        from = stepIndex,
                        to = ResolveExplicitOrLinearNext(stepIndex, line.nextStepId),
                        portKind = GraphPortKind.LinearNext,
                        label = "next"
                    });
                    break;

                case VNCommandStep cmd:
                    result.Add(new EdgeInfo
                    {
                        from = stepIndex,
                        to = ResolveExplicitOrLinearNext(stepIndex, cmd.nextStepId),
                        portKind = GraphPortKind.LinearNext,
                        label = "next"
                    });
                    break;

                case VNJumpStep jump:
                    result.Add(new EdgeInfo
                    {
                        from = stepIndex,
                        to = ResolveStepId(jump.targetStepId),
                        portKind = GraphPortKind.JumpTarget,
                        label = "jump"
                    });
                    break;

                case VNIfStep iff:
                {
                    int? t = string.IsNullOrWhiteSpace(iff.trueStepId) ? ResolveLinearNext(stepIndex) : ResolveStepId(iff.trueStepId);
                    int? f = string.IsNullOrWhiteSpace(iff.falseStepId) ? ResolveLinearNext(stepIndex) : ResolveStepId(iff.falseStepId);

                    result.Add(new EdgeInfo
                    {
                        from = stepIndex,
                        to = t,
                        portKind = GraphPortKind.IfTrue,
                        label = "true"
                    });

                    if (!collapseForThisNode || stepIndex == _selectedStepIndex)
                    {
                        result.Add(new EdgeInfo
                        {
                            from = stepIndex,
                            to = f,
                            portKind = GraphPortKind.IfFalse,
                            label = "false"
                        });
                    }
                    break;
                }

                case VNChoiceStep choice:
                {
                    if (choice.options == null || choice.options.Count == 0) break;

                    int count = choice.options.Count;
                    int showCount = collapseForThisNode ? Mathf.Min(1, count) : count;

                    for (int i = 0; i < showCount; i++)
                    {
                        var opt = choice.options[i];
                        string label = string.IsNullOrWhiteSpace(opt.text) ? $"option {i + 1}" : $"option: {Trim(opt.text)}";

                        result.Add(new EdgeInfo
                        {
                            from = stepIndex,
                            to = ResolveStepId(opt.nextStepId),
                            portKind = GraphPortKind.ChoiceOption,
                            optionIndex = i,
                            label = label
                        });
                    }
                    break;
                }

                case VNEndStep:
                    break;

                default:
                    result.Add(new EdgeInfo
                    {
                        from = stepIndex,
                        to = ResolveLinearNext(stepIndex),
                        portKind = GraphPortKind.LinearNext,
                        label = "next"
                    });
                    break;
            }

            return result;
        }

        private List<GraphPort> BuildPortDefinitionsForStep(int stepIndex)
        {
            var edges = BuildStepEdgeInfos(stepIndex, includeCollapsedBranches: false);
            var result = new List<GraphPort>();

            foreach (var e in edges)
            {
                result.Add(new GraphPort
                {
                    stepIndex = stepIndex,
                    kind = e.portKind,
                    optionIndex = e.optionIndex,
                    label = e.label
                });
            }

            return result;
        }

        private int? ResolveTargetForPort(GraphPort port)
        {
            if (_chapter?.steps == null || port == null) return null;
            if (port.stepIndex < 0 || port.stepIndex >= _chapter.steps.Count) return null;

            var step = _chapter.steps[port.stepIndex];
            if (step == null) return null;

            switch (port.kind)
            {
                case GraphPortKind.LinearNext:
                    if (step is VNLineStep line)
                        return ResolveExplicitOrLinearNext(port.stepIndex, line.nextStepId);
                    if (step is VNCommandStep cmd)
                        return ResolveExplicitOrLinearNext(port.stepIndex, cmd.nextStepId);
                    return ResolveLinearNext(port.stepIndex);

                case GraphPortKind.JumpTarget:
                    return step is VNJumpStep jump ? ResolveStepId(jump.targetStepId) : null;

                case GraphPortKind.IfTrue:
                    if (step is VNIfStep iffT)
                        return string.IsNullOrWhiteSpace(iffT.trueStepId) ? ResolveLinearNext(port.stepIndex) : ResolveStepId(iffT.trueStepId);
                    break;

                case GraphPortKind.IfFalse:
                    if (step is VNIfStep iffF)
                        return string.IsNullOrWhiteSpace(iffF.falseStepId) ? ResolveLinearNext(port.stepIndex) : ResolveStepId(iffF.falseStepId);
                    break;

                case GraphPortKind.ChoiceOption:
                    if (step is VNChoiceStep ch &&
                        ch.options != null &&
                        port.optionIndex >= 0 &&
                        port.optionIndex < ch.options.Count)
                        return ResolveStepId(ch.options[port.optionIndex].nextStepId);
                    break;
            }

            return null;
        }

        private int? ResolveLinearNext(int stepIndex)
        {
            if (_chapter?.steps == null) return null;
            int next = stepIndex + 1;
            return next >= 0 && next < _chapter.steps.Count ? next : null;
        }

        private int? ResolveExplicitOrLinearNext(int stepIndex, string nextStepId)
        {
            if (!string.IsNullOrWhiteSpace(nextStepId))
                return ResolveStepId(nextStepId);

            return ResolveLinearNext(stepIndex);
        }

        private int? ResolveStepId(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            return _stepIndexById.TryGetValue(id, out var idx) ? idx : null;
        }

        private bool IsUnreachable(int stepIndex) => !_reachableSteps.Contains(stepIndex);
        private bool IsOrphan(int stepIndex) => stepIndex != 0 && _incomingRefs.TryGetValue(stepIndex, out var c) && c == 0;
        private bool IsDangling(int stepIndex) => _danglingFromSteps.Contains(stepIndex);

        private bool PassFilter(VNChapterStep s)
        {
            return _filter switch
            {
                StepFilter.All => true,
                StepFilter.Line => s is VNLineStep,
                StepFilter.Choice => s is VNChoiceStep,
                StepFilter.If => s is VNIfStep,
                StepFilter.Command => s is VNCommandStep,
                StepFilter.Jump => s is VNJumpStep,
                StepFilter.End => s is VNEndStep,
                _ => true
            };
        }

        private string BuildNodeText(int index, VNChapterStep s)
        {
            string type = s.GetType().Name.Replace("VN", "").Replace("Step", "");
            string preview = Escape(ShortPreview(s));
            string label = string.IsNullOrWhiteSpace(s.label) ? "" : $"\n<b>{Escape(s.label.Trim())}</b>";
            string extra = "";

            if (s is VNChoiceStep ch && ch.options != null && ch.options.Count > 1 && _collapseSecondaryBranches && _selectedStepIndex != index)
                extra = $"\n<color=#BBBBBB>+ {ch.options.Count - 1} hidden option(s)</color>";

            if (s is VNIfStep && _collapseSecondaryBranches && _selectedStepIndex != index)
                extra += "\n<color=#BBBBBB>false hidden</color>";

            return $"<b>{index:000} [{type}]</b>\n{preview}{label}{extra}";
        }

        private void DrawInspectorPanel()
        {
            using (new EditorGUILayout.VerticalScope())
            {
                if (_chapter == null) return;

                if (_selectedStepIndex < 0 || _selectedStepIndex >= _chapter.steps.Count || _chapter.steps[_selectedStepIndex] == null)
                {
                    EditorGUILayout.HelpBox("Select a node in the graph.", MessageType.Info);
                    return;
                }

                using (new EditorGUILayout.VerticalScope("box"))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Insert Line", GUILayout.Width(90))) InsertAfter(_selectedStepIndex, typeof(VNLineStep));
                        if (GUILayout.Button("Insert Choice", GUILayout.Width(100))) InsertAfter(_selectedStepIndex, typeof(VNChoiceStep));
                        if (GUILayout.Button("Insert If", GUILayout.Width(80))) InsertAfter(_selectedStepIndex, typeof(VNIfStep));
                        if (GUILayout.Button("Insert Cmd", GUILayout.Width(90))) InsertAfter(_selectedStepIndex, typeof(VNCommandStep));
                        if (GUILayout.Button("Insert Jump", GUILayout.Width(95))) InsertAfter(_selectedStepIndex, typeof(VNJumpStep));
                        if (GUILayout.Button("Insert End", GUILayout.Width(85))) InsertAfter(_selectedStepIndex, typeof(VNEndStep));
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.FlexibleSpace();
                        EditorGUILayout.LabelField($"Index: {_selectedStepIndex}", GUILayout.Width(100));
                        if (GUILayout.Button("Set As First", GUILayout.Width(95))) SetSelectedAsFirst();
                        if (GUILayout.Button("Delete", GUILayout.Width(80))) DeleteSelected();
                    }
                }

                EditorGUILayout.Space(4);
                DrawStepWarnings(_selectedStepIndex);

                _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll);
                DrawQuickLinkTools(_selectedStepIndex);
                EditorGUILayout.Space(8);
                DrawStepInspector(_selectedStepIndex);
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawStepWarnings(int stepIndex)
        {
            if (IsDangling(stepIndex))
                EditorGUILayout.HelpBox("This step has one or more broken/dangling outgoing links.", MessageType.Error);

            if (IsUnreachable(stepIndex))
                EditorGUILayout.HelpBox("This step is unreachable from chapter start.", MessageType.Warning);

            if (IsOrphan(stepIndex))
                EditorGUILayout.HelpBox("No other step references this node.", MessageType.Info);
        }

        private void DrawQuickLinkTools(int stepIndex)
        {
            if (_chapter == null || stepIndex < 0 || stepIndex >= _chapter.steps.Count) return;

            var step = _chapter.steps[stepIndex];
            if (step == null) return;

            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("Quick tools", EditorStyles.boldLabel);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Child Line", GUILayout.Width(90))) AddChildStep(typeof(VNLineStep));
                    if (GUILayout.Button("Child Choice", GUILayout.Width(95))) AddChildStep(typeof(VNChoiceStep));
                    if (GUILayout.Button("Child If", GUILayout.Width(75))) AddChildStep(typeof(VNIfStep));
                    if (GUILayout.Button("Child Cmd", GUILayout.Width(85))) AddChildStep(typeof(VNCommandStep));
                    if (GUILayout.Button("Child Jump", GUILayout.Width(90))) AddChildStep(typeof(VNJumpStep));
                    if (GUILayout.Button("Child End", GUILayout.Width(80))) AddChildStep(typeof(VNEndStep));
                }

                EditorGUILayout.Space(6);

                if (step is VNChoiceStep choice)
                {
                    choice.options ??= new List<VNChoiceOption>();

                    for (int i = 0; i < choice.options.Count; i++)
                    {
                        var opt = choice.options[i];
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            string label = string.IsNullOrWhiteSpace(opt.text) ? $"Option #{i + 1}" : Trim(opt.text);
                            EditorGUILayout.LabelField(label);

                            if (GUILayout.Button("Create target", GUILayout.Width(100)))
                            {
                                int newIndex = AddStepAtEndInternal(typeof(VNLineStep));
                                choice.options[i].nextStepId = _chapter.steps[newIndex].id;
                                _selectedStepIndex = newIndex;
                                EditorUtility.SetDirty(_chapter);
                                RebuildGraph(forceAutoLayout: false);
                            }

                            if (GUILayout.Button("Ping target", GUILayout.Width(90)))
                            {
                                int? t = ResolveStepId(opt.nextStepId);
                                if (t.HasValue) _selectedStepIndex = t.Value;
                            }
                        }
                    }
                }
                else if (step is VNIfStep iff)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("True branch");
                        if (GUILayout.Button("Create target", GUILayout.Width(100)))
                        {
                            int newIndex = AddStepAtEndInternal(typeof(VNLineStep));
                            iff.trueStepId = _chapter.steps[newIndex].id;
                            _selectedStepIndex = newIndex;
                            EditorUtility.SetDirty(_chapter);
                            RebuildGraph(forceAutoLayout: false);
                        }
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("False branch");
                        if (GUILayout.Button("Create target", GUILayout.Width(100)))
                        {
                            int newIndex = AddStepAtEndInternal(typeof(VNLineStep));
                            iff.falseStepId = _chapter.steps[newIndex].id;
                            _selectedStepIndex = newIndex;
                            EditorUtility.SetDirty(_chapter);
                            RebuildGraph(forceAutoLayout: false);
                        }
                    }
                }
                else if (step is VNJumpStep jump)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("Jump target");
                        if (GUILayout.Button("Create target", GUILayout.Width(100)))
                        {
                            int newIndex = AddStepAtEndInternal(typeof(VNLineStep));
                            jump.targetStepId = _chapter.steps[newIndex].id;
                            _selectedStepIndex = newIndex;
                            EditorUtility.SetDirty(_chapter);
                            RebuildGraph(forceAutoLayout: false);
                        }
                    }
                }
                else if (step is VNLineStep || step is VNCommandStep)
                {
                    EditorGUILayout.HelpBox("Line / Command support explicit nextStepId. Set it in inspector or drag from port.", MessageType.None);
                }
            }
        }

        private void ConnectPortToStep(GraphPort port, int targetStepIndex)
        {
            if (_chapter == null || _chapter.steps == null) return;
            if (port == null) return;
            if (port.stepIndex < 0 || port.stepIndex >= _chapter.steps.Count) return;
            if (targetStepIndex < 0 || targetStepIndex >= _chapter.steps.Count) return;

            Undo.RecordObject(_chapter, "Connect VN Steps");

            string targetId = _chapter.steps[targetStepIndex].id;
            var step = _chapter.steps[port.stepIndex];

            switch (port.kind)
            {
                case GraphPortKind.LinearNext:
                    if (step is VNLineStep line) line.nextStepId = targetId;
                    else if (step is VNCommandStep cmd) cmd.nextStepId = targetId;
                    break;

                case GraphPortKind.JumpTarget:
                    if (step is VNJumpStep jump) jump.targetStepId = targetId;
                    break;

                case GraphPortKind.IfTrue:
                    if (step is VNIfStep iffT) iffT.trueStepId = targetId;
                    break;

                case GraphPortKind.IfFalse:
                    if (step is VNIfStep iffF) iffF.falseStepId = targetId;
                    break;

                case GraphPortKind.ChoiceOption:
                    if (step is VNChoiceStep choice &&
                        choice.options != null &&
                        port.optionIndex >= 0 &&
                        port.optionIndex < choice.options.Count)
                        choice.options[port.optionIndex].nextStepId = targetId;
                    break;
            }

            EditorUtility.SetDirty(_chapter);
            SaveLayoutState();
        }

        private void CreateTargetFromDraggedPort(GraphPort port)
        {
            if (_chapter == null || port == null) return;

            Undo.RecordObject(_chapter, "Create VN Target Step");

            int newIndex = AddStepAtEndInternal(typeof(VNLineStep));
            ConnectPortToStep(port, newIndex);
            _selectedStepIndex = newIndex;
            EditorUtility.SetDirty(_chapter);
        }

        private void AddStepAtEnd(Type type)
        {
            int idx = AddStepAtEndInternal(type);
            _selectedStepIndex = idx;
            _multiSelection.Clear();
            _multiSelection.Add(idx);
            RebuildGraph(forceAutoLayout: false);
            SaveLayoutState();
        }

        private int AddStepAtEndInternal(Type type)
        {
            if (_chapter == null) return -1;

            Undo.RecordObject(_chapter, "Add VN Step");
            var step = CreateStepInstance(type);
            _chapter.steps.Add(step);
            EditorUtility.SetDirty(_chapter);
            return _chapter.steps.Count - 1;
        }

        private void InsertAfter(int stepIndex, Type type)
        {
            int idx = InsertAfterInternal(stepIndex, type);
            _selectedStepIndex = idx;
            _multiSelection.Clear();
            _multiSelection.Add(idx);
            RebuildGraph(forceAutoLayout: false);
            SaveLayoutState();
        }

        private int InsertAfterInternal(int stepIndex, Type type)
        {
            if (_chapter == null) return -1;
            if (stepIndex < 0 || stepIndex >= _chapter.steps.Count) return -1;

            Undo.RecordObject(_chapter, "Insert VN Step");
            var step = CreateStepInstance(type);
            _chapter.steps.Insert(stepIndex + 1, step);
            EditorUtility.SetDirty(_chapter);
            return stepIndex + 1;
        }

        private void AddChildStep(Type type)
        {
            if (_chapter == null) return;
            if (_selectedStepIndex < 0 || _selectedStepIndex >= _chapter.steps.Count) return;

            Undo.RecordObject(_chapter, "Add VN Child Step");

            int insertIndex = _selectedStepIndex + 1;
            var newStep = CreateStepInstance(type);
            _chapter.steps.Insert(insertIndex, newStep);

            var parent = _chapter.steps[_selectedStepIndex];

            switch (parent)
            {
                case VNChoiceStep choice:
                    choice.options ??= new List<VNChoiceOption>();
                    choice.options.Add(new VNChoiceOption
                    {
                        text = "Новый вариант",
                        nextStepId = newStep.id
                    });
                    break;

                case VNIfStep iff:
                    if (string.IsNullOrWhiteSpace(iff.trueStepId))
                        iff.trueStepId = newStep.id;
                    else if (string.IsNullOrWhiteSpace(iff.falseStepId))
                        iff.falseStepId = newStep.id;
                    break;

                case VNJumpStep jump:
                    jump.targetStepId = newStep.id;
                    break;

                case VNLineStep line:
                    if (string.IsNullOrWhiteSpace(line.nextStepId))
                        line.nextStepId = newStep.id;
                    break;

                case VNCommandStep cmd:
                    if (string.IsNullOrWhiteSpace(cmd.nextStepId))
                        cmd.nextStepId = newStep.id;
                    break;
            }

            EditorUtility.SetDirty(_chapter);
            _selectedStepIndex = insertIndex;
            _multiSelection.Clear();
            _multiSelection.Add(insertIndex);
            RebuildGraph(forceAutoLayout: false);
            SaveLayoutState();
        }

        private VNChapterStep CreateStepInstance(Type type)
        {
            var step = (VNChapterStep)Activator.CreateInstance(type);
            step.id = Guid.NewGuid().ToString("N");
            step.label = "";

            if (step is VNCommandStep cmd && cmd.command == null)
                cmd.command = new VNSetBackgroundCommand();

            return step;
        }

        private void SetSelectedAsFirst()
        {
            if (_chapter == null) return;
            if (_selectedStepIndex <= 0 || _selectedStepIndex >= _chapter.steps.Count) return;

            Undo.RecordObject(_chapter, "Set VN Step As First");

            var step = _chapter.steps[_selectedStepIndex];
            _chapter.steps.RemoveAt(_selectedStepIndex);
            _chapter.steps.Insert(0, step);

            EditorUtility.SetDirty(_chapter);

            _selectedStepIndex = 0;
            _multiSelection.Clear();
            _multiSelection.Add(0);

            RebuildGraph(forceAutoLayout: false);
            SaveLayoutState();
        }

        private void DeleteSelected()
        {
            if (_chapter == null || _chapter.steps == null) return;
            if (_multiSelection.Count == 0 && _selectedStepIndex >= 0)
                _multiSelection.Add(_selectedStepIndex);

            if (_multiSelection.Count == 0) return;

            Undo.RecordObject(_chapter, "Delete VN Step(s)");

            var idsToDelete = _multiSelection
                .Where(i => i >= 0 && i < _chapter.steps.Count)
                .Select(i => _chapter.steps[i]?.id)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet();

            foreach (var s in _chapter.steps)
            {
                if (s == null) continue;

                if (s is VNLineStep l && idsToDelete.Contains(l.nextStepId))
                    l.nextStepId = "";

                if (s is VNCommandStep cmd && idsToDelete.Contains(cmd.nextStepId))
                    cmd.nextStepId = "";

                if (s is VNJumpStep j && idsToDelete.Contains(j.targetStepId))
                    j.targetStepId = "";

                if (s is VNIfStep iff)
                {
                    if (idsToDelete.Contains(iff.trueStepId)) iff.trueStepId = "";
                    if (idsToDelete.Contains(iff.falseStepId)) iff.falseStepId = "";
                }

                if (s is VNChoiceStep c && c.options != null)
                {
                    foreach (var o in c.options)
                        if (o != null && idsToDelete.Contains(o.nextStepId))
                            o.nextStepId = "";
                }
            }

            foreach (var id in idsToDelete)
                _manualNodePositions.Remove(id);

            foreach (int index in _multiSelection.OrderByDescending(i => i))
                if (index >= 0 && index < _chapter.steps.Count)
                    _chapter.steps.RemoveAt(index);

            EditorUtility.SetDirty(_chapter);

            _multiSelection.Clear();
            _selectedStepIndex = Mathf.Clamp(_selectedStepIndex - 1, -1, _chapter.steps.Count - 1);
            RebuildGraph(forceAutoLayout: false);
            SaveLayoutState();
        }

        private void RenumberStepIdsSequential()
        {
            if (_chapter == null || _chapter.steps == null || _chapter.steps.Count == 0)
                return;

            Undo.RecordObject(_chapter, "Renumber VN Step IDs");

            var oldToNew = new Dictionary<string, string>();
            var newPosMap = new Dictionary<string, Vector2>();

            for (int i = 0; i < _chapter.steps.Count; i++)
            {
                var step = _chapter.steps[i];
                if (step == null) continue;

                string oldId = string.IsNullOrWhiteSpace(step.id) ? "" : step.id.Trim();
                string newId = $"step_{(i + 1):000}";

                if (!string.IsNullOrEmpty(oldId))
                {
                    oldToNew[oldId] = newId;
                    if (_manualNodePositions.TryGetValue(oldId, out var pos))
                        newPosMap[newId] = pos;
                }

                step.id = newId;
            }

            for (int i = 0; i < _chapter.steps.Count; i++)
            {
                var step = _chapter.steps[i];
                if (step == null) continue;

                switch (step)
                {
                    case VNLineStep line:
                        line.nextStepId = RemapId(line.nextStepId, oldToNew);
                        break;

                    case VNCommandStep cmd:
                        cmd.nextStepId = RemapId(cmd.nextStepId, oldToNew);
                        break;

                    case VNJumpStep jump:
                        jump.targetStepId = RemapId(jump.targetStepId, oldToNew);
                        break;

                    case VNIfStep iff:
                        iff.trueStepId = RemapId(iff.trueStepId, oldToNew);
                        iff.falseStepId = RemapId(iff.falseStepId, oldToNew);
                        break;

                    case VNChoiceStep choice:
                        if (choice.options != null)
                        {
                            for (int j = 0; j < choice.options.Count; j++)
                            {
                                var opt = choice.options[j];
                                if (opt == null) continue;
                                opt.nextStepId = RemapId(opt.nextStepId, oldToNew);
                            }
                        }
                        break;
                }
            }

            _manualNodePositions.Clear();
            foreach (var kv in newPosMap)
                _manualNodePositions[kv.Key] = kv.Value;

            EditorUtility.SetDirty(_chapter);
            AssetDatabase.SaveAssets();

            RebuildGraph(forceAutoLayout: false);
            SaveLayoutState();
            Repaint();
        }

        private static string RemapId(string value, Dictionary<string, string> oldToNew)
        {
            if (string.IsNullOrWhiteSpace(value)) return value;
            string key = value.Trim();
            return oldToNew.TryGetValue(key, out var mapped) ? mapped : value;
        }

        private void DrawStepInspector(int stepIndex)
        {
            var so = new SerializedObject(_chapter);
            var stepsProp = so.FindProperty("steps");
            var stepProp = stepsProp.GetArrayElementAtIndex(stepIndex);

            var runtime = _chapter.steps[stepIndex];

            EditorGUILayout.LabelField(runtime.GetType().Name, EditorStyles.boldLabel);

            DrawProp(stepProp, "id");
            DrawProp(stepProp, "label");
            DrawProp(stepProp, "disableSkip");
            DrawProp(stepProp, "stopAuto");

            EditorGUILayout.Space(8);

            if (runtime is VNLineStep)
            {
                DrawSpeakerDropdown(stepProp.FindPropertyRelative("speakerId"));
                DrawProp(stepProp, "pose");
                DrawProp(stepProp, "emotion");
                DrawSfxDropdown(stepProp.FindPropertyRelative("sfxId"));
                DrawProp(stepProp, "text");
                DrawProp(stepProp, "addToLog");
                DrawStepIdDropdown("Next", stepProp.FindPropertyRelative("nextStepId"), runtime.id, allowEmptyLinear: true);
            }
            else if (runtime is VNChoiceStep)
            {
                DrawChoice(stepProp, runtime.id);
            }
            else if (runtime is VNIfStep)
            {
                DrawIf(stepProp, runtime.id);
            }
            else if (runtime is VNJumpStep)
            {
                DrawStepIdDropdown("Target", stepProp.FindPropertyRelative("targetStepId"), runtime.id, allowEmptyLinear: false);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Create target step", GUILayout.Width(140)))
                    {
                        int newIndex = AddStepAtEndInternal(typeof(VNLineStep));
                        stepProp.FindPropertyRelative("targetStepId").stringValue = _chapter.steps[newIndex].id;
                        _selectedStepIndex = newIndex;
                    }
                }
            }
            else if (runtime is VNCommandStep)
            {
                DrawCommand(stepProp, stepIndex);
                DrawStepIdDropdown("Next", stepProp.FindPropertyRelative("nextStepId"), runtime.id, allowEmptyLinear: true);
            }
            else if (runtime is VNEndStep)
            {
                EditorGUILayout.HelpBox("EndStep – конец главы.", MessageType.None);
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(_chapter);
            SaveLayoutState();
        }

        private void DrawChoice(SerializedProperty stepProp, string selfId)
        {
            var optionsProp = stepProp.FindPropertyRelative("options");

            if (GUILayout.Button("+ Option", GUILayout.Width(110)))
                optionsProp.arraySize += 1;

            EditorGUILayout.Space(6);

            for (int i = 0; i < optionsProp.arraySize; i++)
            {
                var opt = optionsProp.GetArrayElementAtIndex(i);
                using (new EditorGUILayout.VerticalScope("box"))
                {
                    EditorGUILayout.LabelField($"Option #{i + 1}", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(opt.FindPropertyRelative("text"));

                    DrawStepIdDropdown("Next", opt.FindPropertyRelative("nextStepId"), selfId, allowEmptyLinear: false);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.FlexibleSpace();

                        if (GUILayout.Button("Create Target", GUILayout.Width(110)))
                        {
                            int newIndex = AddStepAtEndInternal(typeof(VNLineStep));
                            opt.FindPropertyRelative("nextStepId").stringValue = _chapter.steps[newIndex].id;
                            _selectedStepIndex = newIndex;
                        }

                        if (GUILayout.Button("Select Target", GUILayout.Width(110)))
                        {
                            string id = opt.FindPropertyRelative("nextStepId").stringValue;
                            int? idx = ResolveStepId(id);
                            if (idx.HasValue) _selectedStepIndex = idx.Value;
                        }
                    }

                    EditorGUILayout.PropertyField(opt.FindPropertyRelative("kind"));
                    EditorGUILayout.PropertyField(opt.FindPropertyRelative("premiumPrice"));
                    EditorGUILayout.PropertyField(opt.FindPropertyRelative("effects"), true);

                    if (GUILayout.Button("Delete Option", GUILayout.Width(130)))
                    {
                        optionsProp.DeleteArrayElementAtIndex(i);
                        break;
                    }
                }
            }

            if (GUILayout.Button("Create quick branch", GUILayout.Width(140)))
            {
                optionsProp.arraySize += 1;
                var opt = optionsProp.GetArrayElementAtIndex(optionsProp.arraySize - 1);
                opt.FindPropertyRelative("text").stringValue = "Новый вариант";

                int newIndex = AddStepAtEndInternal(typeof(VNLineStep));
                opt.FindPropertyRelative("nextStepId").stringValue = _chapter.steps[newIndex].id;
                _selectedStepIndex = newIndex;
            }
        }

        private void DrawIf(SerializedProperty stepProp, string selfId)
        {
            EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("requireAll"));

            var condProp = stepProp.FindPropertyRelative("conditions");
            if (GUILayout.Button("+ Condition", GUILayout.Width(120)))
                condProp.arraySize += 1;

            EditorGUILayout.Space(6);

            for (int i = 0; i < condProp.arraySize; i++)
            {
                var c = condProp.GetArrayElementAtIndex(i);
                using (new EditorGUILayout.VerticalScope("box"))
                {
                    EditorGUILayout.LabelField($"Condition #{i + 1}", EditorStyles.boldLabel);

                    EditorGUILayout.PropertyField(c.FindPropertyRelative("type"));
                    EditorGUILayout.PropertyField(c.FindPropertyRelative("key"));
                    EditorGUILayout.PropertyField(c.FindPropertyRelative("op"));

                    var type = (VNConditionValueType)c.FindPropertyRelative("type").enumValueIndex;
                    if (type == VNConditionValueType.Bool) EditorGUILayout.PropertyField(c.FindPropertyRelative("boolValue"), new GUIContent("Value"));
                    if (type == VNConditionValueType.Int) EditorGUILayout.PropertyField(c.FindPropertyRelative("intValue"), new GUIContent("Value"));
                    if (type == VNConditionValueType.String) EditorGUILayout.PropertyField(c.FindPropertyRelative("stringValue"), new GUIContent("Value"));

                    if (GUILayout.Button("Delete Condition", GUILayout.Width(140)))
                    {
                        condProp.DeleteArrayElementAtIndex(i);
                        break;
                    }
                }
            }

            EditorGUILayout.Space(8);
            DrawStepIdDropdown("True ->", stepProp.FindPropertyRelative("trueStepId"), selfId, allowEmptyLinear: true);
            DrawStepIdDropdown("False ->", stepProp.FindPropertyRelative("falseStepId"), selfId, allowEmptyLinear: true);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Create True Target", GUILayout.Width(140)))
                {
                    int newIndex = AddStepAtEndInternal(typeof(VNLineStep));
                    stepProp.FindPropertyRelative("trueStepId").stringValue = _chapter.steps[newIndex].id;
                    _selectedStepIndex = newIndex;
                }

                if (GUILayout.Button("Create False Target", GUILayout.Width(145)))
                {
                    int newIndex = AddStepAtEndInternal(typeof(VNLineStep));
                    stepProp.FindPropertyRelative("falseStepId").stringValue = _chapter.steps[newIndex].id;
                    _selectedStepIndex = newIndex;
                }
            }
        }

        private void DrawCommand(SerializedProperty stepProp, int stepIndex)
        {
            var cmdProp = stepProp.FindPropertyRelative("command");

            DrawCommandTypePicker(cmdProp);

            EditorGUILayout.Space(6);

            if (cmdProp.managedReferenceValue == null)
            {
                EditorGUILayout.HelpBox("Pick a command type.", MessageType.Info);
                return;
            }

            EditorGUILayout.PropertyField(cmdProp, true);

            if (_project == null || _project.assetDatabase == null || _project.characterDatabase == null) return;

            var runtime = _chapter.steps[stepIndex] as VNCommandStep;
            if (runtime == null || runtime.command == null) return;

            if (runtime.command is VNSetBackgroundCommand)
                DrawBackgroundDropdown(cmdProp.FindPropertyRelative("backgroundId"));

            if (runtime.command is VNPlayMusicCommand)
                DrawMusicDropdown(cmdProp.FindPropertyRelative("musicId"));

            if (runtime.command is VNPlaySfxCommand)
                DrawSfxDropdown(cmdProp.FindPropertyRelative("sfxId"));

            if (runtime.command is VNShowCharacterCommand)
                DrawCharacterDropdown(cmdProp.FindPropertyRelative("characterId"));
        }

        private void DrawSpeakerDropdown(SerializedProperty speakerIdProp)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(speakerIdProp, new GUIContent("SpeakerId"));

                if (_project?.characterDatabase == null) return;

                if (GUILayout.Button("▼", GUILayout.Width(28)))
                {
                    var ids = _project.characterDatabase.Characters
                        .Select(c => (c?.id ?? "").Trim())
                        .Where(s => !string.IsNullOrEmpty(s))
                        .Distinct().OrderBy(s => s).ToList();

                    ids.Insert(0, "");

                    var m = new GenericMenu();
                    foreach (var id in ids)
                    {
                        string label = string.IsNullOrEmpty(id) ? "(Narrator)" : id;
                        m.AddItem(new GUIContent(label), speakerIdProp.stringValue == id, () =>
                        {
                            speakerIdProp.stringValue = id;
                            speakerIdProp.serializedObject.ApplyModifiedProperties();
                            EditorUtility.SetDirty(_chapter);
                        });
                    }
                    m.ShowAsContext();
                }
            }
        }

        private void DrawCharacterDropdown(SerializedProperty idProp)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(idProp);

                if (_project?.characterDatabase == null) return;
                if (!GUILayout.Button("▼", GUILayout.Width(28))) return;

                var ids = _project.characterDatabase.Characters
                    .Select(c => (c?.id ?? "").Trim())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .Distinct().OrderBy(s => s).ToList();

                var m = new GenericMenu();
                foreach (var id in ids)
                {
                    m.AddItem(new GUIContent(id), idProp.stringValue == id, () =>
                    {
                        idProp.stringValue = id;
                        idProp.serializedObject.ApplyModifiedProperties();
                        EditorUtility.SetDirty(_chapter);
                    });
                }
                m.ShowAsContext();
            }
        }

        private void DrawBackgroundDropdown(SerializedProperty idProp)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(idProp, new GUIContent("backgroundId"));

                if (_project?.assetDatabase == null) return;
                if (!GUILayout.Button("▼", GUILayout.Width(28))) return;

                var ids = _project.assetDatabase.backgrounds
                    .Select(e => (e?.id ?? "").Trim())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .Distinct().OrderBy(s => s).ToList();

                var m = new GenericMenu();
                foreach (var id in ids)
                {
                    m.AddItem(new GUIContent(id), idProp.stringValue == id, () =>
                    {
                        idProp.stringValue = id;
                        idProp.serializedObject.ApplyModifiedProperties();
                        EditorUtility.SetDirty(_chapter);
                    });
                }
                m.ShowAsContext();
            }
        }

        private void DrawMusicDropdown(SerializedProperty idProp)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(idProp, new GUIContent("musicId"));

                if (_project?.assetDatabase == null) return;
                if (!GUILayout.Button("▼", GUILayout.Width(28))) return;

                var ids = _project.assetDatabase.music
                    .Select(e => (e?.id ?? "").Trim())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .Distinct().OrderBy(s => s).ToList();

                var m = new GenericMenu();
                foreach (var id in ids)
                {
                    m.AddItem(new GUIContent(id), idProp.stringValue == id, () =>
                    {
                        idProp.stringValue = id;
                        idProp.serializedObject.ApplyModifiedProperties();
                        EditorUtility.SetDirty(_chapter);
                    });
                }
                m.ShowAsContext();
            }
        }

        private void DrawSfxDropdown(SerializedProperty idProp)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(idProp, new GUIContent(idProp.displayName));

                if (_project?.assetDatabase == null) return;
                if (!GUILayout.Button("▼", GUILayout.Width(28))) return;

                var ids = _project.assetDatabase.sfx
                    .Select(e => (e?.id ?? "").Trim())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .Distinct().OrderBy(s => s).ToList();

                ids.Insert(0, "");

                var m = new GenericMenu();
                foreach (var id in ids)
                {
                    string label = string.IsNullOrEmpty(id) ? "(None)" : id;
                    m.AddItem(new GUIContent(label), idProp.stringValue == id, () =>
                    {
                        idProp.stringValue = id;
                        idProp.serializedObject.ApplyModifiedProperties();
                        EditorUtility.SetDirty(_chapter);
                    });
                }
                m.ShowAsContext();
            }
        }

        private void DrawStepIdDropdown(string caption, SerializedProperty idProp, string excludeSelfId, bool allowEmptyLinear)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(idProp, new GUIContent(caption));

                if (!GUILayout.Button("▼", GUILayout.Width(28))) return;

                var refs = BuildStepRefs(excludeSelfId);
                var m = new GenericMenu();

                if (allowEmptyLinear)
                {
                    m.AddItem(new GUIContent("(Next by order)"), string.IsNullOrEmpty(idProp.stringValue), () =>
                    {
                        idProp.stringValue = "";
                        idProp.serializedObject.ApplyModifiedProperties();
                        EditorUtility.SetDirty(_chapter);
                    });
                    m.AddSeparator("");
                }

                foreach (var r in refs)
                {
                    m.AddItem(new GUIContent(r), idProp.stringValue == ExtractId(r), () =>
                    {
                        idProp.stringValue = ExtractId(r);
                        idProp.serializedObject.ApplyModifiedProperties();
                        EditorUtility.SetDirty(_chapter);
                    });
                }

                m.ShowAsContext();
            }
        }

        private List<string> BuildStepRefs(string excludeSelfId)
        {
            var list = new List<string>();
            if (_chapter?.steps == null) return list;

            for (int i = 0; i < _chapter.steps.Count; i++)
            {
                var s = _chapter.steps[i];
                if (s == null || string.IsNullOrWhiteSpace(s.id)) continue;
                if (!string.IsNullOrEmpty(excludeSelfId) && s.id == excludeSelfId) continue;

                string label = string.IsNullOrWhiteSpace(s.label) ? ShortPreview(s) : s.label.Trim();
                string id6 = s.id.Length >= 6 ? s.id.Substring(0, 6) : s.id;
                list.Add($"{i:000}  {label}  ({id6}|{s.id})");
            }

            return list;
        }

        private static string ExtractId(string refLabel)
        {
            int p = refLabel.LastIndexOf('|');
            int e = refLabel.LastIndexOf(')');
            if (p < 0 || e < 0 || e <= p) return "";
            return refLabel.Substring(p + 1, e - p - 1);
        }

        private void DrawCommandTypePicker(SerializedProperty cmdProp)
        {
            Type current = cmdProp.managedReferenceValue?.GetType();

            var types = TypeCache.GetTypesDerivedFrom<VNCommand>()
                .Where(t => !t.IsAbstract && t.GetConstructor(Type.EmptyTypes) != null)
                .OrderBy(t => t.Name)
                .ToList();

            string currentName = current != null ? current.Name : "(None)";

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Command Type", GUILayout.Width(110));
                EditorGUILayout.LabelField(currentName);

                if (GUILayout.Button("Change", GUILayout.Width(80)))
                {
                    var m = new GenericMenu();

                    m.AddItem(new GUIContent("(None)"), current == null, () =>
                    {
                        cmdProp.managedReferenceValue = null;
                        cmdProp.serializedObject.ApplyModifiedProperties();
                        EditorUtility.SetDirty(_chapter);
                    });

                    m.AddSeparator("");

                    foreach (var t in types)
                    {
                        var captured = t;
                        m.AddItem(new GUIContent(captured.Name), current == captured, () =>
                        {
                            cmdProp.managedReferenceValue = Activator.CreateInstance(captured);
                            cmdProp.serializedObject.ApplyModifiedProperties();
                            EditorUtility.SetDirty(_chapter);
                        });
                    }

                    m.ShowAsContext();
                }
            }
        }

        private static void DrawProp(SerializedProperty root, string name)
        {
            var p = root.FindPropertyRelative(name);
            if (p != null) EditorGUILayout.PropertyField(p, true);
        }

        private static string ShortPreview(VNChapterStep s)
        {
            switch (s)
            {
                case VNLineStep l:
                    return Trim($"{(string.IsNullOrWhiteSpace(l.speakerId) ? "Narrator" : l.speakerId)}: {l.text}");
                case VNChoiceStep c:
                    return $"Options: {c.options?.Count ?? 0}";
                case VNIfStep iff:
                    return $"If ({(iff.requireAll ? "AND" : "OR")}): {iff.conditions?.Count ?? 0}";
                case VNCommandStep cmd:
                    return cmd.command != null ? $"Cmd: {cmd.command.GetType().Name.Replace("VN", "").Replace("Command", "")}" : "Cmd: (null)";
                case VNJumpStep j:
                    return $"Jump -> {(string.IsNullOrWhiteSpace(j.targetStepId) ? "(empty)" : j.targetStepId.Substring(0, Mathf.Min(6, j.targetStepId.Length)))}";
                case VNEndStep:
                    return "END";
            }
            return s.GetType().Name;
        }

        private static string Trim(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Replace("\n", " ").Replace("\r", " ");
            return s.Length > 42 ? s.Substring(0, 42) + "…" : s;
        }

        private static string Escape(string s)
        {
            return string.IsNullOrEmpty(s) ? "" : s.Replace("<", "‹").Replace(">", "›");
        }

        private string GetLayoutPrefsKey(VNChapter chapter = null)
        {
            chapter ??= _chapter;
            if (chapter == null) return "";

            string path = AssetDatabase.GetAssetPath(chapter);
            if (string.IsNullOrWhiteSpace(path))
                path = chapter.name;

            return $"VNChapterEditorWindow.Layout::{path}";
        }

        private void SaveLayoutState(VNChapter chapter = null)
        {
            chapter ??= _chapter;
            if (chapter == null) return;

            string key = GetLayoutPrefsKey(chapter);
            if (string.IsNullOrEmpty(key)) return;

            var data = new LayoutSaveData
            {
                zoom = _zoom,
                entries = new List<LayoutEntry>()
            };

            foreach (var kv in _manualNodePositions)
            {
                data.entries.Add(new LayoutEntry
                {
                    stepId = kv.Key,
                    pos = kv.Value
                });
            }

            string json = JsonUtility.ToJson(data);
            EditorPrefs.SetString(key, json);
            _loadedLayoutKey = key;
        }

        private void LoadLayoutState()
        {
            _manualNodePositions.Clear();

            string key = GetLayoutPrefsKey();
            if (string.IsNullOrEmpty(key))
            {
                _loadedLayoutKey = "";
                return;
            }

            _loadedLayoutKey = key;

            if (!EditorPrefs.HasKey(key))
                return;

            string json = EditorPrefs.GetString(key, "");
            if (string.IsNullOrWhiteSpace(json))
                return;

            try
            {
                var data = JsonUtility.FromJson<LayoutSaveData>(json);
                if (data == null) return;

                _zoom = Mathf.Clamp(data.zoom <= 0f ? 1f : data.zoom, MinZoom, MaxZoom);

                if (data.entries != null)
                {
                    foreach (var e in data.entries)
                    {
                        if (e == null || string.IsNullOrWhiteSpace(e.stepId)) continue;
                        _manualNodePositions[e.stepId] = e.pos;
                    }
                }
            }
            catch
            {
                _zoom = 1f;
            }
        }

        private void CreateChapterAssetAndAddToProject()
        {
            if (_project == null)
            {
                EditorUtility.DisplayDialog("Create Chapter", "Assign VNProjectDatabase first.", "OK");
                return;
            }

            string chapterId = string.IsNullOrWhiteSpace(_newChapterId) ? "chapter_new" : _newChapterId.Trim();
            string assetName = string.IsNullOrWhiteSpace(_newChapterAssetName) ? chapterId : _newChapterAssetName.Trim();

            string projectPath = AssetDatabase.GetAssetPath(_project);
            string folder = string.IsNullOrWhiteSpace(projectPath) ? "Assets" : System.IO.Path.GetDirectoryName(projectPath);
            if (string.IsNullOrWhiteSpace(folder))
                folder = "Assets";

            string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{assetName}.asset");

            var chapter = ScriptableObject.CreateInstance<VNChapter>();
            chapter.name = assetName;

            var chapterSO = new SerializedObject(chapter);

            var idProp = chapterSO.FindProperty("id");
            if (idProp != null && idProp.propertyType == SerializedPropertyType.String)
                idProp.stringValue = chapterId;

            var stepsProp = chapterSO.FindProperty("steps");
            if (stepsProp != null && stepsProp.isArray && stepsProp.arraySize == 0)
            {
                stepsProp.arraySize = 1;
                var elem = stepsProp.GetArrayElementAtIndex(0);
                elem.managedReferenceValue = new VNEndStep();

                var idStep = elem.FindPropertyRelative("id");
                if (idStep != null) idStep.stringValue = "step_001";

                var labelStep = elem.FindPropertyRelative("label");
                if (labelStep != null) labelStep.stringValue = "END";
            }

            chapterSO.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.CreateAsset(chapter, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var projectSO = new SerializedObject(_project);
            var chaptersProp = projectSO.FindProperty("chapters");

            if (chaptersProp == null || !chaptersProp.isArray)
            {
                EditorUtility.DisplayDialog(
                    "Create Chapter",
                    "Не найдено поле 'chapters' в VNProjectDatabase. Проверь имя serialized-поля.",
                    "OK");
                Selection.activeObject = chapter;
                _chapter = chapter;
                return;
            }

            bool alreadyExists = false;
            for (int i = 0; i < chaptersProp.arraySize; i++)
            {
                var item = chaptersProp.GetArrayElementAtIndex(i);
                if (item.objectReferenceValue == chapter)
                {
                    alreadyExists = true;
                    break;
                }
            }

            if (!alreadyExists)
            {
                chaptersProp.arraySize += 1;
                chaptersProp.GetArrayElementAtIndex(chaptersProp.arraySize - 1).objectReferenceValue = chapter;
                projectSO.ApplyModifiedProperties();
                EditorUtility.SetDirty(_project);
                AssetDatabase.SaveAssets();
            }

            _chapter = chapter;
            _selectedStepIndex = -1;
            _multiSelection.Clear();
            _manualNodePositions.Clear();
            _zoom = 1f;

            LoadLayoutState();
            RebuildGraph(forceAutoLayout: true);

            Selection.activeObject = chapter;
            EditorGUIUtility.PingObject(chapter);
        }
    }
}
#endif