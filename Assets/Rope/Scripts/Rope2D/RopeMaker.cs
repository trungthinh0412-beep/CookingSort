using UnityEngine;
using System.Collections.Generic;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;

#endif

//[RequireComponent(typeof(Rope))]
public class RopeMaker : MonoBehaviour
{
    [Header("Ends")] public Vector3 end1 = Vector3.left;
    public Vector3 end2 = Vector3.right;
    public FixEndType fixEnd1 = FixEndType.Pivotable;
    public FixEndType fixEnd2 = FixEndType.Pivotable;

    [Header("Midpoints")] public List<Vector3> midPoints = new List<Vector3>();
    [Range(0, 1)] public float smoothness = 0;

    [Header("Props")][Min(0.001f)] public float scale = 1f;
    [Min(-1)] public float spacing = 0;
    public Vector2 jointAngleLimits = new Vector2(-10, 10);
    public List<RopeNode> ropeNodePrefabs;

    public IEnumerable<Vector3> AllLocalPoints =>
        Enumerable.Repeat(end1, 1)
            .Concat(midPoints)
            .Append(end2);

    public IEnumerable<Vector3> AllWorldPoints => AllLocalPoints.Select(p => transform.TransformPoint(p));
    public Rope Rope => GetComponentInParent<Rope>();


    private void OnDrawGizmos()
    {
        var points = AllWorldPoints.ToList();
        if (points.Count < 2) return;

        var baseColor = ColorUtils.lightGreen * new Color(1, 1, 1, 0.75f);
        var pvtColor = ColorUtils.pink * new Color(1, 1, 1, 0.75f);
        var fixedColor = Color.black * new Color(1, 1, 1, 0.75f);

        for (int i = 0; i < points.Count; i++)
        {
            bool isEnd1 = i == 0;
            bool isEnd2 = i == points.Count - 1;
            bool isEnd = isEnd1 || isEnd2;

            if (isEnd1) Gizmos.color = fixEnd1 == FixEndType.Fixed ? fixedColor : fixEnd1 == FixEndType.Pivotable ? pvtColor : baseColor;
            else if (isEnd2) Gizmos.color = fixEnd2 == FixEndType.Fixed ? fixedColor : fixEnd2 == FixEndType.Pivotable ? pvtColor : baseColor;
            else Gizmos.color = baseColor;
            Gizmos.DrawWireSphere(points[i], isEnd ? 0.15f : 0.1f);

            Gizmos.color = baseColor;
            if (i < points.Count - 1) Gizmos.DrawLine(points[i], points[i + 1]);
        }
    }


