
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class Rope : MonoBehaviour
{
    private static MaterialPropertyBlock _MPB;
    private static MaterialPropertyBlock MPB => _MPB ?? (_MPB = new MaterialPropertyBlock());
    public LineRenderer lr;
    public bool generateStrechBridgeColliders = false;
    public bool useLineRenderer = true;
    public float lineWidthMultiplier = 0.5f;
    public int shortestNodeCount = 1;
    [HideInInspector] public float nodeLineLengthFactor = 0.5f;

    private List<RopeNode> _cachedNodes;
    public bool isSubCut { get; private set; }
    public bool isFadingOut { get; private set; }
    public bool isLineRendering => lr && useLineRenderer;

    public bool IsComplete { get; set; } = false;
    GameObject hand;
    private bool isCutRope = false;
    [SerializeField] private GameObject handPrefab;
    private void Start()
    {
        RefreshVisuals();
        if (generateStrechBridgeColliders) GenerateStrechBridgeColliders();

        isCutRope = false;

        // Improve physics solver iterations for stiffer ropes
        Physics2D.velocityIterations = Mathf.Max(Physics2D.velocityIterations, 5);
        Physics2D.positionIterations = Mathf.Max(Physics2D.positionIterations, 5);

        /*hand = Instantiate(handPrefab, transform);
        var handControl = hand.GetComponentInChildren<Hand>();
        if (handControl == null) return;
        handControl.CutRope(this.transform.position);*/
    }

    private void CopyInstanceData(
        Rope r)
    {
        isFadingOut = r.isFadingOut;
    }


    public void OnNodesCreated() { RefreshVisuals(); }

    public List<RopeNode> GetNodes()
    {
        bool shouldRenew = false;
        if (_cachedNodes == null) shouldRenew = true;
        else if (_cachedNodes.Count == 0) shouldRenew = true;
        else if (_cachedNodes.Any(n => !n)) shouldRenew = true;

        if (shouldRenew) _cachedNodes = new List<RopeNode>(GetComponentsInChildren<RopeNode>());
        return _cachedNodes;
    }


    public void RefreshVisuals()
    {
        List<RopeNode> nodes = GetNodes();
        foreach (var n in nodes) n.SetUseLineRenderer(isLineRendering);
        if (lr) lr.enabled = useLineRenderer;
        UpdateLineRenderer();
    }

    void LateUpdate()
    {
        UpdateLineRenderer();
    }
    public void SetLineWidth(
        float f)
    {
        if (lr) lr.widthMultiplier = f * lineWidthMultiplier;
    }

    private void UpdateLineRenderer()
    {
        if (isLineRendering)
        {
            List<RopeNode> nodes = GetNodes();
            if (nodes != null)
            {
                var points = nodes.Select(n => n.transform.localPosition)
                    .ToArray();
                if (nodes.Count == 1)
                {
                    var n0 = nodes[0];
                    Vector3 localHalfSide = transform.InverseTransformVector(n0.transform.right * n0.nodeLength * 0.5f * nodeLineLengthFactor);
                    points = new Vector3[] { n0.transform.localPosition - localHalfSide, n0.transform.localPosition + localHalfSide, };
                }

                lr.useWorldSpace = false;
                lr.positionCount = points.Length;
                lr.SetPositions(points);

                lr.GetPropertyBlock(MPB);
                MPB.SetFloat("_RopeLength", Mathf.Max(1f, nodes.Count - 1));
                lr.SetPropertyBlock(MPB);
            }
        }
        else if (lr) lr.positionCount = 0;
    }


    private void GenerateStrechBridgeColliders()
    {
        List<RopeNode> nodes = GetNodes();
        if (nodes != null)
        {
            foreach (var n in nodes)
            {
                var joints = n.GetComponents<Joint2D>();
                foreach (var j in joints)
                {
                    if (j && j.connectedBody)
                    {
                        var nOther = j.connectedBody.GetComponent<RopeNode>();
                        if (nOther != n && nodes.Contains(nOther))
                        {
                            n.stretchBridge = n.gameObject.AddComponent<StretchBridge>();
                            n.stretchBridge.capCol = n.gameObject.AddComponent<CapsuleCollider2D>();
                            n.stretchBridge.capCol.size = new Vector2(0.1f, 0.1f);
                            n.stretchBridge.capCol.direction = CapsuleDirection2D.Horizontal;
                            n.stretchBridge.capCol.isTrigger = false;
                            n.stretchBridge.thisNode = n;
                            n.stretchBridge.thatNode = nOther;
                            n.stretchBridge.joint = j;

                            Physics2D.IgnoreCollision(n.stretchBridge.capCol, n.col);
                            Physics2D.IgnoreCollision(n.stretchBridge.capCol, nOther.col);
                        }
                    }
                }
            }
        }
    }


    public virtual void SplitRopeAt(
        RopeNode node,
        bool before,
        bool isLastElement = false)
    {
        if (isFadingOut) return;

        var nodes = GetNodes();
        if (nodes == null)
        {
            Debug.LogError("Nodes is null. Skip splitting rope.");
            return;
        }

        if (!isCutRope && hand != null)
        {
            isCutRope = true;
            hand.gameObject.SetActive(false);
        }
        if (nodes.Count < shortestNodeCount * 2) return;

        var nodeIndex = nodes.IndexOf(node);
        int lastIndexToKeep = nodeIndex;
        if (before) lastIndexToKeep--;
        if (lastIndexToKeep < 0 || lastIndexToKeep >= nodes.Count - 1)
        {
            if (!isLastElement)
            {
                return;
            }

            lastIndexToKeep = nodes.Count - shortestNodeCount - 1;
        }

        lastIndexToKeep = Mathf.Clamp(lastIndexToKeep, shortestNodeCount - 1, nodes.Count - shortestNodeCount - 1);

        var newRope = Instantiate(this, transform.parent);
        foreach (var pair in nodes.Zip(newRope.GetNodes(),
            (
                n1,
                n2) => (n1.rb, n2.rb)))
        {
            pair.Item2.linearVelocity = pair.Item1.linearVelocity;
            pair.Item2.angularVelocity = pair.Item1.angularVelocity;
            pair.Item1.constraints = RigidbodyConstraints2D.None;
        }

        bool fadeThis = nodes[0]
            .GetFixedType() != RopeMaker.FixEndType.Free;
        bool fadeThat = isFadingOut || nodes[nodes.Count - 1]
            .GetFixedType() != RopeMaker.FixEndType.Free;

        TrimRope(lastIndexToKeep, true);
        newRope.TrimRope(lastIndexToKeep, false);

        if (fadeThis) FadeOut(0.15f, 0.1f);
        //if (fadeThat) newRope.FadeOut(1.5f, 2.5f);

        if (!isSubCut)
        {
            //var levelRoot = GetComponentInParent<LevelRoot>();
            //if (levelRoot) levelRoot.RegisterActionCoin(node.col.bounds.center);
        }

        isSubCut = true;
        //newRope.isSubCut = true;

        IsComplete = true;
    }

    private void TrimRope(
        int index,
        bool leftside)
    {
        var nodes = GetNodes();
        if (nodes != null)
        {
            index = Mathf.Min(index, nodes.Count - 1);
            int from = leftside ? 0 : index + 1;
            int to = leftside ? index : nodes.Count - 1;

            HashSet<RopeNode> toDestroyNodes = new HashSet<RopeNode>(nodes.Skip(from)
                .Take(to - from + 1));
            foreach (var n in nodes)
            {
                if (toDestroyNodes.Contains(n)) Destroy(n.gameObject);
                else
                {
                    var joints = n.GetComponents<Joint2D>();
                    foreach (var j in joints)
                    {
                        var connectedNodeBody = j.connectedBody?.GetComponent<RopeNode>();
                        if (toDestroyNodes.Contains(connectedNodeBody))
                        {
                            Destroy(j);
                        }
                    }
                }
            }

            nodes.RemoveAll(toDestroyNodes.Contains);

            if (nodes.Count == 0)
            {
                Destroy(gameObject);
            }
        }
    }


    private void FadeOut(
        float duration,
        float delay)
    {
        if (isFadingOut) return;
        isFadingOut = true;

        var nodes = GetNodes();
        foreach (var n in nodes)
        {
            n.rb.mass = 0.01f;
        }

        if (useLineRenderer)
        {
            var gradient0 = lr.colorGradient;
            /*DOTween.To(f =>
                {
                    lr.colorGradient = new Gradient()
                    {
                        mode = gradient0.mode,
                        colorKeys = gradient0.colorKeys,
                        alphaKeys = gradient0.alphaKeys.Select(k => new GradientAlphaKey(k.alpha * f, k.time)).ToArray(),
                    };
                }, 1, 0, duration)
                .SetDelay(delay)
                .OnComplete(() => { Destroy(gameObject); });*/
        }
        else
        {
            /*var seq = DOTween.Sequence();
            seq.SetId(this);
            for (int i = 0; i < nodes.Count; i++)
            {
                var n = nodes[i];
                if (i == 0) seq.Append(n.rend.DOFade(0, duration));
                else seq.Join(n.rend.DOFade(0, duration));
            }
            
            seq.AppendCallback(() => { Destroy(gameObject); });*/
        }
    }

    void OnDisabled()
    {
        if (!isCutRope && hand != null)
        {
            isCutRope = true;
            hand.gameObject.SetActive(false);
        }
    }
}