    public void CreateRope()
    {
        transform.ClearChildrenOfType<RopeNode>(true);
        if (ropeNodePrefabs?.Count <= 0) return;

        var smoothedPoints = CalculateSmoothedLocalPoints();
        int nodeCounter = 0;
        int pointIndex = 0;
        float pointOffset = 0;

        float nodeScale = scale;
        float spacingScale = Mathf.Max(0.1f, 1 + spacing);

        List<RopeNode> nodes = new List<RopeNode>();
        while (pointIndex < smoothedPoints.Count)
        {
            var nodePrefab = ropeNodePrefabs.GetRepeat(nodeCounter);
            nodeCounter++;

            float nodeOccupyLength = nodePrefab.nodeLength * nodeScale * spacingScale;

            float targetOffset = pointOffset + nodeOccupyLength / 2f;
            if (nodeCounter <= 1) targetOffset = 0;

            float currentMaxOffset = (smoothedPoints[pointIndex + 1] - smoothedPoints[pointIndex]).magnitude;
            while (targetOffset > currentMaxOffset)
            {
                targetOffset -= currentMaxOffset;
                pointIndex++;
                if (pointIndex >= smoothedPoints.Count - 1) break;
                currentMaxOffset = (smoothedPoints[pointIndex + 1] - smoothedPoints[pointIndex]).magnitude;
            }

            if (pointIndex >= smoothedPoints.Count - 1) break;

            Vector3 prevPoint = smoothedPoints[pointIndex];
            Vector3 nextPoint = smoothedPoints[pointIndex + 1];
            float ratio = targetOffset / currentMaxOffset;
            var targetPosition = Vector3.Lerp(prevPoint, nextPoint, ratio);
            var node = ObjectUtils.InstantiatePrefab(nodePrefab, transform);
            node.transform.localPosition = targetPosition;
            node.transform.localRotation = Quaternion.Euler(0, 0, Vector2.SignedAngle(Vector3.right, nextPoint - prevPoint));
            node.transform.localScale = Vector3.one * nodeScale;

            var prevNode = nodes.LastOrDefault();
            if (prevNode)
            {
                float nodeJointOffsetX = nodeOccupyLength / nodeScale / 2f;
                float prevNodeJointOffsetX = prevNode.nodeLength * spacingScale / 2f;

                var hingeJoint = node.gameObject.AddComponent<HingeJoint2D>();
                hingeJoint.connectedBody = prevNode.rb;
                hingeJoint.autoConfigureConnectedAnchor = false;
                hingeJoint.useLimits = true;
                hingeJoint.limits = new JointAngleLimits2D() { min = jointAngleLimits.x, max = jointAngleLimits.y };
                hingeJoint.anchor = Vector2.left * nodeJointOffsetX;
                hingeJoint.connectedAnchor = Vector2.right * prevNodeJointOffsetX;
            }

            nodes.Add(node);
            pointOffset = targetOffset + nodeOccupyLength / 2f;
        }


        void SetupEnd(
            bool isFirst,
            RopeNode node,
            FixEndType fet)
        {
            AnchoredJoint2D anchorJoint = null;
            if (fet == FixEndType.Fixed) anchorJoint = node.gameObject.AddComponent<FixedJoint2D>();
            else if (fet == FixEndType.Pivotable) anchorJoint = node.gameObject.AddComponent<HingeJoint2D>();

            if (anchorJoint)
            {
                float nodeJointOffsetX = node.nodeLength / 2f;
                anchorJoint.autoConfigureConnectedAnchor = true;
                anchorJoint.anchor = (isFirst ? Vector2.left : Vector2.right) * nodeJointOffsetX;
            }
        }

        if (nodes.Count > 0) SetupEnd(true, nodes[0], fixEnd1);
        if (nodes.Count > 1) SetupEnd(false, nodes[nodes.Count - 1], fixEnd2);

        var rope = Rope;
        if (rope)
        {
            rope.nodeLineLengthFactor = scale * spacingScale;
            rope.OnNodesCreated();
            rope.SetLineWidth(scale);
        }
    }

    List<Vector3> CalculateSmoothedLocalPoints()
    {
        var points = AllLocalPoints.ToList();
        float smoothAngle = Mathf.Lerp(120, 10, Mathf.Sqrt(smoothness));
        int maxIterations = Mathf.RoundToInt(Mathf.Lerp(0, 4, smoothness));
        int iterations = 0;
        while (iterations < maxIterations && points.Count > 2)
        {
            iterations++;
            Dictionary<int, List<Vector3>> offsetMap = new Dictionary<int, List<Vector3>>();
            Dictionary<int, Vector3> insertMap = new Dictionary<int, Vector3>();
            for (int i = 1; i < points.Count - 1; i++)
            {
                var pPrev = points[i - 1];
                var pThis = points[i];
                var pNext = points[i + 1];
                var d1 = pThis - pPrev;
                var d2 = pNext - pThis;
                if (Vector3.Angle(d1, d2) > smoothAngle)
                {
                    if (!insertMap.TryGetValue(i, out var pPrevNew))
                    {
                        pPrevNew = (pPrev + pThis) / 2f;
                        insertMap[i] = pPrevNew;
                    }

                    if (!insertMap.TryGetValue(i + 1, out var pNextNew))
                    {
                        pNextNew = (pNext + pThis) / 2f;
                        insertMap[i + 1] = pNextNew;
                    }

                    var pThisTarget = (pPrevNew + pNextNew + pThis) / 3f;
                    if (!offsetMap.TryGetValue(i, out var thisOffsets)) thisOffsets = new List<Vector3>();
                    thisOffsets.Add(pThisTarget - pThis);
                    offsetMap[i] = thisOffsets;
                }
            }

            foreach (var kvp in offsetMap)
            {
                var index = kvp.Key;
                var offsets = kvp.Value;
                if (index >= 1 && index < points.Count - 1) points[index] += ObjectUtils.AverageVectors(offsets);
            }

            foreach (var kvp in insertMap.OrderByDescending(kvp => kvp.Key))
            {
                var index = kvp.Key;
                var pos = kvp.Value;
                points.Insert(index, pos);
            }

            if (offsetMap.Count == 0 && insertMap.Count == 0) break;
        }

        return points;
    }


    public enum FixEndType
    {
        Free,
        Fixed,
        Pivotable
    }
}


#if UNITY_EDITOR
[CustomEditor(typeof(RopeMaker))]
[CanEditMultipleObjects]
public class RopeMakerEditor : Editor
{
    bool basicFoldout;
    bool ropeFoldout;
    bool editingRope;

    private void OnEnable()
    {
        Undo.undoRedoPerformed -= OnUndoRedoPerformed;
        Undo.undoRedoPerformed += OnUndoRedoPerformed;
    }

    private void OnDisable() { Undo.undoRedoPerformed -= OnUndoRedoPerformed; }

    private void OnUndoRedoPerformed() { TryCreateRope(target as RopeMaker, false); }


    public static void TryCreateRope(
        RopeMaker rm,
        bool withLog = true)
    {
        string log = "";
        if (rm)
        {
            bool can = true;
            var prefabInstanceStatus = PrefabUtility.GetPrefabInstanceStatus(rm);
            if (prefabInstanceStatus == PrefabInstanceStatus.Connected || prefabInstanceStatus == PrefabInstanceStatus.MissingAsset)
            {
                log = "Can't create rope that is connected to a prefab";
                can = false;
            }

            var prefabType = PrefabUtility.GetPrefabAssetType(rm);
            if (prefabType != PrefabAssetType.NotAPrefab)
            {
                log = "Can't create rope that is a prefab";
                can = false;
            }

            if (can) rm.CreateRope();
        }

        if (withLog && !string.IsNullOrEmpty(log)) Debug.LogError(log, rm);
    }


    public override void OnInspectorGUI()
    {
        if (serializedObject.isEditingMultipleObjects || Application.isPlaying)
        {
            EditorUtilEx.MiniBoxedSection(string.Format("<color={0}><b>WARNING</b></color>", ColorUtils.RichTextTag(Color.red)), () => { EditorGUILayout.LabelField("You can not edit multiple ropeMakers at once"); });
            base.OnInspectorGUI();
            return;
        }

        var ropeMaker = target as RopeMaker;

        var prefabInstanceStatus = PrefabUtility.GetPrefabInstanceStatus(ropeMaker);
        if (prefabInstanceStatus == PrefabInstanceStatus.Connected || prefabInstanceStatus == PrefabInstanceStatus.MissingAsset)
        {
            EditorUtilEx.MiniBoxedSection(string.Format("<color={0}><b>WARNING</b></color>", ColorUtils.RichTextTag(Color.red)),
                () =>
                {
                    EditorGUILayout.LabelField("To edit this RopeMaker, you need to disconnect/unpack prefab. Or press start editing below.");
                    if (EditorUtilEx.MyButton("Start editing", Color.yellow))
                    {
                        PrefabUtility.UnpackPrefabInstance(ropeMaker.gameObject, PrefabUnpackMode.OutermostRoot, InteractionMode.UserAction);
                        return;
                    }
                });
            base.OnInspectorGUI();
            return;
        }

        var prefabType = PrefabUtility.GetPrefabAssetType(ropeMaker);
        if (prefabType != PrefabAssetType.NotAPrefab)
        {
            EditorUtilEx.MiniBoxedSection(string.Format("<color={0}><b>WARNING</b></color>", ColorUtils.RichTextTag(Color.red)), () => { EditorGUILayout.LabelField("Can not edit prefab ropeMaker"); });
            base.OnInspectorGUI();
            return;
        }


        bool changed = false;
        basicFoldout = EditorGUILayout.Foldout(basicFoldout, "RopeMaker", true);
        if (basicFoldout)
        {
            EditorGUI.indentLevel++;
            EditorGUI.BeginChangeCheck();
            base.OnInspectorGUI();
            changed |= EditorGUI.EndChangeCheck();
            EditorGUI.indentLevel--;
        }

        var rope = ropeMaker.Rope;
        if (rope)
        {
            ropeFoldout = EditorGUILayout.Foldout(ropeFoldout, "Rope", true);
            if (ropeFoldout)
            {
                EditorGUI.indentLevel++;
                EditorGUI.BeginChangeCheck();
                var editor = CreateEditor(rope);
                editor.OnInspectorGUI();
                changed |= EditorGUI.EndChangeCheck();
                EditorGUI.indentLevel--;
            }
        }

        EditorUtilEx.SubSection("RopeMaker editor");

        EditorUtilEx.MiniBoxedSection("Editing",
            () =>
            {
                if (EditorUtilEx.MyButton("Select node prefabs")) RopeNodeSelectorWindow.Open(ropeMaker);

                float newScale = EditorGUILayout.Slider("Scale", ropeMaker.scale, 0.1f, 3);
                float newSpacing = EditorGUILayout.Slider("Spacing", ropeMaker.spacing, -0.9f, 1f);
                float newSmoothness = EditorGUILayout.Slider("Smoothness", ropeMaker.smoothness, 0, 1);
                if (newScale != ropeMaker.scale || newSpacing != ropeMaker.spacing || newSmoothness != ropeMaker.smoothness)
                {
                    Undo.RegisterCompleteObjectUndo(ropeMaker, "Edit");
                    ropeMaker.scale = newScale;
                    ropeMaker.spacing = newSpacing;
                    ropeMaker.smoothness = newSmoothness;
                    changed = true;
                    EditorUtility.SetDirty(ropeMaker);
                }

                if (rope)
                {
                    bool newUseLine = EditorGUILayout.Toggle("Line render", rope.useLineRenderer);
                    if (newUseLine != rope.useLineRenderer)
                    {
                        Undo.RegisterCompleteObjectUndo(rope, "Edit");
                        rope.useLineRenderer = newUseLine;
                        changed = true;
                        EditorUtility.SetDirty(rope);
                    }
                }

                var nodes = ropeMaker.GetComponentsInChildren<RopeNode>();
                EditorGUILayout.BeginHorizontal();
                {
                    int nl = nodes.Length;
                    float nlRatio = Mathf.Clamp01((float)nl / 100);
                    EditorGUILayout.LabelField("Nodes count", nl.ToString());

                    var rectBase = GUILayoutUtility.GetRect(GUIContent.none, EditorStyles.helpBox, GUILayout.Width(200));
                    var finalRect = rectBase.Grown(-3);
                    finalRect.width *= nlRatio;
                    var c = Color.Lerp(Color.green, Color.red, nlRatio);
                    EditorGUI.HelpBox(rectBase, "", MessageType.None);
                    EditorGUI.DrawRect(finalRect, c);
                    if (nlRatio > 0.75f) EditorGUI.DropShadowLabel(rectBase, "Too many nodes!");
                }
                EditorGUILayout.EndHorizontal();

                if (EditorUtilEx.MyButton("Edit rope", editingRope ? (Color?)ColorUtils.lightGreen : null))
                {
                    editingRope = !editingRope;
                    SceneView.RepaintAll();
                }

                if (editingRope)
                {
                    EditorUtilEx.MiniBoxedSection("Controls",
                        () =>
                        {
                            EditorGUILayout.LabelField("Shift to add midpoint");
                            EditorGUILayout.LabelField("Ctrl to delete midpoint");
                            //EditorGUILayout.LabelField("Alt+shift to move midpoint");
                        });
                }
            });

        EditorUtilEx.MiniBoxedSection("Creation",
            () =>
            {
                if (EditorUtilEx.MyButton("Recreate rope")) changed = true;
                if (EditorUtilEx.MyButton("Center rope")) CenterRope(ropeMaker);
            });

        if (changed) TryCreateRope(ropeMaker);
    }


    private void OnSceneGUI()
    {
        if (Application.isPlaying) return;
        if (!editingRope) return;

        var ropeMaker = target as RopeMaker;
        var ec = Event.current;
        bool isClick = ec.type == EventType.MouseDown && ec.button == 0;
        bool changed = false;

        if (ec.shift)
        {
            changed |= DoMidPointAdding(ropeMaker, isClick);
            if (isClick) HandlesUtils.SkipEvent();
        }
        else if (ec.control)
        {
            changed |= DoMidPointRemoving(ropeMaker, isClick);
        }
        else
        {
            for (int i = 0; i < ropeMaker.midPoints.Count; i++)
            {
                changed |= MoveLocalPos(ropeMaker, () => ropeMaker.midPoints[i], v => ropeMaker.midPoints[i] = v);
            }

            changed |= MoveLocalPos(ropeMaker, () => ropeMaker.end1, v => ropeMaker.end1 = v);
            changed |= MoveLocalPos(ropeMaker, () => ropeMaker.end2, v => ropeMaker.end2 = v);

            var hsize = HandleUtility.GetHandleSize(ropeMaker.transform.position) * 0.5f;

            void doDrawEnd(
                string name,
                Vector3 endPos,
                RopeMaker.FixEndType fixType,
                System.Action<RopeMaker.FixEndType> setTypeCallback)
            {
                var wp = ropeMaker.transform.TransformPoint(endPos);
                var guiPos = HandleUtility.WorldToGUIPoint(wp);

                var btnSize = new Vector2(80, 40);
                var btnPos = guiPos - Vector2.down * (15 + btnSize.y / 2f);
                Rect btnRect = new Rect().FromCenter(btnPos, btnSize);
                var btnText = name + "\n" + fixType;
                if (GUI.Button(btnRect, btnText))
                {
                    var menu = new GenericMenu();
                    var vals = (RopeMaker.FixEndType[])System.Enum.GetValues(typeof(RopeMaker.FixEndType));
                    foreach (var val in vals)
                    {
                        menu.AddItem(new GUIContent(val.ToString()),
                            val == fixType,
                            () =>
                            {
                                if (val != fixType)
                                {
                                    Undo.RegisterCompleteObjectUndo(ropeMaker, "Edit");
                                    setTypeCallback?.Invoke(val);
                                    EditorUtility.SetDirty(ropeMaker);
                                    TryCreateRope(ropeMaker);
                                }
                            });
                    }

                    menu.ShowAsContext();
                }
            }

            ;

            Handles.BeginGUI();
            doDrawEnd("End 1", ropeMaker.end1, ropeMaker.fixEnd1, t => ropeMaker.fixEnd1 = t);
            doDrawEnd("End2", ropeMaker.end2, ropeMaker.fixEnd2, t => ropeMaker.fixEnd2 = t);
            Handles.EndGUI();
        }

        if (changed) TryCreateRope(ropeMaker);
        SceneView.RepaintAll();
    }

    bool MoveLocalPos(
        RopeMaker rm,
        System.Func<Vector3> localPosGetter,
        System.Action<Vector3> localPosSetter)
    {
        var lp = localPosGetter();
        var wp = rm.transform.TransformPoint(lp);

        var rot = Tools.pivotRotation == PivotRotation.Global ? Quaternion.identity : rm.transform.rotation;
        var newWP = Handles.PositionHandle(wp, rot);

        if (newWP != wp)
        {
            var newLP = rm.transform.InverseTransformPoint(newWP);
            Undo.RegisterCompleteObjectUndo(rm, "Rope edit");
            localPosSetter(newLP);
            EditorUtility.SetDirty(rm);

            return true;
        }

        return false;
    }

    bool DoMidPointAdding(
        RopeMaker rm,
        bool click)
    {
        if (!SceneViewUtils.Get2DMouseScenePosition(out var mousePos)) return false;

        float hSize = HandleUtility.GetHandleSize(mousePos) * 0.25f;
        var wps = rm.AllWorldPoints.ToList();
        int closestIndex = -1;
        float closestIndexDist = -1;
        float maxDistance = 500;
        for (int i = 0; i < wps.Count - 1; i++)
        {
            var p1 = wps[i];
            var p2 = wps[i + 1];
            var d = HandleUtility.DistancePointToLineSegment(mousePos, p1, p2);
            if (d < maxDistance && (d < closestIndexDist || closestIndexDist < 0))
            {
                closestIndex = i;
                closestIndexDist = d;
            }
        }

        if (closestIndex >= 0)
        {
            Handles.color = Color.cyan;
            Handles.DrawWireDisc(mousePos, Vector3.forward, hSize);
            Handles.DrawDottedLine(wps[closestIndex], mousePos, 2);
            Handles.DrawDottedLine(wps[closestIndex + 1], mousePos, 2);

            if (click)
            {
                var lp = rm.transform.InverseTransformPoint(mousePos);
                lp.z = 0;

                Undo.RegisterCompleteObjectUndo(rm, "Rope edit");
                if (closestIndex >= rm.midPoints.Count) rm.midPoints.Add(lp);
                else rm.midPoints.Insert(closestIndex, lp);
                EditorUtility.SetDirty(rm);
                return true;
            }
        }

        return false;
    }

    bool DoMidPointRemoving(
        RopeMaker rm,
        bool click)
    {
        if (!SceneViewUtils.Get2DMouseScenePosition(out var mousePos)) return false;

        float hSize = HandleUtility.GetHandleSize(mousePos) * 0.25f;
        Handles.color = Color.red;
        for (int i = 0; i < rm.midPoints.Count; i++)
        {
            var lmp = rm.midPoints[i];
            var wmp = rm.transform.TransformPoint(lmp);
            if (Handles.Button(wmp,
                Quaternion.identity,
                hSize,
                hSize,
                Handles.SphereHandleCap))
            {
                Undo.RegisterCompleteObjectUndo(rm, "Rope edit");
                rm.midPoints.RemoveAt(i);
                EditorUtility.SetDirty(rm);
                return true;
            }
        }

        return false;
    }

    void CenterRope(
        RopeMaker rm)
    {
#if UNITY_EDITOR
        var nodes = rm.GetComponentsInChildren<RopeNode>();
        var bounds = ObjectUtils.GetBounds(nodes.Select(n => n.transform.position)
            .ToList());
        var offset = bounds.center - rm.transform.position;
        ObjectUtils.ModifyTransformWithoutChildren(rm.transform, t => { t.position += offset; });
        var localOffset = rm.transform.InverseTransformVector(offset);
        rm.end1 -= localOffset;
        rm.end2 -= localOffset;
        rm.midPoints = rm.midPoints.Select(v => v - localOffset)
            .ToList();

        TryCreateRope(rm);
#endif

    }
}

#endif